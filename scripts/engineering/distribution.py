"""Distribution calculations and a traceable JSON load-schedule runner."""

from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path
from typing import Any

from .electrical_core import (
    DemandLoad,
    DemandSchedule,
    DemandScheduleLine,
    Provenance,
    TransformerLoading,
    aggregate_demand_schedule,
    identified_calculation_record,
    transformer_loading,
)


def calculate_demand_schedule(data: dict[str, Any]) -> dict[str, Any]:
    """Validate structured inputs and return one identified demand calculation."""
    if not isinstance(data, dict):
        raise ValueError("input document must be a JSON object")
    required = (
        "project",
        "revision",
        "calc_id",
        "status",
        "source_or_assumption",
        "active_power_unit",
        "reactive_power_unit",
        "limitations",
        "loads",
    )
    missing = [key for key in required if key not in data]
    if missing:
        raise ValueError(f"missing required input fields: {', '.join(missing)}")
    if data["active_power_unit"] != "kW" or data["reactive_power_unit"] != "kvar":
        raise ValueError("schedule units must explicitly be kW and kvar")
    for key in ("project", "revision", "calc_id", "status", "source_or_assumption"):
        if not isinstance(data[key], str) or not data[key].strip():
            raise ValueError(f"{key} cannot be blank")
    if not isinstance(data["loads"], list) or not data["loads"]:
        raise ValueError("loads must be a non-empty list")

    load_values: list[DemandLoad] = []
    for index, row in enumerate(data["loads"]):
        if not isinstance(row, dict):
            raise ValueError(f"loads[{index}] must be an object")
        required_load_fields = (
            "equipment_id",
            "connected_active_power_kw",
            "demand_factor",
            "source_or_assumption",
        )
        missing_load = [key for key in required_load_fields if key not in row]
        if missing_load:
            raise ValueError(f"loads[{index}] missing fields: {', '.join(missing_load)}")
        has_pf = "power_factor" in row and row["power_factor"] is not None
        has_direction = "reactive_direction" in row and row["reactive_direction"] is not None
        if has_pf != has_direction:
            raise ValueError(f"loads[{index}] must supply power_factor and reactive_direction together")
        if not isinstance(row["source_or_assumption"], str) or not row["source_or_assumption"].strip():
            raise ValueError(f"loads[{index}].source_or_assumption cannot be blank")
        load_values.append(
            DemandLoad(
                equipment_id=row["equipment_id"],
                connected_active_power_kw=row["connected_active_power_kw"],
                power_factor=row.get("power_factor"),
                demand_factor=row["demand_factor"],
                reactive_direction=row.get("reactive_direction"),
            )
        )

    schedule = aggregate_demand_schedule(
        loads=load_values,
        provenance=Provenance(data["source_or_assumption"], data["status"]),
    )
    calculation = identified_calculation_record(
        calc_id=data["calc_id"],
        inputs=data,
        method=(
            "Soma de P e Q diversificados por carga; S = sqrt(P^2 + Q^2)"
            if all(line.demand_reactive_power_kvar is not None for line in schedule.lines)
            else "Soma de P diversificada por carga; Q/S permanecem desconhecidos sem FP e sentido reativo"
        ),
        result=schedule,
        unit=(
            "kW, kvar, kVA, PF"
            if all(line.demand_reactive_power_kvar is not None for line in schedule.lines)
            else "kW; kvar/kVA/PF não calculados quando ausentes nas entradas"
        ),
        limitations=data["limitations"],
    )
    return {
        "project": data["project"],
        "revision": data["revision"],
        "status": data["status"],
        "calculation": calculation,
    }


def write_demand_schedule(data: dict[str, Any], output_dir: Path) -> dict[str, Any]:
    """Write identified JSON results and one CSV row per equipment load."""
    result = calculate_demand_schedule(data)
    output_dir.mkdir(parents=True, exist_ok=True)
    json_path = output_dir / "demand-schedule-results.json"
    csv_path = output_dir / "demand-schedule-lines.csv"
    json_path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    lines = result["calculation"]["result"]["lines"]
    fieldnames = [
        "equipment_id",
        "connected_active_power_kw",
        "demand_factor",
        "demand_active_power_kw",
        "demand_reactive_power_kvar",
        "demand_apparent_power_kva",
        "power_factor",
        "reactive_direction",
    ]
    with csv_path.open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, delimiter=";")
        writer.writeheader()
        writer.writerows(lines)
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inputs", required=True, type=Path, help="structured JSON load schedule")
    parser.add_argument("--output", required=True, type=Path, help="output directory")
    args = parser.parse_args()
    data = json.loads(args.inputs.read_text(encoding="utf-8"))
    result = write_demand_schedule(data, args.output)
    print(json.dumps(result["calculation"]["result"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()


__all__ = [
    "DemandLoad",
    "DemandSchedule",
    "DemandScheduleLine",
    "TransformerLoading",
    "aggregate_demand_schedule",
    "calculate_demand_schedule",
    "transformer_loading",
    "write_demand_schedule",
]
