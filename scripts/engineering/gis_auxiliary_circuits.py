"""Auditable preliminary schedule for GIS control-panel AC branch circuits.

The calculation derives eleven 150 W loads and PF 0.90 from the preserved R8
auxiliary-load study. 220 V line-to-neutral is an explicit assumption based on
the Revit 380Y/220 V, four-wire distribution setup. The module never infers a
Revit circuit from the calculation: native status is confirmed only by matching
the exported ElectricalSystem, QAC panel identity, terminal instance IDs, and
the system's calculated voltage, active/apparent load, power factor, and current.
Logical system membership is reported separately from physical cable/wire.
"""
from __future__ import annotations

import argparse
import csv
import json
import re
from pathlib import Path
from typing import Any

from .electrical_core import (
    Provenance,
    power_triangle_from_active_power_and_power_factor,
    single_phase_current,
)


PANEL_MARKS: tuple[str, ...] = (
    "P-L01",
    "P-L02",
    "P-BUS",
    "P-TR21-A",
    "P-TR21-B",
    "P-TR22-A",
    "P-TR22-B",
    "P-BC",
    "P-SCADA",
    "TR-01-CTRL",
    "TR-02-CTRL",
)


def _numeric(name: str, value: Any) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{name} must be a JSON number")
    return float(value)


def _count(name: str, value: Any) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
        raise ValueError(f"{name} must be a positive integer")
    return value


def _as_int(value: Any) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return 0


def _close(actual: Any, expected: float, tolerance: float) -> bool:
    return (
        isinstance(actual, (int, float))
        and not isinstance(actual, bool)
        and abs(float(actual) - expected) <= tolerance
    )


def build_schedule(auxiliary_inputs: dict[str, Any], *, revision: str = "R12") -> dict[str, Any]:
    revision = revision.upper()
    if not re.fullmatch(r"R[1-9][0-9]*", revision):
        raise ValueError("revision must use the form R followed by a positive integer")
    ac = auxiliary_inputs["ac_system"]
    load = next(item for item in ac["loads"] if item["id"] == "AUX-AC-06")
    count = _count("AUX-AC-06.quantity", load["quantity"])
    if count != len(PANEL_MARKS):
        raise ValueError(f"AUX-AC-06 quantity {count} does not match {len(PANEL_MARKS)} model panel marks")
    unit_kw = _numeric("AUX-AC-06.unit_power_kw_assumed", load["unit_power_kw_assumed"])
    demand_factor = _numeric("AUX-AC-06.demand_factor_assumed", load["demand_factor_assumed"])
    pf = _numeric("ac_system.power_factor_assumed", ac["power_factor_assumed"])
    voltage_ll = _numeric("ac_system.voltage_v_line_to_line_assumed", ac["voltage_v_line_to_line_assumed"])
    if unit_kw <= 0 or not 0 <= demand_factor <= 1 or voltage_ll <= 0:
        raise ValueError("AUX-AC-06 power, demand factor, and line voltage are outside valid study ranges")
    voltage_ln = 220.0
    provenance = Provenance(
        source_or_assumption=(
            "R8 servicos-auxiliares-r8.json AUX-AC-06: 11 x 0.15 kW, demand factor 1.0; "
            "PF 0.90 from R8 AC system; 220 V phase-neutral assumed from the 380Y/220 V "
            "four-wire system configured by the Revit generator."
        ),
        status="PRELIMINARY_LOAD_AND_VOLTAGE_ASSUMPTIONS_NO_CONDUCTOR_SIZING",
    )
    circuits: list[dict[str, Any]] = []
    for index, panel_mark in enumerate(PANEL_MARKS):
        phase = "ABC"[index % 3]
        p_kw = unit_kw * demand_factor
        triangle = power_triangle_from_active_power_and_power_factor(
            active_power_kw=p_kw,
            power_factor=pf,
            provenance=provenance,
        )
        current = single_phase_current(
            apparent_power_va=triangle.apparent_power_kva * 1000.0,
            voltage_v=voltage_ln,
            provenance=provenance,
        )
        circuits.append({
            "calculation_id": f"AUX-AC-PP-{index + 1:02d}",
            "revit_system_id": f"AUX-AC-{panel_mark}",
            "source_equipment_mark": "QAC-01",
            "terminal_equipment_mark": panel_mark,
            "phase_assigned_for_study": phase,
            "nominal_voltage_v_line_to_neutral_assumed": voltage_ln,
            "active_power_kw_demand_assumed": p_kw,
            "power_factor_assumed": pf,
            "apparent_power_kva": triangle.apparent_power_kva,
            "reactive_power_kvar": triangle.reactive_power_kvar,
            "current_a": current.current_a,
            "source_load_id": load["id"],
            "native_circuit_status": "NOT_VERIFIED_UNTIL_REVIT_AUDIT",
            "wire_2d_view_specific_confirmed": False,
            "three_dimensional_cable_modeled": False,
            "physical_power_conductor_confirmed": False,
            "source_or_assumption": provenance.source_or_assumption,
            "status": provenance.status,
        })
    total_p_kw = sum(row["active_power_kw_demand_assumed"] for row in circuits)
    total_s_kva = sum(row["apparent_power_kva"] for row in circuits)
    total_q_kvar = sum(row["reactive_power_kvar"] for row in circuits)
    phase_currents = {
        phase: sum(row["current_a"] for row in circuits if row["phase_assigned_for_study"] == phase)
        for phase in "ABC"
    }
    return {
        "project": "19 - Subestacao GIS 138/13,8 kV",
        "revision": revision,
        "source_inputs": "../../servicos-auxiliares-r8.json / AUX-AC-06",
        "status": "PRELIMINARY_AUXILIARY_BRANCH_CIRCUIT_STUDY",
        "distribution_system": {
            "line_to_line_voltage_v_assumed": voltage_ll,
            "line_to_neutral_voltage_v_assumed": voltage_ln,
            "configuration": "380Y/220 V, 3-phase, 4-wire; 220 V one-phase loads are an assumption",
            "circuits_included": count,
        },
        "aggregate": {
            "active_power_kw_demand": total_p_kw,
            "apparent_power_kva_sum_of_branch_loads": total_s_kva,
            "reactive_power_kvar_sum_of_branch_loads": total_q_kvar,
            "phase_assignment_counts": {phase: sum(1 for row in circuits if row["phase_assigned_for_study"] == phase) for phase in "ABC"},
            "phase_currents_a_arithmetic_branch_sums": phase_currents,
            "neutral_current_a": None,
            "source_load_basis": load["basis"],
            "source_power_factor_assumption": pf,
            "status": "DEMAND_AND_PHASE_BALANCE_ARE_STUDY_ASSUMPTIONS_NOT_PANEL_SIZING",
        },
        "circuits": circuits,
        "model_validation": {
            "status": f"NOT_RUN_NO_REVIT_{revision}_AUDIT",
            "native_systems_confirmed": 0,
            "expected_systems": count,
            "physical_power_conductors_confirmed": 0,
            "limitations": [
                "220 V line-neutral circuit input is inferred from the 380Y/220 V four-wire setup; confirm with project electrical basis.",
                "11 x 150 W and PF 0.90 are explicit R8 study assumptions, not equipment nameplate values.",
                "Phase assignment is a balancing proposal; actual phase allocation and neutral-current phasors are not modeled.",
                "No branch conductor, overcurrent device, voltage drop, short-circuit withstand, or selectivity is sized.",
                "Revit ElectricalSystem membership is logical model connectivity; it does not prove a 3D cable or field installation.",
            ],
        },
    }


def validate_revit_audit(
    schedule: dict[str, Any],
    equipment_rows: list[dict[str, str]],
    audit: dict[str, Any],
) -> dict[str, Any]:
    row_by_mark = {row.get("marca", ""): row for row in equipment_rows}
    panel_row = row_by_mark.get("QAC-01")
    systems = audit.get("electrical_system_details", [])
    confirmed: dict[str, dict[str, Any]] = {}
    reasons: dict[str, str] = {}
    for expected in schedule["circuits"]:
        system_id = expected["revit_system_id"]
        terminal_mark = expected["terminal_equipment_mark"]
        matches = [row for row in systems if row.get("system_id") == system_id]
        if len(matches) != 1:
            reasons[system_id] = f"Expected one audited ElectricalSystem, found {len(matches)}"
            continue
        system = matches[0]
        selected_panel = system.get("selected_panel_element") or {}
        terminals = system.get("terminal_elements") or []
        terminal_rows = [row for row in terminals if row.get("mark") == terminal_mark]
        audited_terminal = row_by_mark.get(terminal_mark)
        panel_identity_matches = bool(
            panel_row
            and selected_panel.get("mark") == "QAC-01"
            and str(selected_panel.get("element_id", "")) == panel_row.get("element_id", "")
            and selected_panel.get("unique_id") == panel_row.get("unique_id")
        )
        terminal_identity_matches = bool(
            audited_terminal
            and len(terminal_rows) == 1
            and str(terminal_rows[0].get("element_id", "")) == audited_terminal.get("element_id", "")
            and terminal_rows[0].get("unique_id") == audited_terminal.get("unique_id")
        )
        electrical_connectors_present = bool(
            panel_row
            and audited_terminal
            and _as_int(panel_row.get("mep_connectors", "0")) > 0
            and _as_int(audited_terminal.get("mep_connectors", "0")) > 0
            and "DomainElectrical" in panel_row.get("dominios", "")
            and "DomainElectrical" in audited_terminal.get("dominios", "")
        )
        system_identity_confirmed = bool(
            system.get("revit_class") == "ElectricalSystem"
            and system.get("system_type") == "PowerCircuit"
            and system.get("native_membership_confirmed") is True
            and system.get("panel_assignment_confirmed") is True
            and system.get("terminal_membership_confirmed") is True
            and system.get("load_name_confirmed") is True
            and system.get("revit_load_name") == system_id
            and system.get("load_calculation_confirmed") is True
        )
        actual_load_matches = all((
            _close(system.get("model_voltage_V"), expected["nominal_voltage_v_line_to_neutral_assumed"], 1.0),
            _close(system.get("model_true_load_W"), expected["active_power_kw_demand_assumed"] * 1000.0, 0.5),
            _close(system.get("model_apparent_power_VA"), expected["apparent_power_kva"] * 1000.0, 1.0),
            _close(system.get("model_power_factor"), expected["power_factor_assumed"], 0.01),
            _close(system.get("model_apparent_current_A"), expected["current_a"], 0.02),
        ))
        if not panel_identity_matches:
            reasons[system_id] = "Selected QAC-01 panel identity does not match the audited equipment row"
            continue
        if not terminal_identity_matches:
            reasons[system_id] = f"Terminal {terminal_mark} identity does not match the audited equipment row"
            continue
        if not electrical_connectors_present:
            reasons[system_id] = "Electrical-domain connectors are not present at both audited endpoints"
            continue
        if not system_identity_confirmed:
            reasons[system_id] = "PowerCircuit, native membership, panel/terminal assignment, LoadName, or load-calculation confirmation is missing"
            continue
        if not actual_load_matches:
            reasons[system_id] = "Revit-calculated voltage/load/apparent power/power factor/current does not match the stated study assumptions"
            continue
        confirmed[system_id] = system

    updated = json.loads(json.dumps(schedule))
    for row in updated["circuits"]:
        system = confirmed.get(row["revit_system_id"])
        row["native_circuit_status"] = (
            "CONFIRMED_REVIT_LOGICAL_SYSTEM_AND_LOAD_NO_PHYSICAL_CONDUCTOR"
            if system else "NOT_CONFIRMED"
        )
        row["revit_element_id"] = system.get("revit_element_id") if system else None
        row["revit_unique_id"] = system.get("revit_unique_id") if system else None
        row["circuit_number"] = system.get("circuit_number") if system else None
        row["wire_2d_view_specific_confirmed"] = bool(system.get("wire_2d_view_specific_confirmed", False)) if system else False
        row["three_dimensional_cable_modeled"] = bool(system.get("three_dimensional_cable_modeled", False)) if system else False
        row["model_voltage_v"] = system.get("model_voltage_V") if system else None
        row["model_true_load_w"] = system.get("model_true_load_W") if system else None
        row["model_apparent_power_va"] = system.get("model_apparent_power_VA") if system else None
        row["model_power_factor"] = system.get("model_power_factor") if system else None
        row["model_apparent_current_a"] = system.get("model_apparent_current_A") if system else None
        row["logical_endpoint_connectors_isconnected_true"] = system.get("physical_endpoint_connectors_isconnected_true", 0) if system else 0
        row["load_calculation_confirmed"] = bool(system.get("load_calculation_confirmed", False)) if system else False
    updated["model_validation"] = {
        "status": "ALL_EXPECTED_LOGICAL_SYSTEMS_AND_REVIT_LOADS_CONFIRMED_NO_PHYSICAL_FEEDERS" if len(confirmed) == len(updated["circuits"]) else "PARTIAL_OR_FAILED_LOGICAL_SYSTEM_OR_LOAD_VALIDATION",
        "native_systems_confirmed": len(confirmed),
        "calculated_loads_confirmed": len(confirmed),
        "expected_systems": len(updated["circuits"]),
        "physical_endpoint_connectors_isconnected_true_by_logical_system": {
            key: int(item.get("physical_endpoint_connectors_isconnected_true", 0) or 0)
            for key, item in confirmed.items()
        },
        "physical_power_conductors_confirmed": sum(
            bool(row["three_dimensional_cable_modeled"] or row["wire_2d_view_specific_confirmed"])
            for row in updated["circuits"]
        ),
        "unconfirmed_systems": reasons,
        "limitations": schedule["model_validation"]["limitations"],
    }
    return updated


def write_outputs(root: Path, *, validate_model: bool = True, revision: str = "R12") -> dict[str, Any]:
    revision = revision.upper()
    if not re.fullmatch(r"R[1-9][0-9]*", revision):
        raise ValueError("revision must use the form R followed by a positive integer")
    data_dir = root / "projetos/19-subestacao-gis/dados"
    revision_slug = revision.lower()
    revision_dir = data_dir / f"reconstrucao-{revision_slug}"
    revision_dir.mkdir(parents=True, exist_ok=True)
    input_path = data_dir / "servicos-auxiliares-r8.json"
    schedule = build_schedule(json.loads(input_path.read_text(encoding="utf-8")), revision=revision)
    audit_path = revision_dir / f"auditoria-modelo-{revision_slug}.json"
    quantities_path = revision_dir / "quantitativos-modelo.csv"
    if validate_model and audit_path.exists() and quantities_path.exists():
        with quantities_path.open("r", encoding="utf-8-sig", newline="") as stream:
            rows = list(csv.DictReader(stream, delimiter=";"))
        audit = json.loads(audit_path.read_text(encoding="utf-8"))
        schedule = validate_revit_audit(schedule, rows, audit)
    json_path = revision_dir / f"circuitos-auxiliares-{revision_slug}.json"
    json_path.write_text(json.dumps(schedule, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    csv_path = revision_dir / f"circuitos-auxiliares-{revision_slug}.csv"
    columns = [
        "calculation_id", "revit_system_id", "source_equipment_mark", "terminal_equipment_mark",
        "phase_assigned_for_study", "nominal_voltage_v_line_to_neutral_assumed",
        "active_power_kw_demand_assumed", "power_factor_assumed", "apparent_power_kva",
        "reactive_power_kvar", "current_a", "native_circuit_status", "revit_element_id",
        "revit_unique_id", "circuit_number", "wire_2d_view_specific_confirmed",
        "three_dimensional_cable_modeled", "physical_power_conductor_confirmed",
        "model_voltage_v", "model_true_load_w", "model_apparent_power_va",
        "model_power_factor", "model_apparent_current_a",
        "logical_endpoint_connectors_isconnected_true", "load_calculation_confirmed", "status",
    ]
    with csv_path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=columns, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(schedule["circuits"])
    return schedule


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd(), help="Repository root")
    parser.add_argument("--revision", default="R12", help="GIS revision to validate (for example R13)")
    args = parser.parse_args()
    result = write_outputs(args.root, revision=args.revision)
    print(json.dumps(result["model_validation"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
