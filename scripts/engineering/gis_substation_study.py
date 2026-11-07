"""Traceable concept calculations for the Project 19 GIS substation.

The tool calculates only from declared study assumptions. It does not size
protective devices, certify equipment, or replace utility/network data.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
from pathlib import Path
from typing import Any


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


def calculate(base: dict[str, Any]) -> dict[str, Any]:
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
    if count < 1 or not 0 < pf <= 1 or not 0 < pfs <= 1:
        raise ValueError("Quantidade de transformadores e FP precisam estar no intervalo permitido.")
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

    s_total = p_total / pf
    q_total = math.sqrt(max(0.0, s_total**2 - p_total**2))
    s_critical = p_critical / pf
    i_total_hv = s_total / (math.sqrt(3.0) * v_hv)
    i_total_mv = s_total / (math.sqrt(3.0) * v_mv)
    i_tr_hv = rating / (math.sqrt(3.0) * v_hv)
    i_tr_mv = rating / (math.sqrt(3.0) * v_mv)
    i_critical_mv = s_critical / (math.sqrt(3.0) * v_mv)
    normal_loading = s_total / (count * rating) * 100.0
    n1_critical_loading = s_critical / rating * 100.0
    n1_full_loading = s_total / rating * 100.0
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
        _entry("BP-08", "Carregamento da carga crítica sob N-1", f"Pcrit={p_critical:g} MW; FP={pf:g}; Sr={rating:g} MVA", "(Pcrit/FP)/Sr*100", n1_critical_loading, "%", "Assume FP uniforme e sem sobrecarga de emergência"),
        _entry("BP-09", "Carregamento da carga total sob N-1", f"P={p_total:g} MW; FP={pf:g}; Sr={rating:g} MVA", "(P/FP)/Sr*100", n1_full_loading, "%", "Sem rejeição de carga; demonstra necessidade de contingência"),
        _entry("BP-10", "Carga ativa a rejeitar sob N-1", f"P={p_total:g} MW; Pcrit={p_critical:g} MW", "P-Pcrit", p_shed, "MW", "Premissa do cenário de contingência"),
        _entry("BP-11", "Corrente nominal do acoplamento/cubículo de referência", f"In de transformador={i_tr_mv*1000:.3f} A; candidato={bus_candidate_a:g} A", "In/candidato*100", i_tr_mv * 1000.0 / bus_candidate_a * 100.0, "% do candidato", "GE Vernova M-Tier lista opção de 2.000 A em 17,5 kV; candidato apenas, sujeito a dados e seleção"),
        _entry("BP-12", "Corrente de curto simplificada por transformador em 13,8 kV", f"In={i_tr_mv:.6f} kA; Z={z_pu*100:g}% assumida", "Isc=In/Zpu", isc_one, "kA", "Fonte ideal; somente impedância percentual presumida do transformador"),
        _entry("BP-13", "Corrente de curto simplificada com transformadores em paralelo", f"{count} unidades idênticas; cada contribuição={isc_one:.6f} kA", "Soma de contribuições, sem impedâncias da rede/cabos", isc_parallel, "kA", "Sensibilidade; acoplador normalmente aberto no regime normal"),
        _entry("BP-14", f"Compensação idealizada de FP {pf:g} para {pfs:g}", f"P={p_total:g} MW; FPin={pf:g}; FPout={pfs:g}", "P*(tan(acos(FPin))-tan(acos(FPout)))", q_comp, "MVAr", "Estimativa sem harmônicos, variação de carga ou estudo de ressonância"),
        _entry("BP-15", "Corrente normal em cada seção de 13,8 kV", f"P por seção={section_a_mw:g} MW; FP={pf:g}; VLL={v_mv:g} kV", "(P/FP)/(sqrt(3)*VLL)", section_current_ka, "kA", "Assume cargas iguais nas seções A/B conforme alocação declarada"),
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
        "feeders_sum_to_normal_load": math.isclose(feeder_sum, p_total, abs_tol=1e-9),
        "feeders_sum_to_critical_load": math.isclose(critical_sum, p_critical, abs_tol=1e-9),
        "critical_load_within_one_transformer_nameplate": s_critical <= rating,
        "full_load_requires_shedding_in_n_minus_one": s_total > rating,
        "normal_transformers_not_overloaded_if_equally_shared": normal_loading <= 100.0,
    }
    return {
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


def write_outputs(base: dict[str, Any], output_dir: Path) -> dict[str, Any]:
    output_dir.mkdir(parents=True, exist_ok=True)
    result = calculate(base)
    (output_dir / "resultados.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    with (output_dir / "resultados.csv").open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=["calc_id", "quantity", "inputs", "method", "result", "unit", "source_or_assumption", "status"], delimiter=";")
        writer.writeheader()
        writer.writerows(result["calculations"])

    lines = [
        "# Memorial de cálculo — Subestação GIS 138/13,8 kV",
        "",
        f"- Revisão: {result['revision']} | Data: {result['calculation_date']} | Estado: {result['status']}",
        "- Base: trifásica equilibrada; tensões linha-linha; MVA/kV resulta em kA.",
        "- Entradas: [base-de-projeto.json](base-de-projeto.json) e [base de projeto](base-de-projeto.md).",
        "",
        "## Resultados rastreáveis",
        "",
        "| ID | Grandeza | Entradas | Método | Resultado | Unidade | Origem/estado |",
        "|---|---|---|---|---:|---|---|",
    ]
    for item in result["calculations"]:
        value = item["result"]
        if isinstance(value, float):
            value = f"{value:.3f}".replace(".", ",")
        lines.append(f"| {item['calc_id']} | {item['quantity']} | {item['inputs']} | `{item['method']}` | {value} | {item['unit']} | {item['source_or_assumption']} ({item['status']}) |")
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
    args = parser.parse_args()
    base = json.loads(args.inputs.read_text(encoding="utf-8"))
    result = write_outputs(base, args.output)
    print(json.dumps(result["quality_checks"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
