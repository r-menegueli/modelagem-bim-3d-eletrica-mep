"""Traceable logical I/O maps for automation and instrumentation studies.

This module records tag-to-equipment relationships only. It does not claim that
field wiring, PLC logic, a network protocol, interlocks, or control performance
have been implemented or tested.
"""

from __future__ import annotations

import argparse
from dataclasses import asdict, dataclass
import json
import math
from pathlib import Path
from typing import Any, Iterable


SIGNAL_KINDS = frozenset({"AI", "AO", "DI", "DO"})


@dataclass(frozen=True)
class IOPoint:
    point_id: str
    signal_kind: str
    source_equipment_id: str
    destination_equipment_id: str
    function: str
    engineering_units: str = ""
    range_low: float | None = None
    range_high: float | None = None
    source_or_assumption: str = "Explicitly declared functional point"


@dataclass(frozen=True)
class EquipmentRelation:
    relation_id: str
    from_equipment_id: str
    to_equipment_id: str
    relation_type: str
    description: str
    evidence_class: str = "LOGICAL_RELATION_ONLY"


def build_control_architecture(
    *, equipment_ids: Iterable[str], controller_ids: Iterable[str],
    field_device_ids: Iterable[str], points: Iterable[IOPoint],
    relations: Iterable[EquipmentRelation] = (),
) -> dict:
    """Validate IDs and return an auditable logical I/O architecture record."""
    known = tuple(equipment_ids)
    controllers = tuple(controller_ids)
    field_devices = tuple(field_device_ids)
    if not known or any(not isinstance(item, str) or not item.strip() for item in known):
        raise ValueError("equipment_ids must contain nonblank IDs")
    known_set = set(known)
    if len(known_set) != len(known):
        raise ValueError("equipment_ids must be unique")
    for label, values in (("controller_ids", controllers), ("field_device_ids", field_devices)):
        if any(not isinstance(item, str) or not item.strip() for item in values):
            raise ValueError(f"{label} must contain nonblank string IDs")
        if len(set(values)) != len(values) or any(item not in known_set for item in values):
            raise ValueError(f"{label} must be unique IDs present in equipment_ids")
    if set(controllers) & set(field_devices):
        raise ValueError("an equipment ID cannot be classified as both controller and field device")

    point_rows = tuple(points)
    relation_rows = tuple(relations)
    point_ids: set[str] = set()
    serialized_points = []
    for point in point_rows:
        if not isinstance(point, IOPoint):
            raise ValueError("points must contain IOPoint values")
        if not isinstance(point.point_id, str) or not point.point_id.strip() or point.point_id in point_ids:
            raise ValueError("point_id values must be nonblank and unique")
        point_ids.add(point.point_id)
        if not isinstance(point.signal_kind, str) or point.signal_kind not in SIGNAL_KINDS:
            raise ValueError(f"{point.point_id}: signal_kind must be one of {sorted(SIGNAL_KINDS)}")
        if point.source_equipment_id not in known_set or point.destination_equipment_id not in known_set:
            raise ValueError(f"{point.point_id}: endpoints must exist in equipment_ids")
        if not isinstance(point.function, str) or not point.function.strip():
            raise ValueError(f"{point.point_id}: function cannot be blank")
        if not isinstance(point.source_or_assumption, str) or not point.source_or_assumption.strip():
            raise ValueError(f"{point.point_id}: source_or_assumption cannot be blank")
        expected_source = field_devices if point.signal_kind in {"AI", "DI"} else controllers
        expected_destination = controllers if point.signal_kind in {"AI", "DI"} else field_devices
        if point.source_equipment_id not in expected_source or point.destination_equipment_id not in expected_destination:
            raise ValueError(f"{point.point_id}: endpoint direction does not match {point.signal_kind}")
        has_range = point.range_low is not None or point.range_high is not None
        if point.signal_kind in {"AI", "AO"}:
            if (not isinstance(point.engineering_units, str) or not point.engineering_units.strip()
                    or point.range_low is None or point.range_high is None):
                raise ValueError(f"{point.point_id}: analog point requires units and both range limits")
            if (isinstance(point.range_low, bool) or isinstance(point.range_high, bool)
                    or not isinstance(point.range_low, (int, float)) or not isinstance(point.range_high, (int, float))
                    or not math.isfinite(point.range_low) or not math.isfinite(point.range_high)):
                raise ValueError(f"{point.point_id}: analog range limits must be real numbers")
            if point.range_low >= point.range_high:
                raise ValueError(f"{point.point_id}: range_low must be below range_high")
        elif has_range or (not isinstance(point.engineering_units, str) or point.engineering_units):
            raise ValueError(f"{point.point_id}: discrete points cannot declare an analog range or units")
        row = asdict(point)
        row["implementation_status"] = "DOCUMENTED_LOGICAL_POINT_NOT_FIELD_WIRING_OR_TESTED_CONTROL"
        row["protocol_status"] = "NOT_SPECIFIED_OR_TESTED"
        serialized_points.append(row)

    relation_ids: set[str] = set()
    serialized_relations = []
    for relation in relation_rows:
        if not isinstance(relation, EquipmentRelation):
            raise ValueError("relations must contain EquipmentRelation values")
        if (not isinstance(relation.relation_id, str) or not relation.relation_id.strip()
                or relation.relation_id in relation_ids):
            raise ValueError("relation_id values must be nonblank and unique")
        relation_ids.add(relation.relation_id)
        if relation.from_equipment_id not in known_set or relation.to_equipment_id not in known_set:
            raise ValueError(f"{relation.relation_id}: relation endpoints must exist in equipment_ids")
        if (not isinstance(relation.relation_type, str) or not relation.relation_type.strip()
                or not isinstance(relation.description, str) or not relation.description.strip()):
            raise ValueError(f"{relation.relation_id}: type and description cannot be blank")
        if relation.evidence_class != "LOGICAL_RELATION_ONLY":
            raise ValueError("this module only records LOGICAL_RELATION_ONLY evidence")
        row = asdict(relation)
        row["implementation_status"] = "DOCUMENTED_LOGICAL_RELATION_NOT_PHYSICAL_CONNECTION"
        serialized_relations.append(row)

    return {
        "equipment_ids": list(known),
        "controllers": list(controllers),
        "field_devices": list(field_devices),
        "points": serialized_points,
        "relations": serialized_relations,
        "summary": {
            "point_count": len(serialized_points),
            "analog_input_count": sum(row["signal_kind"] == "AI" for row in serialized_points),
            "analog_output_count": sum(row["signal_kind"] == "AO" for row in serialized_points),
            "digital_input_count": sum(row["signal_kind"] == "DI" for row in serialized_points),
            "digital_output_count": sum(row["signal_kind"] == "DO" for row in serialized_points),
            "relation_count": len(serialized_relations),
            "physical_wiring_confirmed": False,
            "control_logic_tested": False,
            "protocols_tested": [],
        },
        "limitations": [
            "I/O and equipment relations are functional documentation only; field wiring is not proven.",
            "No PLC/RTU logic, interlock, fail-safe behavior, protocol, or control performance is implemented or tested.",
            "Instrument calibration, accuracy, range selection and hazardous-area suitability require project-specific data.",
        ],
    }


def control_architecture_from_mapping(data: dict[str, Any]) -> dict:
    """Build an architecture from a JSON-compatible mapping."""
    if not isinstance(data, dict):
        raise ValueError("architecture input must be a JSON object")
    required = {"equipment_ids", "controller_ids", "field_device_ids", "points"}
    missing = required - data.keys()
    if missing:
        raise ValueError(f"architecture input is missing keys: {', '.join(sorted(missing))}")
    arrays = ("equipment_ids", "controller_ids", "field_device_ids", "points")
    if any(not isinstance(data[key], list) for key in arrays) or not isinstance(data.get("relations", []), list):
        raise ValueError("equipment IDs, points and relations must be JSON arrays")
    try:
        points = tuple(IOPoint(**row) for row in data["points"])
        relations = tuple(EquipmentRelation(**row) for row in data.get("relations", []))
    except (TypeError, ValueError) as error:
        raise ValueError(f"invalid point or relation record: {error}") from error
    return build_control_architecture(
        equipment_ids=data["equipment_ids"],
        controller_ids=data["controller_ids"],
        field_device_ids=data["field_device_ids"],
        points=points,
        relations=relations,
    )


def write_control_architecture(input_path: Path, output_path: Path) -> dict:
    """Read structured JSON and write a validated JSON architecture report."""
    data = json.loads(input_path.read_text(encoding="utf-8"))
    result = control_architecture_from_mapping(data)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inputs", type=Path, required=True, help="JSON with equipment, controller, field device and point records")
    parser.add_argument("--output", type=Path, required=True, help="Validated JSON report destination")
    args = parser.parse_args()
    result = write_control_architecture(args.inputs, args.output)
    print(json.dumps(result["summary"], ensure_ascii=False, indent=2))


__all__ = [
    "EquipmentRelation", "IOPoint", "SIGNAL_KINDS", "build_control_architecture",
    "control_architecture_from_mapping", "write_control_architecture",
]


if __name__ == "__main__":
    main()
