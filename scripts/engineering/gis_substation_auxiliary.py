"""Explicit-assumption preliminary AC, DC battery, and UPS study for Project 19."""

from __future__ import annotations

import argparse
import csv
import json
import math
from pathlib import Path
from typing import Any


def calculate(base: dict[str, Any]) -> dict[str, Any]:
    revision_tag = str(base.get("revision", "R1")).lower()
    ac = base["ac_system"]
    dc = base["dc_system"]
    ups = base["ups_system"]
    voltage = float(ac["voltage_v_line_to_line_assumed"])
    pf = float(ac["power_factor_assumed"])
    ac_rating = float(ac["installed_transformer_candidate_kva"])
    max_utilization = float(ac["utilization_limit_assumed"])
    dc_voltage = float(dc["nominal_voltage_v_assumed"])
    autonomy = float(dc["autonomy_h_assumed"])
    ups_pf = float(ups["power_factor_assumed"])

    positive = [voltage, pf, ac_rating, max_utilization, dc_voltage, autonomy, ups_pf]
    if any(value <= 0 for value in positive) or pf > 1 or ups_pf > 1 or max_utilization > 1:
        raise ValueError("Tensões, fatores de potência, autonomia e capacidade devem ser positivos e válidos.")

    ac_rows = []
    connected_kw = 0.0
    demand_kw = 0.0
    seen: set[str] = set()
    for load in ac["loads"]:
        load_id = str(load["id"])
        quantity = float(load["quantity"])
        unit_kw = float(load["unit_power_kw_assumed"])
        demand_factor = float(load["demand_factor_assumed"])
        if load_id in seen or quantity < 0 or unit_kw < 0 or not 0 <= demand_factor <= 1:
            raise ValueError(f"Carga auxiliar inválida ou duplicada: {load_id}")
        seen.add(load_id)
        connected = quantity * unit_kw
        demanded = connected * demand_factor
        connected_kw += connected
        demand_kw += demanded
        ac_rows.append({
            "calc_id": load_id,
            "quantity": load["description"],
            "inputs": f"quantidade={quantity:g}; P_unit={unit_kw:g} kW; fator_demanda={demand_factor:g}",
            "method": "P_demanda=quantidade*P_unit*fator_demanda",
            "result": demanded,
            "unit": "kW",
            "source_or_assumption": load["basis"],
            "status": "ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS",
        })

    ac_kva = demand_kw / pf
    ac_current = ac_kva * 1000.0 / (math.sqrt(3.0) * voltage)
    ac_utilization = ac_kva / ac_rating * 100.0

    continuous_dc_ah = float(dc["continuous_load_kw_assumed"]) * 1000.0 * autonomy / dc_voltage
    trip_dc_ah = (
        float(dc["trip_coil_current_a_assumed"])
        * float(dc["trip_duration_s_assumed"])
        * int(dc["emergency_operations_assumed"])
        / 3600.0
    )
    dc_raw_ah = continuous_dc_ah + trip_dc_ah
    dc_factors = (
        float(dc["design_margin_factor_assumed"])
        * float(dc["aging_factor_assumed"])
        * float(dc["temperature_factor_assumed"])
        / (float(dc["depth_of_discharge_assumed"]) * float(dc["discharge_efficiency_assumed"]))
    )
    dc_required_ah = dc_raw_ah * dc_factors
    dc_candidate_ah = math.ceil(dc_required_ah / 10.0) * 10.0
    dc_load_a = float(dc["continuous_load_kw_assumed"]) * 1000.0 / dc_voltage
    dc_recharge_a = dc_candidate_ah / float(dc["charger_recovery_h_assumed"])
    charger_kw = (dc_load_a + dc_recharge_a) * dc_voltage / 1000.0

    ups_kw = float(ups["critical_load_kw_assumed"])
    ups_kva = ups_kw / ups_pf
    ups_utilization = ups_kva / float(ups["candidate_ups_rating_kva"]) * 100.0
    ups_energy_kwh = (
        ups_kw
        * float(ups["autonomy_h_assumed"])
        * float(ups["design_margin_factor_assumed"])
        * float(ups["aging_factor_assumed"])
        * float(ups["temperature_factor_assumed"])
        / (float(ups["depth_of_discharge_assumed"]) * float(ups["discharge_efficiency_assumed"]))
    )

    results = ac_rows + [
        {"calc_id": "AUX-AC-10", "quantity": "Potência conectada CA estimada", "inputs": f"soma das cargas conectadas={connected_kw:.6f} kW", "method": "soma(quantidade*P_unit)", "result": connected_kw, "unit": "kW", "source_or_assumption": f"Entradas em servicos-auxiliares-{revision_tag}.json", "status": "ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS"},
        {"calc_id": "AUX-AC-11", "quantity": "Demanda ativa CA estimada", "inputs": f"cargas={len(ac_rows)}; fatores de demanda por linha", "method": "soma(quantidade*P_unit*fator_demanda)", "result": demand_kw, "unit": "kW", "source_or_assumption": "Estimativa; lista de cargas e simultaneidade reais pendentes", "status": "ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS"},
        {"calc_id": "AUX-AC-12", "quantity": "Demanda aparente CA estimada", "inputs": f"P={demand_kw:.6f} kW; FP={pf:g}", "method": "S=P/FP", "result": ac_kva, "unit": "kVA", "source_or_assumption": "FP e cargas assumidos; estudo de cargas pendente", "status": "ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS"},
        {"calc_id": "AUX-AC-13", "quantity": "Corrente trifásica CA estimada", "inputs": f"S={ac_kva:.6f} kVA; VLL={voltage:g} V", "method": "I=S/(sqrt(3)*VLL)", "result": ac_current, "unit": "A", "source_or_assumption": "Tensão auxiliar de 380 V assumida; confirmar arquitetura real", "status": "ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS"},
        {"calc_id": "AUX-AC-14", "quantity": "Utilização do candidato de transformador auxiliar", "inputs": f"S={ac_kva:.6f} kVA; candidato={ac_rating:g} kVA", "method": "S_demanda/S_candidato*100", "result": ac_utilization, "unit": "%", "source_or_assumption": "100 kVA é candidato ilustrativo, sem especificação", "status": "CANDIDATE_CHECK_ONLY"},
        {"calc_id": "DC-01", "quantity": "Carga contínua equivalente da bateria", "inputs": f"P={dc['continuous_load_kw_assumed']:g} kW; autonomia={autonomy:g} h; V={dc_voltage:g} V", "method": "Ah=P*1000*h/V", "result": continuous_dc_ah, "unit": "Ah", "source_or_assumption": dc["basis"], "status": "ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS"},
        {"calc_id": "DC-02", "quantity": "Energia equivalente de operações de emergência", "inputs": f"I={dc['trip_coil_current_a_assumed']:g} A; t={dc['trip_duration_s_assumed']:g} s; operações={dc['emergency_operations_assumed']}", "method": "Ah=I*t*n/3600", "result": trip_dc_ah, "unit": "Ah", "source_or_assumption": "Corrente, duração e número de operações assumidos", "status": "ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS"},
        {"calc_id": "DC-03", "quantity": "Capacidade corrigida requerida para bateria 125 Vcc", "inputs": f"Ah_base={dc_raw_ah:.6f}; fatores combinados={dc_factors:.6f}", "method": "Ah_corrigido=(Ah_contínuo+Ah_manobras)*margem*envelhecimento*temperatura/(DoD*eficiência)", "result": dc_required_ah, "unit": "Ah", "source_or_assumption": "Não inclui curva de descarga, perfil de cargas ou catálogo de célula", "status": "ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS"},
        {"calc_id": "DC-04", "quantity": "Capacidade nominal candidata da bateria", "inputs": f"requerida={dc_required_ah:.6f} Ah; arredondamento de estudo para 10 Ah", "method": "arredondar para cima ao próximo múltiplo de 10 Ah", "result": dc_candidate_ah, "unit": "Ah", "source_or_assumption": "Valor de capacidade não seleciona química, tensão final ou fabricante", "status": "CANDIDATE_CHECK_ONLY"},
        {"calc_id": "DC-05", "quantity": "Corrente de carregador candidata para recuperação", "inputs": f"carga contínua={dc_load_a:.6f} A; recarga={dc_recharge_a:.6f} A; recuperação={dc['charger_recovery_h_assumed']:g} h", "method": "I_charger=I_contínua+Ah_candidato/tempo_recuperação", "result": dc_load_a + dc_recharge_a, "unit": "A", "source_or_assumption": "Sem perfil de carga, curva de carga ou redundância especificados", "status": "CANDIDATE_CHECK_ONLY"},
        {"calc_id": "DC-06", "quantity": "Potência CC de saída para o carregador candidato", "inputs": f"V={dc_voltage:g} V; I={dc_load_a+dc_recharge_a:.6f} A", "method": "P=V*I", "result": charger_kw, "unit": "kW", "source_or_assumption": "Potência de saída CC; rendimento de CA e seleção de equipamento pendentes", "status": "CANDIDATE_CHECK_ONLY"},
        {"calc_id": "UPS-01", "quantity": "Demanda aparente da carga crítica UPS", "inputs": f"P={ups_kw:g} kW; FP={ups_pf:g}", "method": "S=P/FP", "result": ups_kva, "unit": "kVA", "source_or_assumption": ups["basis"], "status": "ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS"},
        {"calc_id": "UPS-02", "quantity": "Utilização do candidato UPS", "inputs": f"S={ups_kva:.6f} kVA; candidato={ups['candidate_ups_rating_kva']:g} kVA", "method": "S_demanda/S_candidato*100", "result": ups_utilization, "unit": "%", "source_or_assumption": "Candidato de 3 kVA sem catálogo ou fator de crista verificado", "status": "CANDIDATE_CHECK_ONLY"},
        {"calc_id": "UPS-03", "quantity": "Energia nominal mínima preliminar da bateria UPS", "inputs": f"P={ups_kw:g} kW; autonomia={ups['autonomy_h_assumed']:g} h; fatores explícitos", "method": "E=P*h*margem*envelhecimento*temperatura/(DoD*eficiência)", "result": ups_energy_kwh, "unit": "kWh", "source_or_assumption": "Energia apenas; tensão CC, curvas, eficiência por carga e catálogo pendentes", "status": "ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS"},
    ]
    checks = {
        "ac_candidate_below_assumed_utilization_limit": ac_utilization <= max_utilization * 100.0,
        "dc_candidate_capacity_covers_calculated_study_capacity": dc_candidate_ah >= dc_required_ah,
        "charger_candidate_power_covers_calculated_dc_demand": 5.0 >= charger_kw,
        "ups_candidate_rating_covers_study_apparent_load": float(ups["candidate_ups_rating_kva"]) >= ups_kva,
    }
    return {
        "project": base["project"],
        "revision": base["revision"],
        "date": base["date"],
        "status": base["status"],
        "inputs": base,
        "calculations": results,
        "quality_checks": checks,
        "limitations": base["limitations"],
    }


def write_outputs(base: dict[str, Any], output_dir: Path) -> dict[str, Any]:
    output_dir.mkdir(parents=True, exist_ok=True)
    result = calculate(base)
    revision_tag = str(base.get("revision", "R1")).lower()
    (output_dir / f"servicos-auxiliares-resultados-{revision_tag}.json").write_text(
        json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    with (output_dir / f"servicos-auxiliares-resultados-{revision_tag}.csv").open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=["calc_id", "quantity", "inputs", "method", "result", "unit", "source_or_assumption", "status"], delimiter=";")
        writer.writeheader()
        writer.writerows(result["calculations"])
    lines = [
        "# Memorial preliminar de serviços auxiliares — Projeto 19",
        "",
        f"- Revisão: {result['revision']} | Estado: {result['status']}",
        f"- Entradas editáveis: [servicos-auxiliares-{revision_tag}.json](../servicos-auxiliares-{revision_tag}.json).",
        f"- Resultados rastreáveis: [JSON](servicos-auxiliares-resultados-{revision_tag}.json) e [CSV](servicos-auxiliares-resultados-{revision_tag}.csv).",
        "",
        "## Resultados",
        "",
        "| ID | Grandeza | Método | Resultado | Unidade | Estado / hipótese |",
        "|---|---|---|---:|---|---|",
    ]
    for item in result["calculations"]:
        lines.append(f"| {item['calc_id']} | {item['quantity']} | `{item['method']}` | {item['result']:.3f} | {item['unit']} | {item['status']}; {item['source_or_assumption']} |")
    lines.extend(["", "## Verificações internas da hipótese", ""])
    lines.extend(f"- `{name}`: {'OK' if value else 'FALHOU'}" for name, value in result["quality_checks"].items())
    lines.extend(["", "## Limites", "", *[f"- {item}" for item in result["limitations"]], ""])
    (output_dir / f"memorial-servicos-auxiliares-{revision_tag}.md").write_text("\n".join(lines), encoding="utf-8")
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
