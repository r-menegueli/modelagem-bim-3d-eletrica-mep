"""Evidence-linked topology graph for the GIS substation Revit audit.

This graph keeps switching topology, Revit electrical systems, geometric routes,
and cable-tray infrastructure as separate evidence classes. It never upgrades a
geometric route or connector count into an electrical connection.
"""
from __future__ import annotations

import argparse
import csv
import json
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
    voltage_kv: float
    state: str
    evidence_class: str
    diagram_ref: str
    equipment_marks: tuple[str, ...] = ()
    geometry_prefixes: tuple[str, ...] = ()
    limitation: str = ""


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
            tuple(f"GIS-{phase}-HV-PH-{p}-SEG-" for p in "ABC"),
            "DirectShapes indicam o envelope de rota por fase; não são condutores ou conectores elétricos."))

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
    EdgeSpec("GIS-BC-52-TO-B", "GIS-BC01-52", "GIS-BC01-89B", 138.0, "ASSUMED_OPEN_NORMAL", "DIAGRAM_ONLY", "E-01", ("GIS-BC01-52", "GIS-BC01-89B")),
    EdgeSpec("GIS-BC-B-SELECTOR", "GIS-BC01-89B", "BUS-138-B", 138.0, "ASSUMED_CLOSED_NORMAL", "GEOMETRIC_ROUTE_ONLY", "E-01", ("GIS-BC01-89B",), ("GIS-BC01-89B-STUB",),
             limitation="Carcaça geométrica entre seccionadora e barra B; contato, estado e condutor não são conectados eletricamente no RVT."),
    EdgeSpec("GIS-BC-CT-PROTECTION", "GIS-BC01-CT", "P-BUS", 0.125, "CANDIDATE_FUNCTION", "DIAGRAM_ONLY", "E-01 / E-06", ("GIS-BC01-CT", "P-BUS"),
             limitation="87B/50BF são funções candidatas; esquema, posição, quantidade por fase, relação e núcleo pendentes."),
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
        tuple(f"{phase_prefix}-MV-PH-{p}-SEG-" for p in "ABC"),
        "Envelope geométrico trifásico; cabo/busduct, seção, ampacidade, terminações e conexão MEP não selecionados."))
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
             limitation="Um ElectricalSystem lógico e Wire 2D, se confirmados no audit; não há cabo 3D, fonte a montante nem circuito CC da bateria."),
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


def _load_rows(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream, delimiter=";"))


def _native_aux_confirmed(
    elements: list[dict[str, str]], audit: dict[str, Any]
) -> tuple[bool, dict[str, Any] | None]:
    for system in audit.get("electrical_system_details", []):
        marks = {str(x.get("mark", "")) for x in system.get("terminal_elements", [])}
        panel = system.get("selected_panel_element") or {}
        panel_rows = [row for row in elements if row.get("marca") == "QAC-01"]
        panel_is_model_instance = any(
            row.get("unique_id") == panel.get("unique_id")
            and row.get("element_id") == str(panel.get("element_id", ""))
            for row in panel_rows
        )
        if (
            system.get("system_id") == "AUX-UPS-01"
            and "UPS-01" in marks
            and panel.get("mark") == "QAC-01"
            and panel_is_model_instance
        ):
            return True, system
    return False, None


def build_graph(elements: list[dict[str, str]], audit: dict[str, Any], revision: str = "R10") -> dict[str, Any]:
    if len({n.node_id for n in NODES}) != len(NODES):
        raise ValueError("Node IDs duplicados na especificação.")
    node_ids = {n.node_id for n in NODES}
    for edge in EDGES:
        if edge.from_node not in node_ids or edge.to_node not in node_ids:
            raise ValueError(f"Aresta {edge.edge_id} referencia nó ausente.")

    native_aux, aux_system = _native_aux_confirmed(elements, audit)
    node_records: list[dict[str, Any]] = []
    for spec in NODES:
        matches = [r for r in elements if _matches(r.get("marca", ""), spec.marks)] if spec.marks else []
        if spec.external:
            state = "EXTERNAL_OR_REMOTE_NOT_MODELED"
        elif matches:
            state = "MODEL_ELEMENT_FOUND"
        else:
            state = "MISSING_EXPECTED_MODEL_ELEMENT"
        node_records.append({
            **asdict(spec),
            "marks": list(spec.marks),
            "model_state": state,
            "model_elements": [{
                "element_id": r.get("element_id", ""),
                "unique_id": r.get("unique_id", ""),
                "mark": r.get("marca", ""),
                "category": r.get("categoria_revit", ""),
                "connector_count": _as_int(r.get("mep_connectors")),
                "isconnected_true": _as_int(r.get("isconnected_true")),
            } for r in matches],
        })

    edge_records: list[dict[str, Any]] = []
    for spec in EDGES:
        model_matches = [r for r in elements if _matches(r.get("marca", ""), spec.equipment_marks)] if spec.equipment_marks else []
        geometry_matches = [r for r in elements if any(r.get("marca", "").startswith(prefix) for prefix in spec.geometry_prefixes)]
        classification = spec.evidence_class
        native_confirmed = False
        if spec.edge_id == "AUX-QAC-TO-UPS":
            native_confirmed = native_aux
            if not native_aux:
                classification = "NATIVE_SYSTEM_NOT_CONFIRMED"
        edge_records.append({
            **asdict(spec),
            "evidence_class": classification,
            "equipment_marks": list(spec.equipment_marks),
            "geometry_prefixes": list(spec.geometry_prefixes),
            "model_elements": [{
                "element_id": r.get("element_id", ""),
                "unique_id": r.get("unique_id", ""),
                "mark": r.get("marca", ""),
                "model_geometry": r.get("model_geometry", ""),
            } for r in model_matches],
            "geometry_elements": [{
                "element_id": r.get("element_id", ""),
                "unique_id": r.get("unique_id", ""),
                "mark": r.get("marca", ""),
            } for r in geometry_matches],
            "geometry_present": not spec.geometry_prefixes or bool(geometry_matches),
            "native_electrical_system_confirmed": native_confirmed,
            "physical_conductor_confirmed": False,
            "status": "CONFIRMED_SYSTEM_MEMBERSHIP_ONLY" if native_confirmed else (
                "GEOMETRY_FOUND_NO_CONDUCTOR" if geometry_matches else "DOCUMENTED_RELATION_NOT_ELECTRICALLY_CONNECTED"
            ),
        })

    infrastructure: list[dict[str, Any]] = []
    for mark, label in INFRASTRUCTURE_SPECS:
        matches = [r for r in elements if r.get("marca", "").startswith(mark + "-") or r.get("marca", "") == mark]
        infrastructure.append({
            "mark_prefix": mark,
            "label": label,
            "state": "NATIVE_CABLETRAY_INFRASTRUCTURE_ONLY" if matches else "NOT_FOUND",
            "electrical_connection": False,
            "elements": [{"element_id": x.get("element_id", ""), "unique_id": x.get("unique_id", ""), "mark": x.get("marca", ""),
                          "isconnected_true": _as_int(x.get("isconnected_true"))} for x in matches],
        })

    ct_nodes = [n for n in node_records if n["node_id"].endswith("-CT") or n["node_id"] == "GIS-BC01-CT"]
    ct_found = sum(1 for n in ct_nodes if n["model_state"] == "MODEL_ELEMENT_FOUND")
    physical_edges = sum(1 for e in edge_records if e["physical_conductor_confirmed"])
    native_edges = sum(1 for e in edge_records if e["native_electrical_system_confirmed"])
    missing_nodes = [n["node_id"] for n in node_records if n["model_state"] == "MISSING_EXPECTED_MODEL_ELEMENT"]
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
            "physical_power_conductors_confirmed": physical_edges,
            "current_transformer_placeholders_expected": len(ct_nodes),
            "current_transformer_placeholders_found": ct_found,
            "missing_expected_model_nodes": missing_nodes,
            "missing_geometric_route_edges": missing_routes,
            "edges_by_evidence_class": edge_classes,
        },
        "nodes": node_records,
        "edges": edge_records,
        "infrastructure": infrastructure,
        "auxiliary_native_system": aux_system,
        "limitations": [
            "A topologia primária GIS/13,8 kV está no E-01 e no grafo; não existe uma rede Revit PowerCircuit conectada para potência.",
            "Envelopes DirectShape e leitos CableTray não são condutores; a continuidade entre conectores de leito comprova apenas juntas de infraestrutura.",
            "Estados de disjuntores e seccionadoras são premissas; não há simulação de manobra nem lógica de intertravamento.",
            "Cada família GIS_CT é um placeholder genérico; não comprova conjunto trifásico, relação, núcleo, burden ou zona de proteção.",
            "O ElectricalSystem QAC-01–UPS-01 comprova apenas a associação auditada; o Wire 2D é específico da vista e não há cabo 3D, fonte a montante, saída da UPS ou circuito CC da bateria.",
            "As conexões com as duas redes externas e os dez destinos remotos dos alimentadores ficam fora do limite modelado.",
        ],
    }


def _as_int(value: Any) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return 0


def write_graph(audit_dir: Path, out_dir: Path | None = None, revision: str = "R10") -> dict[str, Any]:
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
                   "native_electrical_system_confirmed", "physical_conductor_confirmed", "geometry_present", "equipment_marks", "geometry_prefixes", "limitation")
        writer = csv.DictWriter(stream, fieldnames=columns, delimiter=";", extrasaction="ignore")
        writer.writeheader()
        for row in graph["edges"]:
            writer.writerow({**row, "equipment_marks": ",".join(row["equipment_marks"]), "geometry_prefixes": ",".join(row["geometry_prefixes"])})
    lines = [
        f"# Auditoria de conectividade por equipamento — {revision}", "",
        "Fonte: extração do RVT pelo add-in Revit 2027 e grafo topológico independente. ElementId e UniqueId são extraídos do modelo; IDs sem correspondência são listados como pendência.", "",
        "## Resultado", "",
        f"- Nós esperados: {graph['summary']['nodes_expected']}; relações: {graph['summary']['edges_expected']}.",
        "- Classes de relação: " + "; ".join(f"`{name}`: {count}" for name, count in sorted(graph["summary"]["edges_by_evidence_class"].items())) + ".",
        f"- ElectricalSystem nativo confirmado: {graph['summary']['native_electrical_system_relations_confirmed']} relação auxiliar.",
        f"- Condutores primários de potência confirmados: {graph['summary']['physical_power_conductors_confirmed']}.",
        f"- Placeholders de TC: {graph['summary']['current_transformer_placeholders_found']}/{graph['summary']['current_transformer_placeholders_expected']}.",
        f"- Nós BIM esperados ausentes: {len(graph['summary']['missing_expected_model_nodes'])}; rotas geométricas faltantes: {len(graph['summary']['missing_geometric_route_edges'])}.", "",
        "## Classificação", "",
        "`NATIVE_ELECTRICAL_SYSTEM_LOGICAL` registra somente associação elétrica nativa confirmada pela lista de terminais. `GEOMETRIC_ROUTE_ONLY` indica carcaça ou percurso geométrico sem condutor. `DIAGRAM_ONLY` indica topologia documentada sem conexão MEP. Leitos CableTray permanecem `INFRASTRUCTURE_ONLY`.", "",
        "## Pendências", "",
    ] + [f"- {item}" for item in graph["limitations"]] + ["", f"O CSV `relacoes-rede-{suffix}.csv` contém os endpoints e a classe de evidência por relação. O JSON preserva os vínculos de cada nó/aresta aos ElementIds e UniqueIds extraídos.", ""]
    (base / f"conectividade-{suffix}.md").write_text("\n".join(lines), encoding="utf-8")
    return graph


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--audit-dir", type=Path, required=True, help="Pasta do audit extraído do Revit")
    parser.add_argument("--output", type=Path, help="Destino dos artefatos; por padrão é --audit-dir")
    parser.add_argument("--revision", default="R10", help="Identificador da revisão (padrão R10 para compatibilidade)")
    args = parser.parse_args()
    result = write_graph(args.audit_dir, args.output, args.revision)
    print(json.dumps(result["summary"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
