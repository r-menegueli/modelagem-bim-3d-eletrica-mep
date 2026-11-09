"""Evidence-linked topology graph for the GIS substation Revit audit.

This graph keeps switching topology, Revit electrical systems, geometric routes,
and cable-tray infrastructure as separate evidence classes. It never upgrades a
geometric route or connector count into an electrical connection.
"""
from __future__ import annotations

import argparse
import csv
import json
import math
import re
from collections import Counter, defaultdict
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any, Iterable


@dataclass(frozen=True)
class NodeSpec:
    node_id: str
    label: str
    voltage_kv: float | None
    marks: tuple[str, ...] = ()
    external: bool = False
    evidence_class: str = "BIM_EQUIPMENT"


@dataclass(frozen=True)
class EdgeSpec:
    edge_id: str
    from_node: str
    to_node: str
    voltage_kv: float | None
    state: str
    evidence_class: str
    diagram_ref: str
    equipment_marks: tuple[str, ...] = ()
    geometry_prefixes: tuple[str, ...] = ()
    limitation: str = ""
    system_id: str = ""


def _node(node_id: str, label: str, voltage: float | None, *marks: str,
          external: bool = False, evidence: str = "BIM_EQUIPMENT") -> NodeSpec:
    return NodeSpec(node_id, label, voltage, tuple(marks), external, evidence)


NODES: tuple[NodeSpec, ...] = (
    _node("GRID-L01", "Rede externa 138 kV L01", 138.0, external=True, evidence="EXTERNAL_BOUNDARY"),
    _node("GRID-L02", "Rede externa 138 kV L02", 138.0, external=True, evidence="EXTERNAL_BOUNDARY"),
    _node("BUS-138-A", "Barra A GIS 138 kV", 138.0, "GIS-BUSA-ENCLOSURE", evidence="ENCLOSURE_GEOMETRY_ONLY"),
    _node("BUS-138-B", "Barra B GIS 138 kV", 138.0, "GIS-BUSB-ENCLOSURE", evidence="ENCLOSURE_GEOMETRY_ONLY"),
)

_bay_data = (
    ("GIS-L01", "Entrada L01", "BUS-138-A", "GIS-L01-89A", "GIS-L01-89B", "GIS-L01-52", "GIS-L01-CT", "GIS-L01-TERM", "ASSUMED_CLOSED_NORMAL", "ASSUMED_OPEN_NORMAL"),
    ("GIS-T01", "Transformador TR-01", "BUS-138-A", "GIS-T01-89A", "GIS-T01-89B", "GIS-T01-52", "GIS-T01-CT", "TR-01", "ASSUMED_CLOSED_NORMAL", "ASSUMED_OPEN_NORMAL"),
    ("GIS-T02", "Transformador TR-02", "BUS-138-B", "GIS-T02-89A", "GIS-T02-89B", "GIS-T02-52", "GIS-T02-CT", "TR-02", "ASSUMED_OPEN_NORMAL", "ASSUMED_CLOSED_NORMAL"),
    ("GIS-L02", "Entrada L02", "BUS-138-B", "GIS-L02-89A", "GIS-L02-89B", "GIS-L02-52", "GIS-L02-CT", "GIS-L02-TERM", "ASSUMED_OPEN_NORMAL", "ASSUMED_CLOSED_NORMAL"),
)

_mutable_nodes = list(NODES)
_mutable_edges: list[EdgeSpec] = []
for bay, label, selected_bus, sw_a, sw_b, breaker, ct, endpoint, state_a, state_b in _bay_data:
    bus_b = "BUS-138-B" if selected_bus == "BUS-138-A" else "BUS-138-A"
    for mark, bus, state, side in ((sw_a, selected_bus, state_a, "A"), (sw_b, bus_b, state_b, "B")):
        _mutable_nodes.append(_node(mark, f"{bay} seccionadora de barra {side}", 138.0, mark))
        _mutable_edges.append(EdgeSpec(f"{bay}-{side}-SELECTOR", bus, mark, 138.0, state,
            "GEOMETRIC_ROUTE_ONLY", "E-01", (mark,), (f"{bay}-89{side}-STUB",),
            limitation="Carcaça geométrica entre barra e seccionadora; contato, estado e condutor não são conectados eletricamente no RVT."))
        _mutable_edges.append(EdgeSpec(f"{bay}-{side}-TO-BREAKER", mark, breaker, 138.0, state,
            "DIAGRAM_ONLY", "E-01", (mark, breaker), limitation="O caminho selecionado é uma relação de unifilar; sem PowerCircuit ou contato comutável."))
    _mutable_nodes.extend((
        _node(breaker, f"{bay} disjuntor 52", 138.0, breaker),
        _node(ct, f"{bay} TC placeholder", 138.0, ct),
    ))
    _mutable_edges.append(EdgeSpec(f"{bay}-52-TO-CT", breaker, ct, 138.0, "ASSUMED_CLOSED_NORMAL",
        "DIAGRAM_ONLY", "E-01", (breaker, ct), limitation="TC é placeholder de uma família; não comprova um conjunto trifásico selecionado."))
    end_node = endpoint
    if endpoint.endswith("TERM"):
        grid_node = "GRID-L01" if bay == "GIS-L01" else "GRID-L02"
        _mutable_nodes.append(_node(endpoint, f"{label} terminação GIS", 138.0, endpoint))
        _mutable_edges.append(EdgeSpec(f"{bay}-CT-TO-TERM", ct, endpoint, 138.0, "DIAGRAM_RELATION",
            "DIAGRAM_ONLY", "E-01", (ct, endpoint), limitation="Terminação representada; condutor primário e interface externa não modelados."))
        _mutable_edges.append(EdgeSpec(f"{bay}-TERM-TO-GRID", endpoint, grid_node, 138.0, "EXTERNAL_NOT_MODELED",
            "DIAGRAM_ONLY", "E-01", (endpoint,), limitation="Conexão com a rede da concessionária fora do limite do modelo."))
    else:
        _mutable_nodes.extend((_node(endpoint, f"{endpoint} tanque", 138.0, endpoint),
                               _node(f"{endpoint}-HV-TERMINALS", f"{endpoint} buchas de alta tensão", 138.0, *(f"{endpoint}-B-HV-{p}" for p in (1, 2, 3))),
                               _node(f"{endpoint}-MV-TERMINALS", f"{endpoint} buchas de média tensão", 13.8, *(f"{endpoint}-B-MV-{p}" for p in (1, 2, 3)))))
        phase = "TR01" if endpoint == "TR-01" else "TR02"
        _mutable_edges.append(EdgeSpec(f"{bay}-CT-TO-{endpoint}-HV", ct, f"{endpoint}-HV-TERMINALS", 138.0, "GEOMETRIC_ROUTE_APPROXIMATE",
            "GEOMETRIC_ROUTE_ONLY", "E-01 / E-03", (ct, endpoint, *(f"{endpoint}-B-HV-{p}" for p in (1, 2, 3))),
            tuple(f"GIS-{phase}-HV-PH-{p}-ROUTE" for p in "ABC"),
            "Uma DirectShape por rota de fase reúne segmentos geométricos; extremos medidos ao envelope de família, sem porta, condutor ou conector elétrico."))

_mutable_nodes.extend((
    _node("GIS-BC01-89A", "Acoplador 138 kV seccionadora A", 138.0, "GIS-BC01-89A"),
    _node("GIS-BC01-52", "Acoplador 138 kV disjuntor", 138.0, "GIS-BC01-52"),
    _node("GIS-BC01-89B", "Acoplador 138 kV seccionadora B", 138.0, "GIS-BC01-89B"),
    _node("GIS-BC01-CT", "TC placeholder do acoplador", 138.0, "GIS-BC01-CT"),
    _node("P-BUS", "Painel funcional de proteção de barras", 0.125, "P-BUS"),
    _node("BUS-MV-A", "Barra MT seção A 13,8 kV", 13.8, "BUS-MV-A"),
    _node("BUS-MV-B", "Barra MT seção B 13,8 kV", 13.8, "BUS-MV-B"),
    _node("QMT-TIE", "Cubículo de acoplamento MT", 13.8, "QMT-TIE"),
    _node("QAC-01", "Quadro auxiliar CA", 0.38, "QAC-01"),
    _node("UPS-01", "UPS", 0.38, "UPS-01"),
))
_mutable_edges.extend((
    EdgeSpec("GIS-BC-A-SELECTOR", "BUS-138-A", "GIS-BC01-89A", 138.0, "ASSUMED_CLOSED_NORMAL", "GEOMETRIC_ROUTE_ONLY", "E-01", ("GIS-BC01-89A",), ("GIS-BC01-89A-STUB",),
             limitation="Carcaça geométrica entre barra A e seccionadora; contato, estado e condutor não são conectados eletricamente no RVT."),
    EdgeSpec("GIS-BC-A-TO-52", "GIS-BC01-89A", "GIS-BC01-52", 138.0, "ASSUMED_CLOSED_NORMAL", "DIAGRAM_ONLY", "E-01", ("GIS-BC01-89A", "GIS-BC01-52")),
    EdgeSpec("GIS-BC-52-TO-CT", "GIS-BC01-52", "GIS-BC01-CT", 138.0, "ASSUMED_OPEN_NORMAL", "DIAGRAM_ONLY", "E-01", ("GIS-BC01-52", "GIS-BC01-CT"),
             limitation="E-01 places CT-BC01* in series on the 52-BC downstream side. The normally open 52-BC state is a documented assumption; no primary contact, terminal, ratio, core, phase set, or native circuit is validated."),
    EdgeSpec("GIS-BC-CT-TO-89B", "GIS-BC01-CT", "GIS-BC01-89B", 138.0, "DIAGRAM_RELATION", "DIAGRAM_ONLY", "E-01", ("GIS-BC01-CT", "GIS-BC01-89B"),
             limitation="Diagram-only series relation from CT-BC01* to selector 89B; exact GIS module arrangement and terminal interfaces await the vendor GA."),
    EdgeSpec("GIS-BC-B-SELECTOR", "GIS-BC01-89B", "BUS-138-B", 138.0, "ASSUMED_CLOSED_NORMAL", "GEOMETRIC_ROUTE_ONLY", "E-01", ("GIS-BC01-89B",), ("GIS-BC01-89B-STUB",),
             limitation="Carcaça geométrica entre seccionadora e barra B; contato, estado e condutor não são conectados eletricamente no RVT."),
    EdgeSpec("GIS-BC-CT-PROTECTION", "GIS-BC01-CT", "P-BUS", None, "CANDIDATE_PROTECTION_RELATION", "DIAGRAM_ONLY", "E-01 / E-06", ("GIS-BC01-CT", "P-BUS"),
             limitation="87B/50BF são funções candidatas; relação secundária, núcleo, posição e associação ao painel pendentes. Não é conexão 125 Vcc."),
))

for transformer, section, cubicle, phase_prefix in (
    ("TR-01", "BUS-MV-A", "QMT-A-IN", "TR01"),
    ("TR-02", "BUS-MV-B", "QMT-B-IN", "TR02"),
):
    terminals = tuple(f"{transformer}-B-MV-{p}" for p in (1, 2, 3))
    _mutable_nodes.extend((_node(cubicle, f"Cubículo de entrada {section}", 13.8, cubicle),
                           _node(f"{section}-IN-TERMINALS", f"Terminações MT {transformer}", 13.8, *terminals)))
    _mutable_edges.append(EdgeSpec(f"{transformer}-MV-ROUTE", f"{transformer}-MV-TERMINALS", cubicle, 13.8, "GEOMETRIC_ROUTE_APPROXIMATE",
        "GEOMETRIC_ROUTE_ONLY", "E-01 / E-03 / E-04", (*terminals, cubicle),
        tuple(f"{phase_prefix}-MV-PH-{p}-ROUTE" for p in "ABC"),
        "Uma DirectShape por rota geométrica; extremos medidos ao envelope de família, sem porta, cabo/busduct, seção, ampacidade, terminação ou conexão MEP."))
    _mutable_edges.append(EdgeSpec(f"{cubicle}-TO-{section}", cubicle, section, 13.8, "DIAGRAM_RELATION", "DIAGRAM_ONLY", "E-01 / E-04", (cubicle, section),
        limitation="Contato interno do cubículo e barramento não são circuito elétrico nativo."))

for side in "AB":
    section = f"BUS-MV-{side}"
    for feeder in range(1, 6):
        tag = f"QMT-{side}-F{feeder:02d}"
        load = f"LOAD-{side}-F{feeder:02d}"
        _mutable_nodes.extend((_node(tag, f"Cubículo alimentador {tag}", 13.8, tag),
                               _node(load, f"Carga remota {tag}", 13.8, external=True, evidence="UNMODELED_REMOTE_LOAD")))
        _mutable_edges.extend((
            EdgeSpec(f"{section}-TO-{tag}", section, tag, 13.8, "DIAGRAM_RELATION", "DIAGRAM_ONLY", "E-01", (section, tag),
                     limitation="Disjuntor, cabo de saída, proteção e terminação remota não associados como circuito nativo."),
            EdgeSpec(f"{tag}-TO-REMOTE-LOAD", tag, load, 13.8, "DESTINATION_UNDEFINED", "DIAGRAM_ONLY", "E-01",
                     (tag,), limitation="Destino, demanda e cabo do alimentador ainda não especificados."),
        ))

_mutable_edges.extend((
    EdgeSpec("MV-TIE-A-TO-CUBICLE", "BUS-MV-A", "QMT-TIE", 13.8, "ASSUMED_OPEN_NORMAL", "DIAGRAM_ONLY", "E-01", ("QMT-TIE",)),
    EdgeSpec("MV-TIE-CUBICLE-TO-B", "QMT-TIE", "BUS-MV-B", 13.8, "ASSUMED_OPEN_NORMAL", "DIAGRAM_ONLY", "E-01", ("QMT-TIE",)),
    EdgeSpec("AUX-QAC-TO-UPS", "QAC-01", "UPS-01", 0.38, "SYSTEM_MEMBERSHIP_TO_AUDIT", "NATIVE_ELECTRICAL_SYSTEM_LOGICAL", "E-06", ("QAC-01", "UPS-01"),
             limitation="ElectricalSystem lógico e Wire 2D view-specific, se confirmados no audit; não há cabo 3D, fonte a montante nem circuito CC da bateria.", system_id="AUX-UPS-01"),
))

_protection_panel_marks = (
    "P-L01", "P-L02", "P-BUS", "P-TR21-A", "P-TR21-B", "P-TR22-A", "P-TR22-B",
    "P-BC", "P-SCADA", "TR-01-CTRL", "TR-02-CTRL",
)
for panel_mark in _protection_panel_marks:
    _mutable_nodes.append(_node(
        f"AUX-INPUT-{panel_mark}", f"Entrada auxiliar CA do painel {panel_mark}", 0.220,
        panel_mark, evidence="ASSUMED_220V_LINE_TO_NEUTRAL_PANEL_INPUT",
    ))
    _mutable_edges.append(EdgeSpec(
        f"AUX-QAC-TO-{panel_mark}", "QAC-01", f"AUX-INPUT-{panel_mark}", 0.220,
        "SYSTEM_MEMBERSHIP_TO_AUDIT", "NATIVE_ELECTRICAL_SYSTEM_LOGICAL", "E-06",
        ("QAC-01", panel_mark),
        limitation=("150 W, FP 0.90 and 220 V line-neutral are explicit preliminary assumptions based on AUX-AC-06 "
                    "and the 380Y/220 V four-wire setup; native circuit membership must match audited ElementIds/UniqueIds. "
                    "No 3D cable, conductor sizing, protection, or internal 125 Vcc circuit is implied."),
        system_id=f"AUX-AC-{panel_mark}",
    ))

INFRASTRUCTURE_SPECS = (
    ("LEITO-MV-A", "Infraestrutura de cabos da seção A"),
    ("LEITO-MV-B", "Infraestrutura de cabos da seção B"),
    ("LEITO-GIS", "Infraestrutura de cabos GIS"),
    ("LEITO-CTRL", "Infraestrutura de controle"),
)
NODES = tuple(_mutable_nodes)
EDGES = tuple(_mutable_edges)


def _matches(mark: str, patterns: Iterable[str]) -> bool:
    return any(mark == p or (p.endswith("*") and mark.startswith(p[:-1])) for p in patterns)


def _matches_geometry_prefix(mark: str, prefix: str) -> bool:
    if mark.startswith(prefix):
        return True
    if prefix.endswith("-ROUTE"):
        return mark.startswith(prefix[:-len("ROUTE")] + "SEG-")
    return False


def _load_rows(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream, delimiter=";"))
    for row in rows:
        row["domain_evidence_source"] = "CONNECTIVITY_EQUIPMENT_EXPORT" if row.get("dominios", "").strip() else "NOT_EXTRACTED"
    quantities_path = path.parent / "quantitativos-modelo.csv"
    if not any(not row.get("dominios", "").strip() for row in rows) or not quantities_path.exists():
        return rows
    with quantities_path.open("r", encoding="utf-8-sig", newline="") as stream:
        quantities = list(csv.DictReader(stream, delimiter=";"))
    by_identity: dict[tuple[str, str], list[dict[str, str]]] = defaultdict(list)
    for quantity in quantities:
        identity = (str(quantity.get("element_id", "")).strip(), str(quantity.get("unique_id", "")).strip())
        if all(identity):
            by_identity[identity].append(quantity)
    for row in rows:
        if row.get("dominios", "").strip():
            continue
        identity = (str(row.get("element_id", "")).strip(), str(row.get("unique_id", "")).strip())
        matches = by_identity.get(identity, [])
        if len(matches) == 1 and matches[0].get("dominios", "").strip():
            row["dominios"] = matches[0]["dominios"]
            row["domain_evidence_source"] = "QUANTITIES_EXPORT_JOINED_BY_ELEMENT_ID_AND_UNIQUE_ID"
    return rows


def _native_systems_confirmed(
    elements: list[dict[str, str]], audit: dict[str, Any]
) -> dict[str, dict[str, Any]]:
    rows_by_mark: dict[str, list[dict[str, str]]] = defaultdict(list)
    for row in elements:
        mark = row.get("marca", "")
        if mark:
            rows_by_mark[mark].append(row)
    duplicate_element_ids = _duplicate_values(str(row.get("element_id", "")).strip() for row in elements)
    duplicate_unique_ids = _duplicate_values(str(row.get("unique_id", "")).strip() for row in elements)
    systems_by_id: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for system in audit.get("electrical_system_details", []):
        if isinstance(system, dict):
            systems_by_id[str(system.get("system_id", ""))].append(system)
    confirmed: dict[str, dict[str, Any]] = {}
    for system_id, system_group in systems_by_id.items():
        if not system_id or len(system_group) != 1:
            continue
        system = system_group[0]
        if not (
            system.get("revit_class") == "ElectricalSystem"
            and system.get("system_type") == "PowerCircuit"
            and system.get("native_membership_confirmed") is True
            and system.get("panel_assignment_confirmed") is True
            and system.get("terminal_membership_confirmed") is True
        ):
            continue
        terminal_elements = system.get("terminal_elements") or []
        panel = system.get("selected_panel_element") or {}
        qac_rows = rows_by_mark.get("QAC-01", [])
        panel_row = qac_rows[0] if len(qac_rows) == 1 else None
        panel_id = str(panel.get("element_id", "")).strip()
        panel_uid = str(panel.get("unique_id", "")).strip()
        if not (
            panel_row
            and panel_id
            and panel_uid
            and panel_id not in duplicate_element_ids
            and panel_uid not in duplicate_unique_ids
            and panel.get("mark") == "QAC-01"
            and panel_uid == panel_row.get("unique_id")
            and panel_id == str(panel_row.get("element_id", ""))
            and "DomainElectrical" in panel_row.get("dominios", "")
            and _as_int(panel_row.get("isconnected_true")) > 0
        ):
            continue
        expected_terminal = "UPS-01" if system_id == "AUX-UPS-01" else (
            system_id.removeprefix("AUX-AC-") if system_id.startswith("AUX-AC-") else ""
        )
        terminal_matches = [row for row in terminal_elements if row.get("mark") == expected_terminal]
        terminal_rows_by_mark = rows_by_mark.get(expected_terminal, [])
        terminal_row = terminal_rows_by_mark[0] if len(terminal_rows_by_mark) == 1 else None
        terminal_id = str(terminal_matches[0].get("element_id", "")).strip() if terminal_matches else ""
        terminal_uid = str(terminal_matches[0].get("unique_id", "")).strip() if terminal_matches else ""
        if not (
            expected_terminal
            and terminal_id
            and terminal_uid
            and terminal_id not in duplicate_element_ids
            and terminal_uid not in duplicate_unique_ids
            and len(terminal_matches) == 1
            and terminal_row
            and terminal_id == str(terminal_row.get("element_id", ""))
            and terminal_uid == terminal_row.get("unique_id")
            and "DomainElectrical" in terminal_row.get("dominios", "")
            and _as_int(terminal_row.get("isconnected_true")) > 0
        ):
            continue
        confirmed[system_id] = system
    return confirmed


def _duplicate_values(values: Iterable[str]) -> set[str]:
    counts = Counter(value for value in values if value)
    return {value for value, count in counts.items() if count > 1}


_PHASE_PREFIX = re.compile(r"PH-([ABC])-(?:SEG-|ROUTE(?:$|-))")
_NON_CONDUCTOR_CATEGORY_TOKENS = ("wire", "cable tray", "cabletray", "conduit", "eletrocalha", "eletroduto", "bandeja")
_GEOMETRIC_ROUTE_ENDPOINTS = {
    "GIS-T01-CT-TO-TR-01-HV": ("GIS-T01-CT", "TR-01-B-HV-{index}"),
    "GIS-T02-CT-TO-TR-02-HV": ("GIS-T02-CT", "TR-02-B-HV-{index}"),
    "TR-01-MV-ROUTE": ("TR-01-B-MV-{index}", "QMT-A-IN"),
    "TR-02-MV-ROUTE": ("TR-02-B-MV-{index}", "QMT-B-IN"),
}


def _legacy_route_geometry_candidates(audit: dict[str, Any]) -> list[dict[str, Any]]:
    """Adapt older Revit route inventories into explicitly non-conductor candidates."""
    candidates: list[dict[str, Any]] = []
    route_to_edge = (
        ("GIS-TR01-HV-PH-", "GIS-T01-CT-TO-TR-01-HV"),
        ("GIS-TR02-HV-PH-", "GIS-T02-CT-TO-TR-02-HV"),
        ("TR01-MV-PH-", "TR-01-MV-ROUTE"),
        ("TR02-MV-PH-", "TR-02-MV-ROUTE"),
    )
    for route in audit.get("geometric_routes", []):
        if not isinstance(route, dict):
            continue
        mark = str(route.get("mark", "")).strip()
        edge_id = next((edge for prefix, edge in route_to_edge if mark.startswith(prefix)), "")
        phase_match = _PHASE_PREFIX.search(mark)
        if not edge_id or phase_match is None:
            continue
        candidates.append({
            "edge_id": edge_id,
            "phase": phase_match.group(1),
            "evidence_source": "REVIT_API_GEOMETRY_AUDIT",
            "representation_role": "GEOMETRIC_ROUTE_ENVELOPE",
            "element_id": route.get("element_id", ""),
            "unique_id": route.get("unique_id", ""),
            "mark": mark,
            "solid_volume_m3": None,
            "comments": route.get("comments", ""),
            "endpoint_evidence": "NOT_EXTRACTED_FROM_LEGACY_ROUTE_INVENTORY",
        })
    return candidates


def _model_identity_inventory(
    equipment_rows: list[dict[str, str]], audit: dict[str, Any]
) -> list[dict[str, str]]:
    """Join separately extracted Revit element classes to the equipment CSV.

    The add-in deliberately exports family instances to the equipment CSV and
    DirectShape, CableTray, and Wire instances to their own API-audit arrays.
    Treating the family CSV as the complete model inventory drops those native
    elements from route and infrastructure checks.
    """
    supplemental: list[dict[str, str]] = []
    collections = (
        ("geometric_routes", "DirectShape", "GEOMETRIC_ENVELOPE_ONLY_NO_ELECTRICAL_CONNECTOR"),
        ("cable_tray_elements", "Cable Tray", "INFRASTRUCTURE_ONLY"),
        ("electrical_wire_2d_details", "Wire", "2D_VIEW_SPECIFIC_WIRE"),
    )
    for collection_name, category, geometry_status in collections:
        records = audit.get(collection_name, [])
        if not isinstance(records, list):
            continue
        for record in records:
            if not isinstance(record, dict):
                continue
            supplemental.append({
                "element_id": str(record.get("element_id", "")).strip(),
                "unique_id": str(record.get("unique_id", "")).strip(),
                "marca": str(record.get("mark", "")).strip(),
                "categoria_revit": category,
                "model_geometry": str(record.get("model_geometry", geometry_status)).strip(),
                "mep_connectors": str(record.get("connector_count", "")).strip(),
                "isconnected_true": str(record.get("connected_connector_count", "")).strip(),
                "allrefs_externos": ",".join(map(str, record.get("external_allrefs", [])))
                if isinstance(record.get("external_allrefs", []), list) else "",
                "dominios": "",
                "comentarios": str(record.get("comments", "")).strip(),
            })

    # Some synthetic/legacy inputs already put these elements in the CSV. Skip
    # only one exact duplicate from the supplemental audit; preserve additional
    # duplicates so identity checks still flag them.
    existing = Counter(
        (str(row.get("element_id", "")).strip(), str(row.get("unique_id", "")).strip(),
         str(row.get("marca", "")).strip())
        for row in equipment_rows
    )
    inventory = list(equipment_rows)
    for row in supplemental:
        key = (row["element_id"], row["unique_id"], row["marca"])
        if existing[key] > 0:
            existing[key] -= 1
            continue
        inventory.append(row)
    return inventory


def _physical_conductor_geometry_audit(
    model_inventory: list[dict[str, str]], audit: dict[str, Any]
) -> tuple[dict[str, dict[str, Any]], dict[str, Any]]:
    """Validate explicitly extracted 3D conductor solids without treating them as circuits.

    A positive record is accepted only when a Revit API audit identifies a unique
    3D solid element, ties its endpoints to exact BIM terminal instances, supplies
    measured terminal gaps and an explicit tolerance basis, and covers A/B/C for
    the specified three-phase route. This is geometric evidence only.
    """
    raw = audit.get("physical_conductor_details")
    route_candidates_fallback = False
    if "physical_conductor_details" not in audit:
        raw = _legacy_route_geometry_candidates(audit)
        route_candidates_fallback = bool(raw)
        if not raw:
            return {}, {
                "audit_status": "NOT_EXTRACTED",
                "audit_fields_available": False,
                "route_candidates_fallback_used": False,
                "records_observed": 0,
                "records_accepted": 0,
                "rejected_records": [],
            }
    elif not isinstance(raw, list):
        return {}, {
            "audit_status": "MALFORMED_AUDIT_DATA",
            "audit_fields_available": False,
            "route_candidates_fallback_used": False,
            "records_observed": 0,
            "records_accepted": 0,
            "rejected_records": [],
        }

    rows_by_element_id: dict[str, list[dict[str, str]]] = defaultdict(list)
    rows_by_unique_id: dict[str, list[dict[str, str]]] = defaultdict(list)
    for row in model_inventory:
        element_id = str(row.get("element_id", "")).strip()
        unique_id = str(row.get("unique_id", "")).strip()
        mark = str(row.get("marca", "")).strip()
        if element_id:
            rows_by_element_id[element_id].append(row)
        if unique_id:
            rows_by_unique_id[unique_id].append(row)
    node_by_id = {node.node_id: node for node in NODES}
    edge_by_id = {edge.edge_id: edge for edge in EDGES}
    record_identity_counts = Counter(
        (str(record.get("element_id", "")).strip(), str(record.get("unique_id", "")).strip())
        for record in raw
        if isinstance(record, dict)
        and str(record.get("element_id", "")).strip()
        and str(record.get("unique_id", "")).strip()
    )
    expected_phases: dict[str, set[str]] = {
        edge.edge_id: {
            phase
            for prefix in edge.geometry_prefixes
            if (match := _PHASE_PREFIX.search(prefix))
            for phase in (match.group(1),)
        }
        for edge in EDGES
    }
    valid_by_edge_phase: dict[str, dict[str, list[dict[str, Any]]]] = defaultdict(lambda: defaultdict(list))
    rejected_records: list[dict[str, Any]] = []

    for index, record in enumerate(raw):
        if not isinstance(record, dict):
            rejected_records.append({"record_index": index, "reasons": ["RECORD_NOT_OBJECT"]})
            continue
        reasons: list[str] = []
        edge_id = str(record.get("edge_id", "")).strip()
        edge = edge_by_id.get(edge_id)
        phase = str(record.get("phase", "")).strip().upper()
        if edge is None:
            reasons.append("UNKNOWN_EDGE_ID")
        if record.get("evidence_source") != "REVIT_API_GEOMETRY_AUDIT":
            reasons.append("EVIDENCE_SOURCE_NOT_REVIT_GEOMETRY_AUDIT")
        if record.get("representation_role") != "CONDUCTOR_3D_SOLID":
            reasons.append("REPRESENTATION_NOT_CONDUCTOR_3D_SOLID")
        if edge and phase not in expected_phases.get(edge_id, set()):
            reasons.append("PHASE_NOT_EXPECTED_FOR_EDGE")
        if edge and expected_phases.get(edge_id) != {"A", "B", "C"}:
            reasons.append("EDGE_HAS_NO_COMPLETE_THREE_PHASE_ROUTE_SPEC")

        element_id = str(record.get("element_id", "")).strip()
        unique_id = str(record.get("unique_id", "")).strip()
        mark = str(record.get("mark", "")).strip()
        id_rows = rows_by_element_id.get(element_id, [])
        uid_rows = rows_by_unique_id.get(unique_id, [])
        model_row = id_rows[0] if len(id_rows) == 1 and len(uid_rows) == 1 and id_rows[0] == uid_rows[0] else None
        if not element_id or not unique_id or not mark or model_row is None:
            reasons.append("CONDUCTOR_MODEL_IDENTITY_NOT_UNIQUE_OR_MISSING")
        elif record_identity_counts[(element_id, unique_id)] != 1:
            reasons.append("CONDUCTOR_IDENTITY_REUSED_IN_AUDIT_RECORDS")
        elif model_row.get("marca", "").strip() != mark:
            reasons.append("CONDUCTOR_MARK_DOES_NOT_MATCH_MODEL_INSTANCE")
        elif model_row.get("model_geometry", "").strip() != "CONDUCTOR_GEOMETRY_3D":
            reasons.append("MODEL_INSTANCE_NOT_TAGGED_AS_CONDUCTOR_GEOMETRY_3D")
        if model_row:
            category = str(model_row.get("categoria_revit", "")).casefold()
            if any(token in category for token in _NON_CONDUCTOR_CATEGORY_TOKENS):
                reasons.append("INFRASTRUCTURE_OR_2D_CATEGORY_CANNOT_CONFIRM_CONDUCTOR")
        if not mark.startswith("COND-"):
            reasons.append("CONDUCTOR_MARK_PREFIX_MISSING")
        route_prefix = next((
            prefix for prefix in (edge.geometry_prefixes if edge else ())
            if (match := _PHASE_PREFIX.search(prefix)) and match.group(1) == phase
        ), None)
        expected_mark_prefix = "COND-" + re.sub(r"-(?:SEG-|ROUTE$)", "-", route_prefix) if route_prefix else ""
        if not expected_mark_prefix or not mark.startswith(expected_mark_prefix):
            reasons.append("CONDUCTOR_MARK_DOES_NOT_MATCH_EDGE_PHASE_ROUTE")
        volume = _finite_float(record.get("solid_volume_m3"))
        if volume is None or volume <= 0:
            reasons.append("CONDUCTOR_SOLID_VOLUME_NOT_POSITIVE")

        terminal_data: list[tuple[dict[str, str] | None, str]] = []
        for end in ("from", "to"):
            terminal_id = str(record.get(f"{end}_terminal_element_id", "")).strip()
            terminal_uid = str(record.get(f"{end}_terminal_unique_id", "")).strip()
            terminal_mark = str(record.get(f"{end}_terminal_mark", "")).strip()
            terminal_id_rows = rows_by_element_id.get(terminal_id, [])
            terminal_uid_rows = rows_by_unique_id.get(terminal_uid, [])
            terminal_row = (terminal_id_rows[0] if len(terminal_id_rows) == 1
                            and len(terminal_uid_rows) == 1 and terminal_id_rows[0] == terminal_uid_rows[0] else None)
            if not terminal_id or not terminal_uid or not terminal_mark or terminal_row is None:
                reasons.append(f"{end.upper()}_TERMINAL_IDENTITY_NOT_UNIQUE_OR_MISSING")
            elif terminal_row.get("marca", "").strip() != terminal_mark:
                reasons.append(f"{end.upper()}_TERMINAL_MARK_DOES_NOT_MATCH_MODEL_INSTANCE")
            terminal_data.append((terminal_row, terminal_mark))

        tolerance = _finite_float(record.get("endpoint_tolerance_mm"))
        gap_from = _finite_float(record.get("from_endpoint_gap_mm"))
        gap_to = _finite_float(record.get("to_endpoint_gap_mm"))
        if tolerance is None or tolerance <= 0 or gap_from is None or gap_to is None or gap_from < 0 or gap_to < 0:
            reasons.append("ENDPOINT_GAP_OR_TOLERANCE_INVALID")
        elif gap_from > tolerance or gap_to > tolerance:
            reasons.append("CONDUCTOR_ENDPOINTS_OUTSIDE_DECLARED_TOLERANCE")
        if not str(record.get("tolerance_basis", "")).strip():
            reasons.append("ENDPOINT_TOLERANCE_BASIS_MISSING")

        if edge and len(terminal_data) == 2 and all(item[0] is not None for item in terminal_data):
            from_node = node_by_id[edge.from_node]
            to_node = node_by_id[edge.to_node]
            from_mark, to_mark = terminal_data[0][1], terminal_data[1][1]

            def phase_matches(node: NodeSpec, mark_value: str) -> bool:
                if len(node.marks) == 3 and phase in "ABC":
                    expected_mark = node.marks["ABC".index(phase)]
                    return _matches(mark_value, (expected_mark,))
                return bool(node.marks and _matches(mark_value, node.marks))

            forward = phase_matches(from_node, from_mark) and phase_matches(to_node, to_mark)
            reverse = phase_matches(from_node, to_mark) and phase_matches(to_node, from_mark)
            if not (forward or reverse):
                reasons.append("TERMINALS_DO_NOT_MATCH_EDGE_ENDPOINT_NODES")

        if reasons:
            rejected_records.append({"record_index": index, "edge_id": edge_id, "phase": phase, "reasons": sorted(set(reasons))})
        else:
            valid_by_edge_phase[edge_id][phase].append({
                "element_id": element_id,
                "unique_id": unique_id,
                "mark": mark,
                "phase": phase,
                "category": model_row.get("categoria_revit", ""),
                "solid_volume_m3": volume,
                "from_terminal_mark": terminal_data[0][1],
                "to_terminal_mark": terminal_data[1][1],
                "from_endpoint_gap_mm": gap_from,
                "to_endpoint_gap_mm": gap_to,
                "endpoint_tolerance_mm": tolerance,
                "tolerance_basis": str(record.get("tolerance_basis", "")).strip(),
            })

    edge_evidence: dict[str, dict[str, Any]] = {}
    for edge in EDGES:
        expected = expected_phases.get(edge.edge_id, set())
        phases = valid_by_edge_phase.get(edge.edge_id, {})
        reasons: list[str] = []
        if expected != {"A", "B", "C"}:
            reasons.append("NO_COMPLETE_THREE_PHASE_ROUTE_SPEC")
        missing_phases = sorted(expected - set(phases))
        duplicate_phases = sorted(phase for phase, rows in phases.items() if len(rows) != 1)
        if missing_phases:
            reasons.append("MISSING_PHASES:" + ",".join(missing_phases))
        if duplicate_phases:
            reasons.append("AMBIGUOUS_PHASE_RECORDS:" + ",".join(duplicate_phases))
        rejected_for_edge = [item for item in rejected_records if item.get("edge_id") == edge.edge_id]
        if rejected_for_edge:
            reasons.append("REJECTED_SOURCE_RECORDS_PRESENT")
        complete = not reasons and all(len(phases.get(phase, [])) == 1 for phase in ("A", "B", "C"))
        edge_evidence[edge.edge_id] = {
            "confirmed": complete,
            "expected_phases": sorted(expected),
            "confirmed_phases": sorted(phase for phase, rows in phases.items() if len(rows) == 1),
            "elements": [row for phase in sorted(phases) for row in phases[phase]],
            "rejection_reasons": reasons,
        }

    return edge_evidence, {
        "audit_status": "GEOMETRIC_ROUTE_CANDIDATES_ONLY" if route_candidates_fallback else (
            "NO_3D_CONDUCTOR_RECORDS" if not raw else "EXTRACTED"
        ),
        "audit_fields_available": not route_candidates_fallback,
        "route_candidates_fallback_used": route_candidates_fallback,
        "records_observed": len(raw),
        "records_accepted": sum(len(rows) for phases in valid_by_edge_phase.values() for rows in phases.values()),
        "rejected_records": rejected_records,
    }


def _geometric_phase_route_audit(
    model_inventory: list[dict[str, str]], audit: dict[str, Any]
) -> tuple[dict[str, dict[str, Any]], dict[str, Any]]:
    """Verify route envelopes from Revit without calling them conductors or terminals.

    A route record is accepted only when its Revit DirectShape identity is unique,
    the actual solid count and bounds cover the declared path, and both endpoints
    fall within the measured family bounding boxes of the specifically identified
    equipment. This is geometric evidence only and never an electrical connection.
    """
    if "geometric_phase_route_details" not in audit:
        return {}, {
            "audit_status": "NOT_EXTRACTED",
            "audit_fields_available": False,
            "records_observed": 0,
            "records_accepted": 0,
            "rejected_records": [],
        }
    raw = audit.get("geometric_phase_route_details")
    if not isinstance(raw, list):
        return {}, {
            "audit_status": "MALFORMED_AUDIT_DATA",
            "audit_fields_available": False,
            "records_observed": 0,
            "records_accepted": 0,
            "rejected_records": [],
        }

    rows_by_id: dict[str, list[dict[str, str]]] = defaultdict(list)
    rows_by_uid: dict[str, list[dict[str, str]]] = defaultdict(list)
    rows_by_mark: dict[str, list[dict[str, str]]] = defaultdict(list)
    for row in model_inventory:
        element_id = str(row.get("element_id", "")).strip()
        unique_id = str(row.get("unique_id", "")).strip()
        mark = str(row.get("marca", "")).strip()
        if element_id:
            rows_by_id[element_id].append(row)
        if unique_id:
            rows_by_uid[unique_id].append(row)
        if mark:
            rows_by_mark[mark].append(row)

    edge_by_id = {edge.edge_id: edge for edge in EDGES}
    route_identity_counts = Counter(
        (str(record.get("element_id", "")).strip(), str(record.get("unique_id", "")).strip())
        for record in raw
        if isinstance(record, dict)
    )
    route_mark_counts = Counter(
        str(record.get("mark", "")).strip()
        for record in raw
        if isinstance(record, dict)
    )
    valid_by_edge_phase: dict[str, dict[str, list[dict[str, Any]]]] = defaultdict(lambda: defaultdict(list))
    rejected: list[dict[str, Any]] = []

    for index, record in enumerate(raw):
        if not isinstance(record, dict):
            rejected.append({"record_index": index, "reasons": ["RECORD_NOT_OBJECT"]})
            continue
        reasons: list[str] = []
        edge_id = str(record.get("edge_id", "")).strip()
        phase = str(record.get("phase", "")).strip().upper()
        edge = edge_by_id.get(edge_id)
        if edge is None or edge_id not in _GEOMETRIC_ROUTE_ENDPOINTS:
            reasons.append("UNKNOWN_GEOMETRIC_ROUTE_EDGE")
        if record.get("evidence_source") != "REVIT_API_GEOMETRY_AUDIT":
            reasons.append("EVIDENCE_SOURCE_NOT_REVIT_GEOMETRY_AUDIT")
        if record.get("representation_role") != "GEOMETRIC_PHASE_ROUTE_ONLY":
            reasons.append("REPRESENTATION_NOT_GEOMETRIC_PHASE_ROUTE")
        if phase not in {"A", "B", "C"}:
            reasons.append("PHASE_LABEL_INVALID")
        if record.get("phase_label_basis") != "GEOMETRIC_LANE_LABEL_ONLY_PHASE_ASSIGNMENT_UNVERIFIED":
            reasons.append("PHASE_LABEL_BASIS_NOT_DECLARED_AS_GEOMETRIC_ONLY")

        element_id = str(record.get("element_id", "")).strip()
        unique_id = str(record.get("unique_id", "")).strip()
        mark = str(record.get("mark", "")).strip()
        id_rows = rows_by_id.get(element_id, [])
        uid_rows = rows_by_uid.get(unique_id, [])
        model_row = id_rows[0] if len(id_rows) == 1 and len(uid_rows) == 1 and id_rows[0] is uid_rows[0] else None
        if not element_id or not unique_id or not mark or model_row is None:
            reasons.append("ROUTE_MODEL_IDENTITY_NOT_UNIQUE_OR_MISSING")
        elif route_identity_counts[(element_id, unique_id)] != 1 or route_mark_counts[mark] != 1:
            reasons.append("ROUTE_IDENTITY_OR_MARK_REUSED")
        elif model_row.get("marca", "").strip() != mark:
            reasons.append("ROUTE_MARK_DOES_NOT_MATCH_MODEL_ROW")
        if record.get("revit_element_class") != "DirectShape":
            reasons.append("ROUTE_IS_NOT_A_REVIT_DIRECTSHAPE")
        if record.get("audit_record_status") != "API_GEOMETRY_EXTRACTED":
            reasons.append("ROUTE_API_EXTRACTION_NOT_COMPLETE")

        expected_prefix = next((
            prefix for prefix in (edge.geometry_prefixes if edge else ())
            if (phase_match := _PHASE_PREFIX.search(prefix)) and phase_match.group(1) == phase
        ), "")
        if not expected_prefix or mark != expected_prefix:
            reasons.append("ROUTE_MARK_DOES_NOT_MATCH_EDGE_PHASE")

        endpoint_spec = _GEOMETRIC_ROUTE_ENDPOINTS.get(edge_id)
        if endpoint_spec and phase in "ABC":
            index_1_based = "ABC".index(phase) + 1
            expected_from = endpoint_spec[0].format(index=index_1_based)
            expected_to = endpoint_spec[1].format(index=index_1_based)
        else:
            expected_from = expected_to = ""
        from_mark = str(record.get("from_equipment_mark", "")).strip()
        to_mark = str(record.get("to_equipment_mark", "")).strip()
        if not expected_from or from_mark != expected_from:
            reasons.append("FROM_EQUIPMENT_MARK_DOES_NOT_MATCH_ROUTE_SPEC")
        if not expected_to or to_mark != expected_to:
            reasons.append("TO_EQUIPMENT_MARK_DOES_NOT_MATCH_ROUTE_SPEC")
        for end, mark_value in (("from", from_mark), ("to", to_mark)):
            matches = rows_by_mark.get(mark_value, [])
            reported_id = str(record.get(f"{end}_equipment_element_id", "")).strip()
            reported_uid = str(record.get(f"{end}_equipment_unique_id", "")).strip()
            if len(matches) != 1:
                reasons.append(f"{end.upper()}_EQUIPMENT_MARK_NOT_UNIQUE_IN_MODEL")
            elif matches[0].get("element_id", "").strip() != reported_id or matches[0].get("unique_id", "").strip() != reported_uid:
                reasons.append(f"{end.upper()}_EQUIPMENT_IDENTITY_DOES_NOT_MATCH_MODEL")

        vertices = record.get("route_vertices_m")
        if not isinstance(vertices, list) or len(vertices) < 2:
            reasons.append("ROUTE_VERTEX_LIST_INVALID")
            vertices = []
        parsed_vertices: list[tuple[float, float, float]] = []
        for vertex in vertices:
            if isinstance(vertex, dict):
                values = tuple(_finite_float(vertex.get(axis)) for axis in ("x", "y", "z"))
            elif isinstance(vertex, (list, tuple)) and len(vertex) == 3:
                values = tuple(_finite_float(value) for value in vertex)
            else:
                values = (None, None, None)
            if any(value is None for value in values):
                reasons.append("ROUTE_VERTEX_COORDINATE_INVALID")
                break
            parsed_vertices.append((float(values[0]), float(values[1]), float(values[2])))
        expected_solid_count = _as_int(record.get("expected_solid_count"))
        solid_count = _as_int(record.get("solid_count"))
        volume = _finite_float(record.get("solid_volume_m3"))
        if expected_solid_count != max(0, len(parsed_vertices) - 1) or solid_count != expected_solid_count or solid_count < 1:
            reasons.append("ROUTE_SOLID_COUNT_DOES_NOT_MATCH_PATH_SEGMENTS")
        if volume is None or volume <= 0 or record.get("geometry_exists") is not True:
            reasons.append("ROUTE_SOLID_GEOMETRY_NOT_CONFIRMED")

        bounds = record.get("model_bounds")
        bounds_contains_path = False
        if isinstance(bounds, dict) and parsed_vertices:
            min_bounds, max_bounds = bounds.get("min_m"), bounds.get("max_m")
            if isinstance(min_bounds, dict) and isinstance(max_bounds, dict):
                axis_bounds = [(_finite_float(min_bounds.get(axis)), _finite_float(max_bounds.get(axis))) for axis in ("x", "y", "z")]
                if all(low is not None and high is not None and low <= high for low, high in axis_bounds):
                    bounds_contains_path = all(
                        axis_bounds[axis][0] - 0.001 <= vertex[axis] <= axis_bounds[axis][1] + 0.001
                        for vertex in parsed_vertices for axis in range(3)
                    )
        if not bounds_contains_path:
            reasons.append("ACTUAL_MODEL_BOUNDS_DO_NOT_CONTAIN_ROUTE_VERTICES")

        tolerance = _finite_float(record.get("endpoint_tolerance_mm"))
        gap_from = _finite_float(record.get("from_endpoint_gap_mm"))
        gap_to = _finite_float(record.get("to_endpoint_gap_mm"))
        if tolerance is None or tolerance <= 0 or gap_from is None or gap_to is None or gap_from < 0 or gap_to < 0:
            reasons.append("EQUIPMENT_ENVELOPE_GAP_OR_TOLERANCE_INVALID")
        elif gap_from > tolerance or gap_to > tolerance:
            reasons.append("ROUTE_ENDPOINT_OUTSIDE_EQUIPMENT_ENVELOPE_TOLERANCE")
        if not str(record.get("endpoint_gap_basis", "")).strip() or not str(record.get("tolerance_basis", "")).strip():
            reasons.append("EQUIPMENT_ENVELOPE_GAP_BASIS_MISSING")
        if record.get("electrical_connection_confirmed") is not False:
            reasons.append("ROUTE_MUST_NOT_CLAIM_ELECTRICAL_CONNECTION")
        if record.get("terminal_port_identified") is not False:
            reasons.append("ROUTE_MUST_NOT_CLAIM_TERMINAL_PORT_IDENTITY")
        if record.get("native_electrical_system_confirmed") is not False:
            reasons.append("ROUTE_MUST_NOT_CLAIM_NATIVE_ELECTRICAL_SYSTEM")

        if reasons:
            rejected.append({"record_index": index, "edge_id": edge_id, "phase": phase, "mark": mark, "reasons": sorted(set(reasons))})
        else:
            valid_by_edge_phase[edge_id][phase].append({
                "element_id": element_id,
                "unique_id": unique_id,
                "mark": mark,
                "phase_label": phase,
                "phase_label_basis": record.get("phase_label_basis"),
                "from_equipment_mark": from_mark,
                "to_equipment_mark": to_mark,
                "from_endpoint_gap_mm": gap_from,
                "to_endpoint_gap_mm": gap_to,
                "endpoint_tolerance_mm": tolerance,
                "route_vertex_count": len(parsed_vertices),
                "solid_count": solid_count,
                "solid_volume_m3": volume,
                "electrical_connection_confirmed": False,
                "terminal_port_identified": False,
            })

    edge_evidence: dict[str, dict[str, Any]] = {}
    for edge_id in _GEOMETRIC_ROUTE_ENDPOINTS:
        phases = valid_by_edge_phase.get(edge_id, {})
        missing = sorted({"A", "B", "C"} - set(phases))
        duplicate = sorted(phase for phase, rows in phases.items() if len(rows) != 1)
        rejected_for_edge = [row for row in rejected if row.get("edge_id") == edge_id]
        reasons = (["MISSING_PHASES:" + ",".join(missing)] if missing else [])
        if duplicate:
            reasons.append("AMBIGUOUS_PHASES:" + ",".join(duplicate))
        if rejected_for_edge:
            reasons.append("REJECTED_SOURCE_RECORDS_PRESENT")
        complete = not reasons and all(len(phases.get(phase, [])) == 1 for phase in "ABC")
        edge_evidence[edge_id] = {
            "verified": complete,
            "expected_phases": ["A", "B", "C"],
            "verified_phase_labels": sorted(phase for phase, rows in phases.items() if len(rows) == 1),
            "routes": [row for phase in sorted(phases) for row in phases[phase]],
            "rejection_reasons": reasons,
        }
    accepted = sum(len(rows) for phases in valid_by_edge_phase.values() for rows in phases.values())
    status = "NO_PHASE_ROUTE_RECORDS" if not raw else ("API_GEOMETRY_VERIFIED" if accepted == len(raw) else "PARTIAL_OR_REJECTED")
    return edge_evidence, {
        "audit_status": status,
        "audit_fields_available": True,
        "records_observed": len(raw),
        "records_accepted": accepted,
        "records_rejected": len(rejected),
        "complete_three_phase_route_shapes": sum(1 for row in edge_evidence.values() if row["verified"]),
        "rejected_records": rejected,
    }


def _finite_float(value: Any) -> float | None:
    try:
        result = float(value)
    except (TypeError, ValueError):
        return None
    return result if math.isfinite(result) else None


def build_graph(elements: list[dict[str, str]], audit: dict[str, Any], revision: str = "R12") -> dict[str, Any]:
    if len({n.node_id for n in NODES}) != len(NODES):
        raise ValueError("Node IDs duplicados na especificação.")
    node_ids = {n.node_id for n in NODES}
    for edge in EDGES:
        if edge.from_node not in node_ids or edge.to_node not in node_ids:
            raise ValueError(f"Aresta {edge.edge_id} referencia nó ausente.")

    model_inventory = _model_identity_inventory(elements, audit)
    native_systems = _native_systems_confirmed(elements, audit)
    physical_conductors, physical_audit = _physical_conductor_geometry_audit(model_inventory, audit)
    geometric_phase_routes, geometric_phase_route_audit = _geometric_phase_route_audit(model_inventory, audit)
    duplicate_marks = _duplicate_values(row.get("marca", "") for row in model_inventory)
    duplicate_element_ids = _duplicate_values(str(row.get("element_id", "")).strip() for row in model_inventory)
    duplicate_unique_ids = _duplicate_values(str(row.get("unique_id", "")).strip() for row in model_inventory)
    rows_by_element_id: dict[str, list[dict[str, str]]] = defaultdict(list)
    rows_by_unique_id: dict[str, list[dict[str, str]]] = defaultdict(list)
    for row in model_inventory:
        element_id = str(row.get("element_id", "")).strip()
        unique_id = str(row.get("unique_id", "")).strip()
        if element_id:
            rows_by_element_id[element_id].append(row)
        if unique_id:
            rows_by_unique_id[unique_id].append(row)

    def has_unique_model_identity(row: dict[str, str]) -> bool:
        element_id = str(row.get("element_id", "")).strip()
        unique_id = str(row.get("unique_id", "")).strip()
        id_rows = rows_by_element_id.get(element_id, [])
        uid_rows = rows_by_unique_id.get(unique_id, [])
        return bool(element_id and unique_id and len(id_rows) == 1 and len(uid_rows) == 1
                    and id_rows[0] is row and uid_rows[0] is row)

    model_elements_without_unique_identity = sorted({
        str(row.get("marca", "")).strip()
        for row in model_inventory
        if str(row.get("marca", "")).strip() and not has_unique_model_identity(row)
    })
    system_ids = [str(row.get("system_id", "")) for row in audit.get("electrical_system_details", []) if isinstance(row, dict)]
    duplicate_system_ids = _duplicate_values(system_ids)
    node_records: list[dict[str, Any]] = []
    for spec in NODES:
        matches = [r for r in elements if _matches(r.get("marca", ""), spec.marks)] if spec.marks else []
        found_patterns = [pattern for pattern in spec.marks if any(_matches(r.get("marca", ""), (pattern,)) for r in elements)]
        ambiguous_patterns = [pattern for pattern in spec.marks if sum(_matches(r.get("marca", ""), (pattern,)) for r in elements) > 1]
        missing_patterns = [pattern for pattern in spec.marks if pattern not in found_patterns]
        if spec.external:
            state = "EXTERNAL_OR_REMOTE_NOT_MODELED"
        elif ambiguous_patterns:
            state = "AMBIGUOUS_MODEL_MARK"
        elif matches and any(not has_unique_model_identity(row) for row in matches):
            state = "MODEL_ELEMENT_IDENTITY_UNVERIFIED"
        elif len(found_patterns) == len(spec.marks) and matches:
            state = "MODEL_ELEMENT_FOUND"
        elif matches:
            state = "PARTIAL_MODEL_ELEMENTS"
        else:
            state = "MISSING_EXPECTED_MODEL_ELEMENT"
        node_records.append({
            **asdict(spec),
            "marks": list(spec.marks),
            "model_state": state,
            "expected_marks_found": found_patterns,
            "expected_marks_missing": missing_patterns,
            "ambiguous_marks": ambiguous_patterns,
            "model_elements": [{
                "element_id": r.get("element_id", ""),
                "unique_id": r.get("unique_id", ""),
                "mark": r.get("marca", ""),
                "category": r.get("categoria_revit", ""),
                "connector_count": _as_int(r.get("mep_connectors")),
                "isconnected_true": _as_int(r.get("isconnected_true")),
                "connector_domains": r.get("dominios", ""),
                "connector_domain_evidence_source": r.get("domain_evidence_source", "EXPLICIT_INPUT_ROWS"),
                "identity_status": "ELEMENT_IDENTITY_CONFIRMED" if has_unique_model_identity(r) else "ELEMENT_IDENTITY_UNVERIFIED",
            } for r in matches],
        })

    edge_records: list[dict[str, Any]] = []
    for spec in EDGES:
        model_matches = [r for r in elements if _matches(r.get("marca", ""), spec.equipment_marks)] if spec.equipment_marks else []
        ambiguous_equipment_marks = sorted({r.get("marca", "") for r in model_matches if r.get("marca", "") in duplicate_marks})
        unverified_equipment_marks = sorted({
            str(r.get("marca", "")).strip() for r in model_matches
            if not has_unique_model_identity(r)
        })
        geometry_matches = [r for r in model_inventory if any(_matches_geometry_prefix(r.get("marca", ""), prefix) for prefix in spec.geometry_prefixes)]
        classification = spec.evidence_class
        native_confirmed = False
        system_detail = native_systems.get(spec.system_id) if spec.system_id else None
        conductor_detail = physical_conductors.get(spec.edge_id, {
            "confirmed": False,
            "expected_phases": [],
            "confirmed_phases": [],
            "elements": [],
            "rejection_reasons": ["PHYSICAL_CONDUCTOR_DETAILS_NOT_EXTRACTED"],
        })
        phase_route_detail = geometric_phase_routes.get(spec.edge_id, {
            "verified": False,
            "expected_phases": ["A", "B", "C"] if spec.edge_id in _GEOMETRIC_ROUTE_ENDPOINTS else [],
            "verified_phase_labels": [],
            "routes": [],
            "rejection_reasons": ["GEOMETRIC_PHASE_ROUTE_DETAILS_NOT_EXTRACTED"],
        })
        if spec.system_id:
            native_confirmed = system_detail is not None and not ambiguous_equipment_marks and not unverified_equipment_marks
            if not native_confirmed:
                classification = "NATIVE_SYSTEM_NOT_CONFIRMED"
        edge_records.append({
            **asdict(spec),
            "evidence_class": classification,
            "equipment_marks": list(spec.equipment_marks),
            "ambiguous_equipment_marks": ambiguous_equipment_marks,
            "unverified_equipment_identity_marks": unverified_equipment_marks,
            "geometry_prefixes": list(spec.geometry_prefixes),
            "model_elements": [{
                "element_id": r.get("element_id", ""),
                "unique_id": r.get("unique_id", ""),
                "mark": r.get("marca", ""),
                "model_geometry": r.get("model_geometry", ""),
                "identity_status": "ELEMENT_IDENTITY_CONFIRMED" if has_unique_model_identity(r) else "ELEMENT_IDENTITY_UNVERIFIED",
            } for r in model_matches],
            "geometry_elements": [{
                "element_id": r.get("element_id", ""),
                "unique_id": r.get("unique_id", ""),
                "mark": r.get("marca", ""),
            } for r in geometry_matches],
            "geometry_present": not spec.geometry_prefixes or bool(geometry_matches),
            "native_electrical_system_confirmed": native_confirmed,
            "revit_load_name_confirmed": bool(system_detail.get("load_name_confirmed", False)) if system_detail else False,
            "revit_load_calculation_confirmed": bool(system_detail.get("load_calculation_confirmed", False)) if system_detail else False,
            "wire_2d_view_specific_confirmed": bool(system_detail.get("wire_2d_view_specific_confirmed", False)) if system_detail else False,
            "physical_conductor_confirmed": conductor_detail["confirmed"],
            "physical_conductor_evidence": conductor_detail,
            "geometric_phase_route_verified": phase_route_detail["verified"],
            "geometric_phase_route_evidence": phase_route_detail,
            "native_system_id": spec.system_id,
            "status": "CONFIRMED_SYSTEM_MEMBERSHIP_ONLY" if native_confirmed else (
                "PHYSICAL_CONDUCTOR_GEOMETRY_CONFIRMED_NO_NATIVE_CIRCUIT" if conductor_detail["confirmed"] else (
                    "GEOMETRIC_PHASE_PATH_VERIFIED_TO_EQUIPMENT_ENVELOPES_NO_TERMINALS" if phase_route_detail["verified"] else (
                        "GEOMETRY_FOUND_NO_CONDUCTOR" if geometry_matches else "DOCUMENTED_RELATION_NOT_ELECTRICALLY_CONNECTED"
                    )
                )
            ),
        })

    infrastructure: list[dict[str, Any]] = []
    cable_tray_rows = [
        row for row in model_inventory
        if row.get("familia_ou_classe", "").casefold() == "cabletray"
        or any(token in row.get("categoria_revit", "").casefold() for token in ("cable tray", "cabletray", "bandeja"))
    ]
    for mark, label in INFRASTRUCTURE_SPECS:
        matches = [r for r in cable_tray_rows if r.get("marca", "").startswith(mark + "-") or r.get("marca", "") == mark]
        infrastructure.append({
            "mark_prefix": mark,
            "label": label,
            "state": "NATIVE_CABLETRAY_INFRASTRUCTURE_ONLY" if matches else "NOT_FOUND",
            "electrical_connection": False,
            "elements": [{"element_id": x.get("element_id", ""), "unique_id": x.get("unique_id", ""), "mark": x.get("marca", ""),
                          "connector_count": _as_int(x.get("mep_connectors")),
                          "connected_connector_count": _as_int(x.get("isconnected_true")),
                          "comments": x.get("comentarios", "")} for x in matches],
        })

    ct_nodes = [n for n in node_records if n["node_id"].endswith("-CT") or n["node_id"] == "GIS-BC01-CT"]
    ct_found = sum(1 for n in ct_nodes if n["model_state"] == "MODEL_ELEMENT_FOUND")
    physical_edges = sum(1 for e in edge_records if e["physical_conductor_confirmed"])
    physical_conductor_solids = sum(
        len(edge["physical_conductor_evidence"]["elements"])
        for edge in edge_records
        if edge["physical_conductor_confirmed"]
    )
    geometric_phase_route_shapes = sum(
        len(edge["geometric_phase_route_evidence"]["routes"])
        for edge in edge_records
    )
    native_edges = sum(1 for e in edge_records if e["native_electrical_system_confirmed"])
    native_loads = sum(1 for e in edge_records if e["native_electrical_system_confirmed"] and e["revit_load_calculation_confirmed"])
    view_specific_wires = sum(1 for e in edge_records if e["native_electrical_system_confirmed"] and e["wire_2d_view_specific_confirmed"])
    missing_nodes = [n["node_id"] for n in node_records if n["model_state"] == "MISSING_EXPECTED_MODEL_ELEMENT"]
    incomplete_nodes = [n["node_id"] for n in node_records if n["model_state"] in {
        "PARTIAL_MODEL_ELEMENTS", "AMBIGUOUS_MODEL_MARK", "MODEL_ELEMENT_IDENTITY_UNVERIFIED"
    }]
    missing_routes = [e["edge_id"] for e in edge_records if e["geometry_prefixes"] and not e["geometry_present"]]
    edge_classes: dict[str, int] = {}
    for edge in edge_records:
        edge_classes[edge["evidence_class"]] = edge_classes.get(edge["evidence_class"], 0) + 1
    return {
        "project": "19 - Subestação GIS 138/13,8 kV",
        "revision": revision,
        "source": "Revit 2027 API audit CSV/JSON plus explicit topology specification",
        "validation_class": "MODEL_TOPOLOGY_CORRESPONDENCE_AUDIT_NOT_PROTECTION_STUDY",
        "summary": {
            "nodes_expected": len(node_records),
            "edges_expected": len(edge_records),
            "native_electrical_system_relations_confirmed": native_edges,
            "native_electrical_system_load_calculations_confirmed": native_loads,
            "view_specific_wires_confirmed": view_specific_wires,
            "physical_power_conductors_confirmed": physical_conductor_solids,
            "physical_conductor_solids_confirmed": physical_conductor_solids,
            "complete_three_phase_geometry_routes_confirmed": physical_edges,
            "physical_conductor_audit_status": physical_audit["audit_status"],
            "physical_conductor_records_observed": physical_audit["records_observed"],
            "physical_conductor_records_accepted": physical_audit["records_accepted"],
            "physical_conductor_records_rejected": len(physical_audit["rejected_records"]),
            "geometric_phase_route_shapes_verified": geometric_phase_route_shapes,
            "complete_three_phase_geometric_route_edges_verified": geometric_phase_route_audit.get("complete_three_phase_route_shapes", 0),
            "geometric_phase_route_audit_status": geometric_phase_route_audit["audit_status"],
            "geometric_phase_route_records_observed": geometric_phase_route_audit["records_observed"],
            "geometric_phase_route_records_accepted": geometric_phase_route_audit["records_accepted"],
            "geometric_phase_route_records_rejected": geometric_phase_route_audit.get("records_rejected", 0),
            "current_transformer_placeholders_expected": len(ct_nodes),
            "current_transformer_placeholders_found": ct_found,
            "missing_expected_model_nodes": missing_nodes,
            "incomplete_or_ambiguous_model_nodes": incomplete_nodes,
            "duplicate_model_marks": sorted(duplicate_marks),
            "duplicate_element_ids": sorted(duplicate_element_ids),
            "duplicate_unique_ids": sorted(duplicate_unique_ids),
            "model_elements_without_unique_identity": model_elements_without_unique_identity,
            "duplicate_audited_system_ids": sorted(duplicate_system_ids),
            "missing_geometric_route_edges": missing_routes,
            "native_cable_tray_segments": len(cable_tray_rows),
            "native_cable_tray_connectors": sum(_as_int(row.get("mep_connectors")) for row in cable_tray_rows),
            "native_cable_tray_connected_connectors": sum(_as_int(row.get("isconnected_true")) for row in cable_tray_rows),
            "edges_by_evidence_class": edge_classes,
        },
        "nodes": node_records,
        "edges": edge_records,
        "infrastructure": infrastructure,
        "auxiliary_native_systems": [native_systems[key] for key in sorted(native_systems)],
        "physical_conductor_audit": physical_audit,
        "geometric_phase_route_audit": geometric_phase_route_audit,
        "limitations": [
            "A topologia primária GIS/13,8 kV está no E-01 e no grafo; condutor geométrico e ElectricalSystem nativo são evidências separadas. Esta auditoria não comprova continuidade elétrica, contatos internos ou PowerCircuits para a rede primária.",
            "Envelopes DirectShape e leitos CableTray não são condutores; a continuidade entre conectores de leito comprova apenas juntas de infraestrutura.",
            "Estados de disjuntores e seccionadoras são premissas; não há simulação de manobra nem lógica de intertravamento.",
            "Cada família GIS_CT é um placeholder genérico; não comprova conjunto trifásico, relação, núcleo, burden ou zona de proteção.",
            "Sistemas auxiliares QAC-01→UPS/painéis só são contados quando sistema, painel, terminais e conectores são confirmados por ElementId/UniqueId; não provam cabo 3D. O Wire 2D existente é específico da vista E-06.",
            "As conexões com as duas redes externas e os dez destinos remotos dos alimentadores ficam fora do limite modelado.",
        ],
    }


def _as_int(value: Any) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return 0


def write_graph(audit_dir: Path, out_dir: Path | None = None, revision: str = "R12") -> dict[str, Any]:
    base = out_dir or audit_dir
    suffix = revision.lower()
    element_path = audit_dir / f"conectividade-equipamentos-{suffix}.csv"
    audit_path = audit_dir / f"auditoria-modelo-{suffix}.json"
    elements = _load_rows(element_path)
    audit = json.loads(audit_path.read_text(encoding="utf-8"))
    graph = build_graph(elements, audit, revision)
    base.mkdir(parents=True, exist_ok=True)
    (base / f"grafo-conectividade-{suffix}.json").write_text(json.dumps(graph, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    with (base / f"relacoes-rede-{suffix}.csv").open("w", newline="", encoding="utf-8-sig") as stream:
        columns = ("edge_id", "from_node", "to_node", "voltage_kv", "state", "evidence_class", "diagram_ref", "status",
                   "native_system_id", "native_electrical_system_confirmed", "geometric_phase_route_verified", "geometric_phase_route_phases", "geometric_phase_route_marks",
                   "geometric_phase_route_rejection_reasons", "physical_conductor_confirmed", "physical_conductor_phases",
                   "physical_conductor_marks", "physical_conductor_rejection_reasons", "geometry_present", "equipment_marks",
                   "ambiguous_equipment_marks", "geometry_prefixes", "limitation")
        writer = csv.DictWriter(stream, fieldnames=columns, delimiter=";", extrasaction="ignore")
        writer.writeheader()
        for row in graph["edges"]:
            writer.writerow({**row, "equipment_marks": ",".join(row["equipment_marks"]),
                             "ambiguous_equipment_marks": ",".join(row["ambiguous_equipment_marks"]),
                             "geometry_prefixes": ",".join(row["geometry_prefixes"]),
                             "geometric_phase_route_phases": ",".join(row["geometric_phase_route_evidence"]["verified_phase_labels"]),
                             "geometric_phase_route_marks": ",".join(item["mark"] for item in row["geometric_phase_route_evidence"]["routes"]),
                             "geometric_phase_route_rejection_reasons": ",".join(row["geometric_phase_route_evidence"]["rejection_reasons"]),
                             "physical_conductor_phases": ",".join(row["physical_conductor_evidence"]["confirmed_phases"]),
                             "physical_conductor_marks": ",".join(item["mark"] for item in row["physical_conductor_evidence"]["elements"]),
                             "physical_conductor_rejection_reasons": ",".join(row["physical_conductor_evidence"]["rejection_reasons"])})
    lines = [
        f"# Auditoria de conectividade por equipamento — {revision}", "",
        "Fonte: extração do RVT pelo add-in Revit 2027 e grafo topológico independente. ElementId e UniqueId são extraídos do modelo; IDs sem correspondência são listados como pendência.", "",
        "## Resultado", "",
        f"- Nós esperados: {graph['summary']['nodes_expected']}; relações: {graph['summary']['edges_expected']}.",
        "- Classes de relação: " + "; ".join(f"`{name}`: {count}" for name, count in sorted(graph["summary"]["edges_by_evidence_class"].items())) + ".",
        f"- ElectricalSystem nativo confirmado: {graph['summary']['native_electrical_system_relations_confirmed']} relações auxiliares auditadas.",
        f"- Cálculo de carga confirmado nessas relações: {graph['summary']['native_electrical_system_load_calculations_confirmed']}; Wire 2D view-specific confirmado: {graph['summary']['view_specific_wires_confirmed']}.",
        f"- Geometrias 3D de condutor verificadas: {graph['summary']['physical_conductor_solids_confirmed']}; estes dados não confirmam circuito elétrico.",
        f"- Rotas geométricas por faixa A/B/C verificadas contra envelopes de equipamentos: {graph['summary']['geometric_phase_route_shapes_verified']}; grupos trifásicos completos: {graph['summary']['complete_three_phase_geometric_route_edges_verified']}. A atribuição da fase elétrica não foi verificada.",
        f"- Auditoria de rotas de fase: {graph['summary']['geometric_phase_route_audit_status']}; elementos observados/aceitos/rejeitados: {graph['summary']['geometric_phase_route_records_observed']}/{graph['summary']['geometric_phase_route_records_accepted']}/{graph['summary']['geometric_phase_route_records_rejected']}.",
        f"- Auditoria geométrica de condutores 3D: {graph['summary']['physical_conductor_audit_status']}; registros observados/aceitos/rejeitados: {graph['summary']['physical_conductor_records_observed']}/{graph['summary']['physical_conductor_records_accepted']}/{graph['summary']['physical_conductor_records_rejected']}.",
        f"- Placeholders de TC: {graph['summary']['current_transformer_placeholders_found']}/{graph['summary']['current_transformer_placeholders_expected']}.",
        f"- Nós BIM esperados ausentes: {len(graph['summary']['missing_expected_model_nodes'])}; nós parciais/ambíguos: {len(graph['summary']['incomplete_or_ambiguous_model_nodes'])}; rotas geométricas faltantes: {len(graph['summary']['missing_geometric_route_edges'])}.",
        f"- Marcas/IDs/SystemIds duplicados: {len(graph['summary']['duplicate_model_marks'])}/{len(graph['summary']['duplicate_element_ids'])}/{len(graph['summary']['duplicate_unique_ids'])}/{len(graph['summary']['duplicate_audited_system_ids'])}.", "",
        "## Classificação", "",
        "`NATIVE_ELECTRICAL_SYSTEM_LOGICAL` registra somente associação elétrica nativa confirmada pela lista de terminais e por identidades únicas. `physical_conductor_confirmed` exige elementos de condutor 3D identificados e terminais BIM inequívocos; mesmo isso não cria circuito. `geometric_phase_route_verified` confirma somente uma DirectShape por faixa geométrica declarada, seus sólidos, percurso e distância aos envelopes de famílias exatas; A/B/C são rótulos geométricos, sem atribuição da fase elétrica, porta ou terminal. Isso não comprova cabo, barramento ou conexão MEP. `GEOMETRIC_ROUTE_ONLY` indica uma carcaça/percurso sem ligação elétrica. `DIAGRAM_ONLY` indica topologia documentada sem conexão MEP. Leitos CableTray permanecem `INFRASTRUCTURE_ONLY`.", "",
        "## Pendências", "",
    ] + [f"- Nó incompleto ou ambíguo: {item}" for item in graph["summary"]["incomplete_or_ambiguous_model_nodes"]] + [
        f"- Marca BIM duplicada: {item}" for item in graph["summary"]["duplicate_model_marks"]
    ] + [f"- ElementId duplicado: {item}" for item in graph["summary"]["duplicate_element_ids"]] + [
        f"- UniqueId duplicado: {item}" for item in graph["summary"]["duplicate_unique_ids"]
    ] + [f"- SystemId de ElectricalSystem duplicado: {item}" for item in graph["summary"]["duplicate_audited_system_ids"]] + [
        f"- {item}" for item in graph["limitations"]
    ] + ["", f"O CSV `relacoes-rede-{suffix}.csv` contém os endpoints e a classe de evidência por relação. O JSON preserva os vínculos de cada nó/aresta aos ElementIds e UniqueIds extraídos e identifica marcas ausentes, duplicadas ou parciais.", ""]
    (base / f"conectividade-{suffix}.md").write_text("\n".join(lines), encoding="utf-8")
    return graph


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--audit-dir", type=Path, required=True, help="Pasta do audit extraído do Revit")
    parser.add_argument("--output", type=Path, help="Destino dos artefatos; por padrão é --audit-dir")
    parser.add_argument("--revision", default="R12", help="Identificador da revisão (padrão: revisão atual R12)")
    args = parser.parse_args()
    result = write_graph(args.audit_dir, args.output, args.revision)
    print(json.dumps(result["summary"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
