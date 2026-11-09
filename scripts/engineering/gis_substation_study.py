"""Traceable concept calculations for the Project 19 GIS substation.

The tool calculates only from declared study assumptions. It does not size
protective devices, certify equipment, or replace utility/network data.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
from pathlib import Path
from typing import Any


DIAGRAM_TO_MODEL_MARKS = {
    "GIS-L01": ["GIS-L01-ENV", "GIS-L01-52", "GIS-L01-89A", "GIS-L01-89B", "GIS-L01-89G", "GIS-L01-CT", "GIS-L01-VT", "GIS-L01-TERM"],
    "GIS-L02": ["GIS-L02-ENV", "GIS-L02-52", "GIS-L02-89A", "GIS-L02-89B", "GIS-L02-89G", "GIS-L02-CT", "GIS-L02-VT", "GIS-L02-TERM"],
    "TR-01": ["TR-01"],
    "TR-02": ["TR-02"],
    "QMT-A-IN": ["QMT-A-IN"],
    "QMT-B-IN": ["QMT-B-IN"],
    "QMT-TIE": ["QMT-TIE"],
    "BUS-MV-A": ["BUS-MV-A"],
    "BUS-MV-B": ["BUS-MV-B"],
}


def _model_mark_refs(diagram_refs: list[str]) -> list[str]:
    model_marks: list[str] = []
    for ref in diagram_refs:
        if ref in DIAGRAM_TO_MODEL_MARKS:
            model_marks.extend(DIAGRAM_TO_MODEL_MARKS[ref])
        elif ref.startswith(("A-F", "B-F")):
            model_marks.append(f"QMT-{ref}")
        else:
            raise ValueError(f"Tag de diagrama sem crosswalk para marca BIM: {ref}")
    return list(dict.fromkeys(model_marks))


def _all_referenced_model_marks(result: dict[str, Any]) -> set[str]:
    marks = {
        mark
        for entry in result["calculations"]
        for mark in entry["model_mark_refs"]
    }
    for scenario in result["n_minus_one_scenarios"]["transformer_outage_with_tie_open"]:
        marks.update(scenario["model_mark_refs"])
    for option in result["n_minus_one_scenarios"]["candidate_full_noncritical_feeder_retention"]:
        marks.update(option["model_mark_refs"])
    marks.update(result["n_minus_one_scenarios"]["transfer_available_aggregate_envelope"]["model_mark_refs"])
    return marks


def audit_model_mark_crosswalk(result: dict[str, Any], quantitative_csv: Path) -> dict[str, Any]:
    path = Path(quantitative_csv)
    if not path.is_file():
        raise FileNotFoundError(f"Quantitativo Revit não encontrado: {path}")
    resolved_path = path.resolve()
    repo_root = Path(__file__).resolve().parents[2]
    try:
        source_file = resolved_path.relative_to(repo_root).as_posix()
    except ValueError:
        source_file = resolved_path.as_posix()

    with path.open(encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream, delimiter=";")
        required = {"element_id", "unique_id", "categoria_revit", "familia", "tipo", "marca"}
        fields = set(reader.fieldnames or [])
        if not required.issubset(fields):
            missing_fields = ", ".join(sorted(required - fields))
            raise ValueError(f"CSV de quantitativos sem campos de identidade: {missing_fields}")
        by_mark: dict[str, list[dict[str, str]]] = {}
        for row in reader:
            mark = (row.get("marca") or "").strip()
            if not mark:
                continue
            by_mark.setdefault(mark, []).append({
                "mark": mark,
                "element_id": (row.get("element_id") or "").strip(),
                "unique_id": (row.get("unique_id") or "").strip(),
                "category": (row.get("categoria_revit") or "").strip(),
                "family": (row.get("familia") or "").strip(),
                "type": (row.get("tipo") or "").strip(),
            })

    referenced = sorted(_all_referenced_model_marks(result))
    mapping = {mark: by_mark.get(mark, []) for mark in referenced}
    missing = [mark for mark, rows in mapping.items() if not rows]
    duplicate = [mark for mark, rows in mapping.items() if len(rows) > 1]
    incomplete_identity = [
        mark for mark, rows in mapping.items()
        if len(rows) == 1 and (not rows[0]["element_id"] or not rows[0]["unique_id"])
    ]
    complete = not missing and not duplicate and not incomplete_identity
    return {
        "status": "MODEL_MARK_CROSSWALK_COMPLETE" if complete else "MODEL_MARK_CROSSWALK_INCOMPLETE",
        "source_file": source_file,
        "source_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "referenced_mark_count": len(referenced),
        "unique_model_mark_count": len(by_mark),
        "missing_marks": missing,
        "duplicate_referenced_marks": duplicate,
        "incomplete_element_identity_marks": incomplete_identity,
        "mark_to_revit_elements": mapping,
        "connectivity_verified": False,
        "scope_note": "Esta auditoria resolve as marcas dos cálculos para registros Revit extraídos com ElementId e UniqueId. Confirma identidade e presença da marca; não prova conectividade elétrica, participação em circuito ou continuidade de condutor físico.",
    }


def _csv_model_identity_fields(model_marks: list[str], audit: dict[str, Any]) -> dict[str, str]:
    if audit.get("status") == "NOT_VERIFIED":
        return {"model_element_ids": "", "model_unique_ids": "", "model_mark_identity_status": "NOT_VERIFIED"}
    mapping = audit.get("mark_to_revit_elements", {})
    elements = [element for mark in model_marks for element in mapping.get(mark, [])]
    complete = all(
        len(mapping.get(mark, [])) == 1
        and mapping[mark][0].get("element_id")
        and mapping[mark][0].get("unique_id")
        for mark in model_marks
    )
    return {
        "model_element_ids": "|".join(dict.fromkeys(element["element_id"] for element in elements if element["element_id"])),
        "model_unique_ids": "|".join(dict.fromkeys(element["unique_id"] for element in elements if element["unique_id"])),
        "model_mark_identity_status": "RESOLVED" if complete else "INCOMPLETE",
    }


def _entry(
    calc_id: str,
    quantity: str,
    inputs: str,
    method: str,
    result: float | str,
    unit: str,
    source: str,
    status: str = "CALCULATED_FROM_STUDY_ASSUMPTIONS",
) -> dict[str, Any]:
    return {
        "calc_id": calc_id,
        "quantity": quantity,
        "inputs": inputs,
        "method": method,
        "result": result,
        "unit": unit,
        "source_or_assumption": source,
        "status": status,
    }


def _required_mapping(value: Any, name: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ValueError(f"{name} must be an object")
    return value


def _required_number(value: Any, name: str, *, positive: bool = False, nonnegative: bool = False) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{name} must be a JSON number")
    try:
        number = float(value)
    except OverflowError as error:
        raise ValueError(f"{name} must be finite") from error
    if not math.isfinite(number):
        raise ValueError(f"{name} must be finite")
    if positive and number <= 0:
        raise ValueError(f"{name} must be positive")
    if nonnegative and number < 0:
        raise ValueError(f"{name} must be nonnegative")
    return number


def _required_integer(value: Any, name: str, *, minimum: int = 0) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < minimum:
        raise ValueError(f"{name} must be an integer >= {minimum}")
    return value


def _required_text(value: Any, name: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{name} must be a nonblank string")
    return value


def _validate_study_input(base: Any) -> None:
    """Reject coercive or non-finite project inputs before calculating."""
    base = _required_mapping(base, "study")
    _required_text(base.get("project"), "project")
    _required_integer(base.get("revision"), "revision")
    _required_text(base.get("date"), "date")
    _required_text(base.get("status"), "status")

    system = _required_mapping(base.get("system"), "system")
    voltages = system.get("nominal_voltage_kv")
    if not isinstance(voltages, (list, tuple)) or len(voltages) != 2:
        raise ValueError("system.nominal_voltage_kv must contain exactly two numbers")
    for index, voltage in enumerate(voltages):
        _required_number(voltage, f"system.nominal_voltage_kv[{index}]", positive=True)
    _required_number(system.get("frequency_hz"), "system.frequency_hz", positive=True)
    _required_integer(system.get("source_line_incomers"), "system.source_line_incomers", minimum=1)
    _required_number(system.get("high_voltage_equipment_class_kv"), "system.high_voltage_equipment_class_kv", positive=True)
    _required_text(system.get("high_voltage_bus"), "system.high_voltage_bus")
    _required_text(system.get("medium_voltage_bus"), "system.medium_voltage_bus")
    if not isinstance(system.get("parallel_transformers_normal"), bool):
        raise ValueError("system.parallel_transformers_normal must be a boolean")

    transformers = _required_mapping(base.get("transformers"), "transformers")
    _required_integer(transformers.get("count"), "transformers.count", minimum=1)
    _required_number(transformers.get("rating_mva_each"), "transformers.rating_mva_each", positive=True)
    _required_number(transformers.get("nominal_impedance_pu_each_assumed"), "transformers.nominal_impedance_pu_each_assumed", positive=True)
    _required_text(transformers.get("insulating_fluid_assumption"), "transformers.insulating_fluid_assumption")
    _required_text(transformers.get("impedance_status"), "transformers.impedance_status")

    scenarios = _required_mapping(base.get("load_scenarios"), "load_scenarios")
    for field in (
        "normal_active_mw_assumed",
        "critical_active_mw_assumed",
        "noncritical_active_mw_shed_in_n_minus_one",
    ):
        _required_number(scenarios.get(field), f"load_scenarios.{field}", nonnegative=True)
    for field in ("power_factor_assumed", "target_power_factor_for_sensitivity"):
        fraction = _required_number(scenarios.get(field), f"load_scenarios.{field}")
        if not 0 < fraction <= 1:
            raise ValueError(f"load_scenarios.{field} must be in (0, 1]")

    allocation = _required_mapping(scenarios.get("allocation_by_section_mw"), "load_scenarios.allocation_by_section_mw")
    if set(allocation) != {"section_A", "section_B"}:
        raise ValueError("load_scenarios.allocation_by_section_mw must define section_A and section_B only")
    for section, values in allocation.items():
        values = _required_mapping(values, f"load_scenarios.allocation_by_section_mw.{section}")
        for field in ("critical", "noncritical"):
            _required_number(values.get(field), f"load_scenarios.allocation_by_section_mw.{section}.{field}", nonnegative=True)

    feeders = scenarios.get("feeders")
    if not isinstance(feeders, list) or not feeders:
        raise ValueError("load_scenarios.feeders must be a nonempty array")
    feeder_positions = _required_integer(scenarios.get("feeder_positions_assumed"), "load_scenarios.feeder_positions_assumed", minimum=1)
    if feeder_positions != len(feeders):
        raise ValueError("load_scenarios.feeder_positions_assumed must match the declared feeder count")
    seen_tags: set[str] = set()
    for index, feeder in enumerate(feeders):
        feeder = _required_mapping(feeder, f"load_scenarios.feeders[{index}]")
        tag = _required_text(feeder.get("tag"), f"load_scenarios.feeders[{index}].tag")
        if tag in seen_tags:
            raise ValueError(f"duplicate feeder tag: {tag}")
        seen_tags.add(tag)
        section = feeder.get("section")
        if not isinstance(section, str) or section not in {"A", "B"}:
            raise ValueError(f"load_scenarios.feeders[{index}].section must be A or B")
        _required_number(feeder.get("active_mw"), f"load_scenarios.feeders[{index}].active_mw", nonnegative=True)
        if not isinstance(feeder.get("critical"), bool):
            raise ValueError(f"load_scenarios.feeders[{index}].critical must be a boolean")

    _required_text(scenarios.get("load_profile_status"), "load_scenarios.load_profile_status")

    protection = _required_mapping(base.get("protection"), "protection")
    _required_text(protection.get("settings_status"), "protection.settings_status")
    functions = protection.get("candidate_functions")
    if not isinstance(functions, list) or any(not isinstance(item, str) or not item.strip() for item in functions):
        raise ValueError("protection.candidate_functions must be an array of nonblank strings")

    equipment = _required_mapping(base.get("equipment_data"), "equipment_data")
    _required_text(equipment.get("geometry_status"), "equipment_data.geometry_status")
    for field in ("manufacturer_selected", "network_short_circuit_available", "soil_resistivity_available", "auxiliary_load_and_battery_mission_available"):
        if not isinstance(equipment.get(field), bool):
            raise ValueError(f"equipment_data.{field} must be a boolean")


def _assert_finite_tree(value: Any, path: str = "result") -> None:
    if isinstance(value, float) and not math.isfinite(value):
        raise ValueError(f"{path} contains a non-finite numeric result")
    if isinstance(value, dict):
        for key, child in value.items():
            _assert_finite_tree(child, f"{path}.{key}")
    elif isinstance(value, (list, tuple)):
        for index, child in enumerate(value):
            _assert_finite_tree(child, f"{path}[{index}]")


def calculate(base: dict[str, Any]) -> dict[str, Any]:
    _validate_study_input(base)
    system = base["system"]
    transformers = base["transformers"]
    scenarios = base["load_scenarios"]
    v_hv, v_mv = system["nominal_voltage_kv"]
    frequency = float(system["frequency_hz"])
    count = int(transformers["count"])
    rating = float(transformers["rating_mva_each"])
    z_pu = float(transformers["nominal_impedance_pu_each_assumed"])
    p_total = float(scenarios["normal_active_mw_assumed"])
    pf = float(scenarios["power_factor_assumed"])
    p_critical = float(scenarios["critical_active_mw_assumed"])
    p_shed = float(scenarios["noncritical_active_mw_shed_in_n_minus_one"])
    pfs = float(scenarios["target_power_factor_for_sensitivity"])
    feeders = scenarios["feeders"]

    if min(v_hv, v_mv, frequency, rating, z_pu) <= 0:
        raise ValueError("Tensões, frequência, potência e impedância devem ser positivas.")
    if count != 2 or not 0 < pf <= 1 or not 0 < pfs <= 1:
        raise ValueError("Este estudo N-1 requer exatamente dois transformadores e fatores de potência entre zero e um.")
    if min(p_total, p_critical, p_shed) < 0 or p_critical > p_total:
        raise ValueError("Cargas inválidas: críticos devem ser não negativos e não superar a carga total.")
    if abs(p_total - p_critical - p_shed) > 1e-9:
        raise ValueError("A carga total deve ser igual à soma de carga crítica e carga não crítica a rejeitar.")

    sections = {"A": [], "B": []}
    seen: set[str] = set()
    for feeder in feeders:
        tag = str(feeder["tag"])
        section = str(feeder["section"])
        power = float(feeder["active_mw"])
        if tag in seen or section not in sections or power < 0:
            raise ValueError(f"Alimentador inválido ou duplicado: {tag}")
        seen.add(tag)
        sections[section].append(feeder)

    for key, expected in scenarios["allocation_by_section_mw"].items():
        section = key[-1]
        section_p = sum(float(f["active_mw"]) for f in sections[section])
        section_critical = sum(float(f["active_mw"]) for f in sections[section] if f["critical"])
        if abs(section_p - float(expected["critical"]) - float(expected["noncritical"])) > 1e-9:
            raise ValueError(f"Alocação de alimentadores da seção {section} diverge da tabela de cargas.")
        if abs(section_critical - float(expected["critical"])) > 1e-9:
            raise ValueError(f"Carga crítica dos alimentadores da seção {section} diverge da premissa.")

    feeder_sum = sum(float(f["active_mw"]) for f in feeders)
    critical_sum = sum(float(f["active_mw"]) for f in feeders if f["critical"])
    if abs(feeder_sum - p_total) > 1e-9 or abs(critical_sum - p_critical) > 1e-9:
        raise ValueError("A soma dos alimentadores não fecha com as cargas normal e crítica declaradas.")

    section_totals = {
        section: {
            "active_mw": sum(float(f["active_mw"]) for f in section_feeders),
            "critical_mw": sum(float(f["active_mw"]) for f in section_feeders if f["critical"]),
            "noncritical_mw": sum(float(f["active_mw"]) for f in section_feeders if not f["critical"]),
        }
        for section, section_feeders in sections.items()
    }

    s_total = p_total / pf
    q_total = math.sqrt(max(0.0, s_total**2 - p_total**2))
    s_critical = p_critical / pf
    i_total_hv = s_total / (math.sqrt(3.0) * v_hv)
    i_total_mv = s_total / (math.sqrt(3.0) * v_mv)
    i_tr_hv = rating / (math.sqrt(3.0) * v_hv)
    i_tr_mv = rating / (math.sqrt(3.0) * v_mv)
    transformer_voltage_ratio_hv_to_mv = v_hv / v_mv
    i_critical_mv = s_critical / (math.sqrt(3.0) * v_mv)
    normal_loading = s_total / (count * rating) * 100.0
    n1_critical_loading = s_critical / rating * 100.0
    n1_full_loading = s_total / rating * 100.0
    one_transformer_active_capacity = rating * pf
    n1_minimum_shedding = max(0.0, p_total - one_transformer_active_capacity)
    n1_noncritical_headroom = max(0.0, one_transformer_active_capacity - p_critical)
    n1_critical_loading_after_transfer = s_critical / rating * 100.0
    n1_critical_isolated_by_open_tie = max(
        (float(section["critical_mw"]) for section in section_totals.values()),
        default=0.0,
    )
    all_feeder_tags = [str(feeder["tag"]) for feeder in feeders]
    critical_feeder_tags = [str(feeder["tag"]) for feeder in feeders if feeder["critical"]]
    noncritical_feeder_tags = [str(feeder["tag"]) for feeder in feeders if not feeder["critical"]]
    n1_open_tie_cases = []
    for failed_section in ("A", "B"):
        surviving_section = "B" if failed_section == "A" else "A"
        surviving = section_totals[surviving_section]
        isolated = section_totals[failed_section]
        failed_transformer_tag = "TR-01" if failed_section == "A" else "TR-02"
        surviving_transformer_tag = "TR-02" if failed_section == "A" else "TR-01"
        isolated_incomer_tag = f"QMT-{failed_section}-IN"
        surviving_incomer_tag = f"QMT-{surviving_section}-IN"
        n1_open_tie_cases.append({
            "scenario_id": f"N1-TIE-OPEN-TR-{failed_section}",
            "failed_transformer_section": failed_section,
            "surviving_transformer_section": surviving_section,
            "failed_transformer_tag": failed_transformer_tag,
            "isolated_section_incomer_tag": isolated_incomer_tag,
            "surviving_transformer_tag": surviving_transformer_tag,
            "surviving_section_incomer_tag": surviving_incomer_tag,
            "tie_tag": "QMT-TIE",
            "diagram_refs": [
                failed_transformer_tag,
                isolated_incomer_tag,
                surviving_transformer_tag,
                surviving_incomer_tag,
                "QMT-TIE",
                *[str(feeder["tag"]) for feeder in sections[failed_section]],
                *[str(feeder["tag"]) for feeder in sections[surviving_section]],
            ],
            "critical_load_on_surviving_section_mw": surviving["critical_mw"],
            "critical_load_on_isolated_section_mw": isolated["critical_mw"],
            "active_load_on_surviving_section_mw": surviving["active_mw"],
            "surviving_transformer_loading_percent": float(surviving["active_mw"]) / pf / rating * 100.0,
            "status": "TOPOLOGY_ONLY_TIE_OPEN_NO_CROSS_SECTION_TRANSFER",
        })
    n1_single_feeder_options = []
    for feeder in feeders:
        feeder_mw = float(feeder["active_mw"])
        if not feeder["critical"] and feeder_mw <= n1_noncritical_headroom + 1e-9:
            n1_single_feeder_options.append({
                "retained_feeder_tag": str(feeder["tag"]),
                "diagram_refs": ["TR-01", "TR-02", "QMT-TIE", *critical_feeder_tags, str(feeder["tag"])],
                "retained_noncritical_active_mw": feeder_mw,
                "aggregate_active_load_mw": p_critical + feeder_mw,
                "surviving_transformer_loading_percent": (p_critical + feeder_mw) / pf / rating * 100.0,
                "status": "INDIVIDUAL_FEEDER_OPTION_NO_PRIORITY_OR_TRANSFER_VALIDATION",
            })
    for scenario in n1_open_tie_cases:
        scenario["model_mark_refs"] = _model_mark_refs(scenario["diagram_refs"])
    for option in n1_single_feeder_options:
        option["model_mark_refs"] = _model_mark_refs(option["diagram_refs"])
    n1_largest_single_retained_feeder_mw = max(
        (float(option["retained_noncritical_active_mw"]) for option in n1_single_feeder_options),
        default=0.0,
    )
    q_comp = max(0.0, p_total * (math.tan(math.acos(pf)) - math.tan(math.acos(pfs))))
    isc_one = i_tr_mv / z_pu
    isc_parallel = count * isc_one
    bus_candidate_a = 2000.0
    section_loads = scenarios["allocation_by_section_mw"]
    section_a_mw = float(section_loads["section_A"]["critical"]) + float(section_loads["section_A"]["noncritical"])
    section_s_mva = section_a_mw / pf
    section_current_ka = section_s_mva / (math.sqrt(3.0) * v_mv)
    entries = [
        _entry("BP-01", "Potência aparente total", f"P={p_total:g} MW; FP={pf:g}", "S=P/FP", s_total, "MVA", "Premissa de estudo em base-de-projeto.json"),
        _entry("BP-02", "Potência reativa total", f"S={s_total:.6f} MVA; P={p_total:g} MW", "Q=sqrt(S^2-P^2)", q_total, "MVAr", "Premissa de estudo em base-de-projeto.json"),
        _entry("BP-03", "Corrente de carga no sistema 138 kV", f"S={s_total:.6f} MVA; VLL={v_hv:g} kV", "I=S/(sqrt(3)*VLL)", i_total_hv, "kA", "Premissa de estudo em base-de-projeto.json"),
        _entry("BP-04", "Corrente de carga no sistema 13,8 kV", f"S={s_total:.6f} MVA; VLL={v_mv:g} kV", "I=S/(sqrt(3)*VLL)", i_total_mv, "kA", "Premissa de estudo em base-de-projeto.json"),
        _entry("BP-05", "Corrente nominal por transformador no lado 138 kV", f"Sr={rating:g} MVA; VLL={v_hv:g} kV", "In=Sr/(sqrt(3)*VLL)", i_tr_hv, "kA", "Rating de estudo; substituir pela placa aprovada"),
        _entry("BP-06", "Corrente nominal por transformador no lado 13,8 kV", f"Sr={rating:g} MVA; VLL={v_mv:g} kV", "In=Sr/(sqrt(3)*VLL)", i_tr_mv, "kA", "Rating de estudo; substituir pela placa aprovada"),
        _entry("BP-07", "Carregamento normal agregado", f"S={s_total:.6f} MVA; capacidade instalada={count*rating:g} MVA", "S/capacidade instalada*100", normal_loading, "%", "Assume divisão equilibrada entre os dois transformadores"),
        _entry("BP-08", "Carregamento da carga crítica agregada após transferência entre seções no N-1", f"Pcrit={p_critical:g} MW; FP={pf:g}; Sr={rating:g} MVA", "(Pcrit/FP)/Sr*100", n1_critical_loading, "%", "Só representa atendimento das duas seções se o acoplador MT puder fechar e transferir a carga; manobra, proteção e capacidade do caminho não foram verificados"),
        _entry("BP-09", "Carregamento da carga total sob N-1", f"P={p_total:g} MW; FP={pf:g}; Sr={rating:g} MVA", "(P/FP)/Sr*100", n1_full_loading, "%", "Sem rejeição de carga; demonstra necessidade de contingência"),
        _entry("BP-10", "Carga ativa a rejeitar sob N-1", f"P={p_total:g} MW; Pcrit={p_critical:g} MW", "P-Pcrit", p_shed, "MW", "Premissa do cenário de contingência"),
        _entry("BP-11", "Corrente nominal do acoplamento/cubículo de referência", f"In de transformador={i_tr_mv*1000:.3f} A; candidato={bus_candidate_a:g} A", "In/candidato*100", i_tr_mv * 1000.0 / bus_candidate_a * 100.0, "% do candidato", "Comparação com candidato de 2.000 A; a brochura GE Vernova lista faixas de tensão e corrente, mas não comprova essa combinação específica; confirmar rating, configuração e condição térmica com o fabricante"),
        _entry("BP-12", "Corrente de curto simplificada por transformador em 13,8 kV", f"In={i_tr_mv:.6f} kA; Z={z_pu*100:g}% assumida", "Isc=In/Zpu", isc_one, "kA", "Fonte ideal; somente impedância percentual presumida do transformador"),
        _entry("BP-13", "Corrente de curto simplificada com transformadores em paralelo", f"{count} unidades idênticas; cada contribuição={isc_one:.6f} kA", "Soma de contribuições, sem impedâncias da rede/cabos", isc_parallel, "kA", "Sensibilidade; acoplador normalmente aberto no regime normal"),
        _entry("BP-14", f"Compensação idealizada de FP {pf:g} para {pfs:g}", f"P={p_total:g} MW; FPin={pf:g}; FPout={pfs:g}", "P*(tan(acos(FPin))-tan(acos(FPout)))", q_comp, "MVAr", "Estimativa sem harmônicos, variação de carga ou estudo de ressonância"),
        _entry("BP-15", "Corrente normal em cada seção de 13,8 kV", f"P por seção={section_a_mw:g} MW; FP={pf:g}; VLL={v_mv:g} kV", "(P/FP)/(sqrt(3)*VLL)", section_current_ka, "kA", "Assume cargas iguais nas seções A/B conforme alocação declarada"),
        _entry("BP-16", "Capacidade ativa de um transformador no fator de potência assumido", f"Sr={rating:g} MVA; FP={pf:g}", "P_limite=Sr*FP", one_transformer_active_capacity, "MW", "Limite de placa em regime contínuo no FP assumido; não inclui sobrecarga de emergência"),
        _entry("BP-17", "Margem aparente na placa após atendimento agregado das cargas críticas no N-1", f"Sr={rating:g} MVA; Scrit={s_critical:g} MVA", "Sr-Scrit", rating - s_critical, "MVA", "Só existe atendimento das duas seções se o acoplador MT e o caminho de transferência estiverem disponíveis; verificação pendente"),
        _entry("BP-18", "Carga não crítica adicional dentro da capacidade de placa após as cargas críticas", f"P_limite={one_transformer_active_capacity:g} MW; Pcrit={p_critical:g} MW", "max(0, P_limite-Pcrit)", n1_noncritical_headroom, "MW", "Envelope agregado teórico com acoplador fechado; seleção de alimentadores e capacidade do acoplador pendentes"),
        _entry("BP-19", "Rejeição ativa mínima agregada para limitar o transformador remanescente à placa no N-1", f"Pnormal={p_total:g} MW; P_limite={one_transformer_active_capacity:g} MW", "max(0, Pnormal-P_limite)", n1_minimum_shedding, "MW", "Envelope agregado contínuo a FP uniforme; não é filosofia de rejeição automática nem lista de prioridade de cargas"),
        _entry("BP-20", "Carga crítica em uma seção isolada com acoplador MT aberto", f"Máxima carga crítica de uma seção={n1_critical_isolated_by_open_tie:g} MW", "max(Pcrit_A, Pcrit_B)", n1_critical_isolated_by_open_tie, "MW", "Carga crítica associada ao transformador indisponível sem caminho de transferência; resultado depende da alocação real por seção"),
        _entry("BP-21", "Carregamento N-1 com cargas críticas e o maior alimentador não crítico individual que cabe", f"Pcrit={p_critical:g} MW; Pnc={n1_largest_single_retained_feeder_mw:g} MW; FP={pf:g}; Sr={rating:g} MVA", "((Pcrit+Pnc)/FP)/Sr*100", (p_critical + n1_largest_single_retained_feeder_mw) / pf / rating * 100.0, "%", "Exige transferência entre seções; mostra apenas a maior opção individual e não escolhe prioridade operacional"),
        _entry("BP-22", "Relação nominal de tensão linha-linha do transformador", f"VHVLL={v_hv:g} kV; VLVLL={v_mv:g} kV", "VHVLL/VLVLL", f"{transformer_voltage_ratio_hv_to_mv:g}:1", "relação VLL", "Relação de tensões nominais do estudo; placa, grupo vetorial e taps devem ser confirmados"),
    ]

    for section in ("A", "B"):
        for feeder in sections[section]:
            p = float(feeder["active_mw"])
            s = p / pf
            current = s / (math.sqrt(3.0) * v_mv)
            entries.append(_entry(
                f"FDR-{feeder['tag']}",
                f"Corrente estimada do alimentador {feeder['tag']}",
                f"P={p:g} MW; FP={pf:g}; VLL={v_mv:g} kV",
                "If=S/(sqrt(3)*VLL), where S=P/FP",
                current,
                "kA",
                f"Carga {('crítica' if feeder['critical'] else 'não crítica')} assumida; sem método de instalação, comprimento ou agrupamento",
                "CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED",
            ))

    diagram_refs_by_calc_id = {
        "BP-01": all_feeder_tags,
        "BP-02": all_feeder_tags,
        "BP-03": ["GIS-L01", "GIS-L02"],
        "BP-04": ["QMT-A-IN", "QMT-B-IN", "BUS-MV-A", "BUS-MV-B", *all_feeder_tags],
        "BP-05": ["TR-01", "TR-02"],
        "BP-06": ["TR-01", "TR-02"],
        "BP-07": ["TR-01", "TR-02", *all_feeder_tags],
        "BP-08": ["TR-01", "TR-02", "QMT-A-IN", "QMT-B-IN", "QMT-TIE", *critical_feeder_tags],
        "BP-09": ["TR-01", "TR-02", *all_feeder_tags],
        "BP-10": all_feeder_tags,
        "BP-11": ["QMT-TIE"],
        "BP-12": ["TR-01", "TR-02"],
        "BP-13": ["TR-01", "TR-02", "BUS-MV-A", "BUS-MV-B", "QMT-TIE"],
        "BP-14": all_feeder_tags,
        "BP-15": ["QMT-A-IN", "QMT-B-IN", "BUS-MV-A", "BUS-MV-B", *all_feeder_tags],
        "BP-16": ["TR-01", "TR-02"],
        "BP-17": ["TR-01", "TR-02", "QMT-TIE", "BUS-MV-A", "BUS-MV-B", *critical_feeder_tags],
        "BP-18": ["TR-01", "TR-02", "QMT-TIE", "BUS-MV-A", "BUS-MV-B", *noncritical_feeder_tags],
        "BP-19": ["TR-01", "TR-02", *all_feeder_tags],
        "BP-20": ["TR-01", "TR-02", "QMT-A-IN", "QMT-B-IN", "QMT-TIE", *critical_feeder_tags],
        "BP-21": ["TR-01", "TR-02", "QMT-TIE", "BUS-MV-A", "BUS-MV-B", *critical_feeder_tags, *[option["retained_feeder_tag"] for option in n1_single_feeder_options]],
        "BP-22": ["TR-01", "TR-02"],
    }
    for entry in entries:
        calc_id = entry["calc_id"]
        if calc_id.startswith("FDR-"):
            refs = [calc_id[4:]]
        else:
            refs = diagram_refs_by_calc_id.get(calc_id)
        if not refs:
            raise ValueError(f"Cálculo sem referência de diagrama/circuito: {calc_id}")
        entry["diagram_refs"] = refs
        entry["model_mark_refs"] = _model_mark_refs(refs)

    not_calculated = [
        {"calc_id": "SC-HV-01", "quantity": "Curto-circuito em 138 kV", "status": "PENDING_NETWORK_EQUIVALENT", "missing_data": "MVA de curto / impedância equivalente, X/R, configuração de fontes e tempos da concessionária"},
        {"calc_id": "SC-MV-01", "quantity": "Curto-circuito completo em 13,8 kV", "status": "PENDING_SOURCE_TRANSFORMER_CABLE_DATA", "missing_data": "Rede, impedâncias reais de transformadores, motores, cabos, X/R e decaimento"},
        {"calc_id": "CAB-01", "quantity": "Seções de condutores e queda de tensão", "status": "PENDING_INSTALLATION_DATA", "missing_data": "Comprimento real, método, material, temperatura, agrupamento, formação, limites de queda e curto"},
        {"calc_id": "PROT-01", "quantity": "Ajustes e coordenação de proteção", "status": "PENDING_NETWORK_AND_DEVICE_DATA", "missing_data": "Curvas dos equipamentos, TCs/TPs, seletividade, tempos, rede e filosofia da concessionária"},
        {"calc_id": "AUX-01", "quantity": "Demanda de serviços auxiliares CA", "status": "PENDING_LOAD_REGISTER", "missing_data": "Lista de HVAC, bombas, iluminação, tomadas, motores e regimes simultâneos"},
        {"calc_id": "DC-01", "quantity": "Capacidade de baterias e missão da UPS", "status": "PENDING_MISSION_PROFILE", "missing_data": "Tensão CC escolhida, cargas contínuas, sequência de manobras, autonomia, temperatura, envelhecimento e tensão final"},
        {"calc_id": "GND-01", "quantity": "Malha de aterramento e tensões de toque/passo", "status": "PENDING_SOIL_AND_FAULT_DATA", "missing_data": "Resistividade por camada, corrente/tempo de falta à terra, geometria e condições da superfície"},
        {"calc_id": "XFMR-01", "quantity": "Perdas, regulação e elevação térmica dos transformadores", "status": "PENDING_NAMEPLATE_AND_TEST_DATA", "missing_data": "Perdas em vazio/carga, tensão de curto, taps, refrigeração, temperatura e relatório de ensaio"},
    ]
    checks = {
        "all_calculations_have_diagram_and_model_refs": all(
            bool(entry.get("diagram_refs")) and bool(entry.get("model_mark_refs")) for entry in entries
        ),
        "feeders_sum_to_normal_load": math.isclose(feeder_sum, p_total, abs_tol=1e-9),
        "feeders_sum_to_critical_load": math.isclose(critical_sum, p_critical, abs_tol=1e-9),
        "critical_load_within_one_transformer_nameplate": s_critical <= rating,
        "full_load_requires_shedding_in_n_minus_one": s_total > rating,
        "normal_transformers_not_overloaded_if_equally_shared": normal_loading <= 100.0,
        "n1_open_tie_serves_all_critical_sections": not any(
            case["critical_load_on_isolated_section_mw"] > 0 for case in n1_open_tie_cases
        ),
        "n1_critical_load_fits_one_unit_if_transfer_path_is_available": s_critical <= rating,
        "n1_minimum_shedding_satisfies_one_unit_nameplate": (p_total - n1_minimum_shedding) / pf <= rating,
        "n1_full_noncritical_shedding_is_at_least_minimum_nameplate_shedding": p_shed + 1e-9 >= n1_minimum_shedding,
    }
    result = {
        "project": base["project"],
        "revision": base["revision"],
        "calculation_date": base["date"],
        "status": base["status"],
        "input_file": "base-de-projeto.json",
        "system_summary": {
            "nominal_voltage_kv": [v_hv, v_mv],
            "frequency_hz": frequency,
            "source_line_incomers": system["source_line_incomers"],
            "high_voltage_bus": system["high_voltage_bus"],
            "medium_voltage_bus": system["medium_voltage_bus"],
        },
        "inputs": base,
        "calculations": entries,
        "identifier_scope": "diagram_refs are E-01/feeder schedule tags; model_mark_refs are Revit Mark values. The optional model_mark_crosswalk_audit resolves those marks to extracted ElementId/UniqueId records and never treats identity as electrical connectivity.",
        "diagram_to_model_crosswalk": {
            ref: _model_mark_refs([ref])
            for ref in sorted({ref for entry in entries for ref in entry["diagram_refs"]})
        },
        "n_minus_one_scenarios": {
            "inputs_and_assumptions": [
                "Dois transformadores idênticos, cada um associado a uma seção MT A/B em regime normal.",
                "O acoplador MT é normalmente aberto conforme a base de projeto.",
                "FP uniforme igual ao valor assumido; capacidade ativa equivalente = MVA nominal × FP.",
                "O caso com acoplador fechado é condicional: capacidade do acoplador, intertravamentos, proteção, cabos e procedimento de transferência não foram verificados.",
            ],
            "transformer_outage_with_tie_open": n1_open_tie_cases,
            "candidate_full_noncritical_feeder_retention": n1_single_feeder_options,
            "transfer_available_aggregate_envelope": {
                "diagram_refs": ["TR-01", "TR-02", "QMT-A-IN", "QMT-B-IN", "BUS-MV-A", "BUS-MV-B", "QMT-TIE", *all_feeder_tags],
                "model_mark_refs": _model_mark_refs(["TR-01", "TR-02", "QMT-A-IN", "QMT-B-IN", "BUS-MV-A", "BUS-MV-B", "QMT-TIE", *all_feeder_tags]),
                "critical_active_load_mw": p_critical,
                "critical_apparent_load_mva": s_critical,
                "surviving_transformer_loading_percent_for_critical_load": n1_critical_loading_after_transfer,
                "one_transformer_active_capacity_at_assumed_power_factor_mw": one_transformer_active_capacity,
                "additional_noncritical_headroom_mw": n1_noncritical_headroom,
                "minimum_active_shedding_to_respect_nameplate_mw": n1_minimum_shedding,
                "planned_noncritical_shedding_mw": p_shed,
                "planned_shedding_exceeds_minimum_by_mw": max(0.0, p_shed - n1_minimum_shedding),
                "status": "CONDITIONAL_TRANSFER_ENVELOPE_NOT_OPERATIONALLY_VALIDATED",
            },
            "limitations": [
                "Com acoplador aberto, a carga crítica da seção associada ao transformador perdido permanece sem transferência neste modelo.",
                "O envelope agregado com acoplador fechado não prova que o equipamento ou os alimentadores suportem a transferência.",
                "A seleção de alimentadores não críticos, os estados de manobra, a rejeição automática, as interligações e a sequência de transferência continuam pendentes.",
                "Não foi modelada capacidade de sobrecarga de emergência; o limite usado é a placa de 40 MVA.",
            ],
        },
        "not_calculated": not_calculated,
        "quality_checks": checks,
        "sources": [
            {"title": "GE Vernova M-Tier MV Switchgear 3.6-17.5 kV", "url": "https://www.gevernova.com/power-conversion/sites/default/files/2025-05/GEA35559_M-Tier-MV-Switchgear-3.6kV-17.5kV-air-insulated-system_EN_20250905.pdf"},
            {"title": "IEC 62271-200:2021+AMD1:2024", "url": "https://webstore.iec.ch/en/publication/63466"},
            {"title": "WEG oil transformer 138/13.8 kV product family", "url": "https://www.weg.net/catalog/weg/BR/en/Generation%2CTransmission-and-Distribution/Oil-type-Reactors-and-Transformers/Oil-Type-Power-Transformer/3-up-to-50-MVA/Oil-Transformer-10000-12500-kVA-138-0-13-8kV-CDC-ONAF/p/14149426"},
        ],
        "limitations": [
            "All loads, ratings and the 10% transformer impedance are assumptions for the portfolio study.",
            "A calculated load current does not establish ampacity or a conductor cross-section.",
            "Transformer-only short-circuit values are sensitivity estimates, not interrupting ratings or protection settings.",
            "No regulatory compliance or construction suitability is claimed.",
        ],
    }
    _assert_finite_tree(result)
    return result


def write_outputs(
    base: dict[str, Any],
    output_dir: Path,
    model_quantities_csv: Path | None = None,
) -> dict[str, Any]:
    output_dir.mkdir(parents=True, exist_ok=True)
    result = calculate(base)
    result["model_mark_crosswalk_audit"] = (
        audit_model_mark_crosswalk(result, model_quantities_csv)
        if model_quantities_csv is not None
        else {
            "status": "NOT_VERIFIED",
            "source_file": None,
            "connectivity_verified": False,
            "scope_note": "Nenhum CSV nativo de quantitativos foi fornecido; identidade das marcas para ElementId/UniqueId não foi verificada, e este resultado não prova conectividade elétrica.",
        }
    )
    (output_dir / "resultados.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    with (output_dir / "resultados.csv").open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=["calc_id", "diagram_refs", "model_mark_refs", "model_element_ids", "model_unique_ids", "model_mark_identity_status", "quantity", "inputs", "method", "result", "unit", "source_or_assumption", "status"], delimiter=";")
        writer.writeheader()
        writer.writerows({
            **item,
            "diagram_refs": "|".join(item["diagram_refs"]),
            "model_mark_refs": "|".join(item["model_mark_refs"]),
            **_csv_model_identity_fields(item["model_mark_refs"], result["model_mark_crosswalk_audit"]),
        } for item in result["calculations"])

    lines = [
        "# Memorial de cálculo — Subestação GIS 138/13,8 kV",
        "",
        f"- Revisão: {result['revision']} | Data: {result['calculation_date']} | Estado: {result['status']}",
        "- Base: trifásica equilibrada; tensões linha-linha; MVA/kV resulta em kA.",
        "- Entrada explícita: [base-de-projeto.json](base-de-projeto.json).",
        "- `E-01/circuito` identifica o tag no unifilar; `Marca(s) BIM` contém o crosswalk para os valores do parâmetro Revit `Mark`.",
        "- O crosswalk de marcas Revit é auditado separadamente contra o CSV nativo informado na execução; presença de marca e identidade ElementId/UniqueId não comprovam conectividade elétrica.",
        "",
        "## Resultados rastreáveis",
        "",
        "| ID | E-01/circuito | Marca(s) BIM | Grandeza | Entradas | Método | Resultado | Unidade | Origem/estado |",
        "|---|---|---|---|---|---:|---|---|---|",
    ]
    for item in result["calculations"]:
        value = item["result"]
        if isinstance(value, float):
            value = f"{value:.3f}".replace(".", ",")
        diagram_refs = ", ".join(item["diagram_refs"])
        model_refs = ", ".join(item["model_mark_refs"])
        lines.append(f"| {item['calc_id']} | {diagram_refs} | {model_refs} | {item['quantity']} | {item['inputs']} | `{item['method']}` | {value} | {item['unit']} | {item['source_or_assumption']} ({item['status']}) |")
    lines.extend([
        "",
        "## Crosswalk do unifilar para as marcas BIM",
        "",
        "O crosswalk abaixo liga o identificador desenhado em E-01 à marca `Mark` do modelo. Quando informado um CSV nativo, `model_mark_crosswalk_audit` também resolve cada marca para `ElementId` e `UniqueId`; isso comprova identidade, não conectividade.",
        "",
        "| Identificador E-01/circuito | Marca(s) BIM correspondente(s) |",
        "|---|---|",
    ])
    for diagram_ref, model_refs in result["diagram_to_model_crosswalk"].items():
        lines.append(f"| {diagram_ref} | {', '.join(model_refs)} |")
    audit = result["model_mark_crosswalk_audit"]
    lines.extend(["", f"Auditoria de identidade do quantitativo Revit: `{audit['status']}`."])
    if audit.get("source_file"):
        lines.append(f"Fonte: `{audit['source_file']}` (SHA-256 `{audit['source_sha256']}`).")
        lines.extend(["", "| Marca | ElementId | UniqueId | Categoria | Família | Tipo |", "|---|---:|---|---|---|---|"])
        for mark, elements in audit["mark_to_revit_elements"].items():
            for element in elements:
                lines.append(f"| {mark} | {element['element_id']} | {element['unique_id']} | {element['category']} | {element['family']} | {element['type']} |")
        if audit["missing_marks"] or audit["duplicate_referenced_marks"] or audit["incomplete_element_identity_marks"]:
            lines.append(f"Pendências: ausentes={audit['missing_marks']}; duplicadas={audit['duplicate_referenced_marks']}; identidade incompleta={audit['incomplete_element_identity_marks']}.")
    lines.append(audit["scope_note"])
    n1 = result["n_minus_one_scenarios"]
    lines.extend([
        "",
        "## Cenários N-1 nas barras de média tensão",
        "",
        "A barra de 13,8 kV é dividida em seções A/B e o acoplador é normalmente aberto. Com o acoplador aberto, a seção associada ao transformador indisponível não recebe transferência neste modelo:",
        "",
        "| Caso | E-01/circuito | Marca(s) BIM | Seção remanescente | Crítica remanescente (MW) | Crítica isolada (MW) | Carregamento remanescente |",
        "|---|---|---|---|---:|---:|---:|",
    ])
    for scenario in n1["transformer_outage_with_tie_open"]:
        lines.append(
            f"| TR da seção {scenario['failed_transformer_section']} indisponível | {', '.join(scenario['diagram_refs'])} | {', '.join(scenario['model_mark_refs'])} | {scenario['surviving_transformer_section']} | "
            f"{scenario['critical_load_on_surviving_section_mw']:.3f} | {scenario['critical_load_on_isolated_section_mw']:.3f} | "
            f"{scenario['surviving_transformer_loading_percent']:.2f}% |"
        )
    envelope = n1["transfer_available_aggregate_envelope"]
    lines.extend([
        "",
        "Com caminho de transferência disponível, a carga crítica agregada de 32 MW representa 33,684 MVA e 84,21% da placa de um transformador. A capacidade ativa equivalente de 40 MVA a FP 0,95 é 38 MW; o envelope permite até 6 MW adicionais não críticos e calcula rejeição mínima agregada de 18 MW para respeitar a placa. O cenário com acoplador fechado é condicional e não comprova uma sequência de operação.",
        "",
        f"A premissa atual rejeita {envelope['planned_noncritical_shedding_mw']:.3f} MW não críticos, ou {envelope['planned_shedding_exceeds_minimum_by_mw']:.3f} MW além do mínimo agregado calculado. Quais alimentadores podem permanecer depende da prioridade das cargas, da comutação e da capacidade não verificada do caminho.",
        "",
        "Opções individuais de 6 MW que cabem no envelope agregado (sem priorização):",
        "",
        "| Alimentador candidato a permanecer | Marcas BIM envolvidas | Carga crítica + alimentador (MW) | Carregamento do transformador remanescente |",
        "|---|---|---:|---:|",
    ])
    for option in n1["candidate_full_noncritical_feeder_retention"]:
        lines.append(
            f"| {option['retained_feeder_tag']} | {', '.join(option['model_mark_refs'])} | {option['aggregate_active_load_mw']:.3f} | "
            f"{option['surviving_transformer_loading_percent']:.2f}% |"
        )
    lines.extend([
        "",
        "As opções são mutuamente alternativas no envelope de 6 MW e requerem transferência entre seções; nenhuma é selecionada como prioridade de operação.",
        "",
        "### Hipóteses e limites do N-1",
        "",
        *[f"- {item}" for item in n1["inputs_and_assumptions"]],
        *[f"- {item}" for item in n1["limitations"]],
    ])
    lines.extend(["", "## Itens sem cálculo numérico", "", "| ID | Grandeza | Estado | Dados faltantes |", "|---|---|---|---|"])
    for item in result["not_calculated"]:
        lines.append(f"| {item['calc_id']} | {item['quantity']} | {item['status']} | {item['missing_data']} |")
    lines.extend([
        "",
        "## Verificações de consistência interna",
        "",
    ])
    for name, value in result["quality_checks"].items():
        lines.append(f"- `{name}`: {'OK' if value else 'FALHOU'}.")
    lines.extend([
        "",
        "## Limites",
        "",
        *[f"- {item}" for item in result["limitations"]],
        "",
        "As referências de fabricante indicam opções de produtos e faixas; não são especificação deste estudo. A IEC 62271-200:2021+AMD1:2024 descreve o escopo geral de conjuntos metal-enclosed acima de 1 kV até 52 kV e até 60 Hz; confirmar requisitos ABNT e da concessionária antes de qualquer projeto executivo.",
        "",
        "Não usar para obra, compra, aprovação ou instalação.",
    ])
    (output_dir / "memorial-de-calculo.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inputs", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--model-quantities", type=Path, help="CSV de quantitativos extraído do Revit para resolver Mark -> ElementId/UniqueId")
    args = parser.parse_args()
    base = json.loads(args.inputs.read_text(encoding="utf-8"))
    result = write_outputs(base, args.output, args.model_quantities)
    print(json.dumps(result["quality_checks"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
