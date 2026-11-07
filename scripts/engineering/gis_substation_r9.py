"""R9 scenario calculations for the GIS substation study.

These sensitivities extend (and do not replace) the R8 base calculations. They
use explicitly assumed source strength and candidate feeder cable inputs; they
are not conductor selection, protection coordination, or a short-circuit study.
"""
from __future__ import annotations

import argparse
import csv
import json
import math
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "projetos" / "19-subestacao-gis"
R8_RESULTS = PROJECT / "dados" / "reconstrucao-r8" / "resultados.json"
R9_INPUTS = PROJECT / "dados" / "reconstrucao-r9" / "estudo-r9.json"


def feeder_current_a(active_mw: float, voltage_kv: float, power_factor: float) -> float:
    return active_mw * 1_000_000 / (math.sqrt(3) * voltage_kv * 1_000 * power_factor)


def calculate(inputs: dict[str, Any], r8: dict[str, Any]) -> dict[str, Any]:
    cable = inputs["candidate_cable"]
    voltage_kv = float(inputs["medium_voltage_kv"])
    pf = float(inputs["power_factor_assumed"])
    phi = math.acos(pf)
    copper_rho_ohm_mm2_per_m = float(cable["copper_resistivity_ohm_mm2_per_m_assumed_20c"])
    alpha = float(cable["temperature_coefficient_per_c_assumed"])
    area_mm2 = float(cable["conductor_area_mm2"])
    temp_c = float(cable["conductor_temperature_c_assumed"])
    r20_ohm_km = copper_rho_ohm_mm2_per_m * 1000 / area_mm2
    r_ohm_km = r20_ohm_km * (1 + alpha * (temp_c - 20))
    x_ohm_km = float(cable["reactance_ohm_per_km_assumed"])
    feeders = [float(x) for x in inputs["feeder_active_power_mw_scenarios"]]
    lengths = [float(x) for x in inputs["route_length_km_scenarios"]]
    cable_rows: list[dict[str, Any]] = []
    for p_mw in feeders:
        current = feeder_current_a(p_mw, voltage_kv, pf)
        for length in lengths:
            drop_v = math.sqrt(3) * current * length * (r_ohm_km * pf + x_ohm_km * math.sin(phi))
            loss_w = 3 * current**2 * r_ohm_km * length
            cable_rows.append({
                "calc_id": f"CAB-{int(p_mw):02d}MW-{int(length*1000):03d}M",
                "active_power_mw": p_mw,
                "current_a": current,
                "length_km": length,
                "candidate_conductor_mm2": area_mm2,
                "conductor_r_at_assumed_temperature_ohm_km": r_ohm_km,
                "reactance_ohm_km_assumed": x_ohm_km,
                "voltage_drop_v": drop_v,
                "voltage_drop_percent": 100 * drop_v / (voltage_kv * 1000),
                "three_phase_copper_loss_kw": loss_w / 1000,
                "status": "SENSITIVITY_ONLY_AMPACITY_AND_INSTALLATION_UNCHECKED",
            })

    source_rows: list[dict[str, Any]] = []
    sbase_mva = float(inputs["short_circuit_sensitivity"]["transformer_base_mva"])
    zt_pu = float(inputs["short_circuit_sensitivity"]["transformer_z_pu_assumed"])
    zbase_ohm = voltage_kv**2 / sbase_mva
    ibase_a = sbase_mva * 1_000_000 / (math.sqrt(3) * voltage_kv * 1000)
    source_mv_a = [float(x) for x in inputs["short_circuit_sensitivity"]["source_short_circuit_mva_assumed"]]
    for source_mva in source_mv_a:
        zs_pu = sbase_mva / source_mva
        for transformer_count in (1, 2):
            # Source impedance is common; identical transformer impedances are
            # parallel only when the operating case explicitly closes the tie.
            zxf_pu = zt_pu / transformer_count
            bus_current = ibase_a / (zs_pu + zxf_pu)
            for length in lengths:
                r_pu = (r_ohm_km * length) / zbase_ohm
                x_pu = (x_ohm_km * length) / zbase_ohm
                remote_current = ibase_a / math.sqrt(r_pu**2 + (zs_pu + zxf_pu + x_pu)**2)
                source_rows.append({
                    "calc_id": f"SC-{int(source_mva):04d}MVA-{transformer_count}TR-{int(length*1000):03d}M",
                    "source_short_circuit_mva_assumed": source_mva,
                    "transformers_in_parallel_assumed": transformer_count,
                    "transformer_z_pu_assumed": zt_pu,
                    "cable_length_km_assumed": length,
                    "bus_fault_current_ka_symmetric_rms": bus_current / 1000,
                    "remote_fault_current_ka_symmetric_rms": remote_current / 1000,
                    "status": "SOURCE_AND_CABLE_SENSITIVITY_R_AND_XR_MISSING",
                })
    source_values = sorted(source_mv_a)

    return {
        "project": "19 - Subestacao GIS 138/13,8 kV",
        "revision": "R9",
        "status": "ESTUDO_DE_SENSIBILIDADE - NAO_E_DIMENSIONAMENTO_EXECUTIVO",
        "preserved_r8_calculation_ids": [x["calc_id"] for x in r8["calculations"]],
        "inputs": inputs,
        "cable_sensitivities": cable_rows,
        "short_circuit_sensitivities": source_rows,
        "quality_checks": {
            "all_voltage_drops_nonnegative": all(x["voltage_drop_percent"] >= 0 for x in cable_rows),
            "longer_route_not_lower_drop_for_same_feeder": all(
                cable_rows[i + 1]["voltage_drop_percent"] >= cable_rows[i]["voltage_drop_percent"]
                for i in range(0, len(cable_rows), len(lengths))
            ),
            "greater_source_strength_not_lower_fault_current_same_case": all(
                next(x for x in source_rows if x["source_short_circuit_mva_assumed"] == source_values[j + 1]
                     and x["transformers_in_parallel_assumed"] == transformer_count
                     and x["cable_length_km_assumed"] == length)["bus_fault_current_ka_symmetric_rms"]
                >= next(x for x in source_rows if x["source_short_circuit_mva_assumed"] == source_values[j]
                        and x["transformers_in_parallel_assumed"] == transformer_count
                        and x["cable_length_km_assumed"] == length)["bus_fault_current_ka_symmetric_rms"]
                for j in range(len(source_values) - 1)
                for transformer_count in (1, 2)
                for length in lengths
            ),
            "two_transformers_increase_bus_fault_current": all(
                next(x for x in source_rows if x["source_short_circuit_mva_assumed"] == source and x["transformers_in_parallel_assumed"] == 2 and x["cable_length_km_assumed"] == lengths[0])["bus_fault_current_ka_symmetric_rms"]
                > next(x for x in source_rows if x["source_short_circuit_mva_assumed"] == source and x["transformers_in_parallel_assumed"] == 1 and x["cable_length_km_assumed"] == lengths[0])["bus_fault_current_ka_symmetric_rms"]
                for source in source_mv_a
            ),
            "ampacity_not_claimed": all("AMPACITY" in x["status"] for x in cable_rows),
        },
        "limitations": inputs["limitations"],
    }


def write_outputs(inputs: dict[str, Any], r8: dict[str, Any], output_dir: Path) -> dict[str, Any]:
    output_dir.mkdir(parents=True, exist_ok=True)
    result = calculate(inputs, r8)
    (output_dir / "estudo-complementar-r9.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    rows = result["cable_sensitivities"] + result["short_circuit_sensitivities"]
    fields = sorted({k for row in rows for k in row})
    with (output_dir / "estudo-complementar-r9.csv").open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, delimiter=";")
        writer.writeheader()
        writer.writerows(rows)
    md = [
        "# Estudo complementar de cabos e curto-circuito — R9", "",
        "Os resultados BP-01 a BP-15 e as correntes FDR-* da R8 permanecem inalterados. Este anexo acrescenta sensibilidades com uma resistência de cobre derivada de 100% IACS e parâmetros explicitamente assumidos.", "",
        "## Queda de tensão e perdas: caso candidato", "",
        f"- Cabo candidato: Nexans EPR105-PVC 8,7/15 kV, 1×{inputs['candidate_cable']['conductor_area_mm2']:.0f} mm² Cu por fase; sem validação de ampacidade.",
        f"- R calculada a {inputs['candidate_cable']['conductor_temperature_c_assumed']:.0f} °C: {result['cable_sensitivities'][0]['conductor_r_at_assumed_temperature_ohm_km']:.5f} Ω/km; X={inputs['candidate_cable']['reactance_ohm_per_km_assumed']:.3f} Ω/km assumido.",
        "- Hipóteses de instalação (método, agrupamento, formação, temperatura ambiente, solo/ventilação, blindagem, terminação e limite de queda) permanecem pendentes; resultados são comparação paramétrica.", "",
        "| ID | P MW | I A | L m | ΔV % | Perda Cu kW | Estado |", "|---|---:|---:|---:|---:|---:|---|",
    ]
    for x in result["cable_sensitivities"]:
        md.append(f"| {x['calc_id']} | {x['active_power_mw']:.1f} | {x['current_a']:.1f} | {x['length_km']*1000:.0f} | {x['voltage_drop_percent']:.3f} | {x['three_phase_copper_loss_kw']:.3f} | Ampacidade e instalação não verificadas |")
    md += ["", "## Curto-circuito: sensibilidade fonte–transformador–cabo", "", "A fonte de 138 kV é representada por sua potência de curto assumida; a impedância do transformador é provisoriamente 10% na base de 40 MVA. Acrescenta-se a impedância série do cabo candidato. X/R da fonte, R/X reais do transformador, motores e contribuição de geração não estão disponíveis; valores são corrente simétrica RMS aproximada, não corrente de interrupção nem ajuste de proteção.", "", "| Scc fonte MVA | Transformadores em paralelo | L cabo m | Icc no barramento kA | Icc remoto kA |", "|---:|---:|---:|---:|---:|"]
    max_length_km = max(float(x) for x in inputs["route_length_km_scenarios"])
    for x in result["short_circuit_sensitivities"]:
        if x["cable_length_km_assumed"] == max_length_km:
            md.append(f"| {x['source_short_circuit_mva_assumed']:.0f} | {x['transformers_in_parallel_assumed']} | {x['cable_length_km_assumed']*1000:.0f} | {x['bus_fault_current_ka_symmetric_rms']:.3f} | {x['remote_fault_current_ka_symmetric_rms']:.3f} |")
    md += ["", "## Resultados R8 mantidos", "", f"{len(result['preserved_r8_calculation_ids'])} identificadores R8 referenciados em `resultados.json` e preservados sem sobrescrita.", "", "## Verificações automáticas", ""]
    md += [f"- `{k}`: {'OK' if v else 'FALHOU'}" for k, v in result["quality_checks"].items()]
    md += ["", "## Limitações", ""] + [f"- {x}" for x in result["limitations"]] + [""]
    (output_dir / "memorial-complementar-r9.md").write_text("\n".join(md), encoding="utf-8")
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inputs", type=Path, default=R9_INPUTS)
    parser.add_argument("--r8-results", type=Path, default=R8_RESULTS)
    parser.add_argument("--output", type=Path, default=PROJECT / "dados" / "reconstrucao-r9")
    args = parser.parse_args()
    result = write_outputs(json.loads(args.inputs.read_text(encoding="utf-8")), json.loads(args.r8_results.read_text(encoding="utf-8")), args.output)
    print(json.dumps(result["quality_checks"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
