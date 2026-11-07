"""Auditable preliminary calculations for Project 19 (GIS substation).

The calculations are deliberately limited to the declared study inputs. They
do not constitute a network short-circuit, protection, grounding, or standards
compliance study.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
from dataclasses import asdict, dataclass
from datetime import date
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class StudyInput:
    project: str
    date: str
    primary_voltage_kv: float
    secondary_voltage_kv: float
    active_load_mw: float
    load_power_factor: float
    critical_active_load_mw: float
    transformer_count: int
    transformer_rating_mva: float
    transformer_impedance_pu: float
    target_power_factor: float
    frequency_hz: float
    basis: str


def calculate(data: StudyInput) -> dict[str, Any]:
    if data.primary_voltage_kv <= 0 or data.secondary_voltage_kv <= 0:
        raise ValueError("Tensões devem ser positivas.")
    if not 0 < data.load_power_factor <= 1 or not 0 < data.target_power_factor <= 1:
        raise ValueError("Fatores de potência devem estar no intervalo (0, 1].")
    if data.transformer_count < 1 or data.transformer_rating_mva <= 0:
        raise ValueError("Quantidade e potência dos transformadores devem ser positivas.")
    if not 0 < data.transformer_impedance_pu:
        raise ValueError("A impedância percentual por unidade deve ser positiva.")
    if data.active_load_mw < 0 or data.critical_active_load_mw < 0:
        raise ValueError("Cargas ativas não podem ser negativas.")

    p = data.active_load_mw
    pf = data.load_power_factor
    s = p / pf
    q = math.sqrt(max(0.0, s * s - p * p))
    p_critical = data.critical_active_load_mw
    s_critical = p_critical / pf
    q_critical = math.sqrt(max(0.0, s_critical * s_critical - p_critical * p_critical))

    def current_ka(apparent_mva: float, voltage_kv: float) -> float:
        return apparent_mva / (math.sqrt(3.0) * voltage_kv)

    transformer_total = data.transformer_count * data.transformer_rating_mva
    transformer_primary_current_ka = current_ka(data.transformer_rating_mva, data.primary_voltage_kv)
    transformer_secondary_current_ka = current_ka(data.transformer_rating_mva, data.secondary_voltage_kv)
    normal_loading_pct = s / transformer_total * 100.0
    n_minus_one_critical_loading_pct = s_critical / data.transformer_rating_mva * 100.0
    n_minus_one_full_load_pct = s / data.transformer_rating_mva * 100.0

    source_tan = math.tan(math.acos(pf))
    target_tan = math.tan(math.acos(data.target_power_factor))
    compensation_mvar = max(0.0, p * (source_tan - target_tan))

    # Transformer-only, ideal-primary-source estimate. The same transformer
    # impedance base is used for each unit; two identical units in parallel
    # double the contribution at the LV bus.
    one_transformer_fault_ka = transformer_secondary_current_ka / data.transformer_impedance_pu
    parallel_fault_ka = one_transformer_fault_ka * data.transformer_count

    return {
        "project": data.project,
        "calculation_date": data.date,
        "status": "ESTUDO PRELIMINAR COM PREMISSAS",
        "inputs": asdict(data),
        "results": {
            "normal_load": {
                "apparent_mva": s,
                "reactive_mvar": q,
                "current_primary_ka": current_ka(s, data.primary_voltage_kv),
                "current_secondary_ka": current_ka(s, data.secondary_voltage_kv),
                "installed_transformer_capacity_mva": transformer_total,
                "transformer_loading_pct": normal_loading_pct,
            },
            "transformer_ratings": {
                "primary_current_each_ka": transformer_primary_current_ka,
                "secondary_current_each_ka": transformer_secondary_current_ka,
            },
            "n_minus_one": {
                "critical_apparent_mva": s_critical,
                "critical_reactive_mvar": q_critical,
                "critical_current_secondary_ka": current_ka(s_critical, data.secondary_voltage_kv),
                "one_transformer_critical_loading_pct": n_minus_one_critical_loading_pct,
                "one_transformer_full_load_pct": n_minus_one_full_load_pct,
                "critical_load_within_nameplate": n_minus_one_critical_loading_pct <= 100.0,
                "full_load_within_nameplate": n_minus_one_full_load_pct <= 100.0,
            },
            "power_factor_correction": {
                "target_power_factor": data.target_power_factor,
                "estimated_compensation_mvar": compensation_mvar,
            },
            "secondary_fault_estimate": {
                "transformer_only_one_unit_ka": one_transformer_fault_ka,
                "transformer_only_parallel_units_ka": parallel_fault_ka,
                "method": "Isc = In/Zpu; primary source assumed ideal; transformer impedance only",
            },
        },
        "not_calculated": [
            "curto-circuito em 138 kV e impedância equivalente da rede",
            "curto-circuito completo em 13,8 kV com rede, motores e cabos",
            "X/R, componente assimétrica, capacidade de interrupção e coordenação de proteção",
            "perdas e elevação térmica dos transformadores (sem dados de fabricante)",
            "queda de tensão, cabos, aterramento, toque/passo e malha de terra",
            "perfil de carga, energia mensal e contingência de duração definida",
        ],
    }


def _fmt(value: float, digits: int = 3) -> str:
    return f"{value:.{digits}f}".replace(".", ",")


def write_outputs(data: StudyInput, output_dir: Path) -> dict[str, Any]:
    output_dir.mkdir(parents=True, exist_ok=True)
    result = calculate(data)
    json_path = output_dir / "resultados.json"
    csv_path = output_dir / "resultados.csv"
    memo_path = output_dir / "memorial-de-calculo.md"
    json_path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    rows = []
    for group, values in result["results"].items():
        for key, value in values.items():
            rows.append((group, key, value))
    with csv_path.open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.writer(stream, delimiter=";")
        writer.writerow(["grupo", "grandeza", "valor", "unidade/observação"])
        for group, key, value in rows:
            unit = "booleano" if isinstance(value, bool) else ("texto" if isinstance(value, str) else "ver chave")
            writer.writerow([group, key, value, unit])

    n = result["results"]["normal_load"]
    rating = result["results"]["transformer_ratings"]
    n1 = result["results"]["n_minus_one"]
    pf = result["results"]["power_factor_correction"]
    fault = result["results"]["secondary_fault_estimate"]
    memo = f"""# Memorial de cálculo — Projeto 19: Subestação GIS

- Revisão: A | Data do cálculo: {data.date} | Estado: estudo preliminar com premissas
- Base de potência: trifásica equilibrada; valores em SI; tensão linha-linha.

## Entradas e origem

Os valores abaixo são premissas de estudo criadas para este exercício; não são dados de concessionária, fabricante ou levantamento de campo.

| Grandeza | Valor |
|---|---:|
| Tensão primária | {data.primary_voltage_kv:g} kV |
| Tensão secundária | {data.secondary_voltage_kv:g} kV |
| Carga ativa agregada | {data.active_load_mw:g} MW |
| Fator de potência de carga | {data.load_power_factor:g} |
| Carga ativa crítica | {data.critical_active_load_mw:g} MW |
| Transformadores | {data.transformer_count} × {data.transformer_rating_mva:g} MVA |
| Impedância de placa adotada | {data.transformer_impedance_pu * 100:g}% por unidade |
| FP-alvo para estimativa de compensação | {data.target_power_factor:g} |

Base da premissa: {data.basis}

## Metodologia implementada

- Potência aparente: `S = P / FP`.
- Potência reativa: `Q = sqrt(S² - P²)`.
- Corrente trifásica: `I = S / (sqrt(3) × V_LL)`; MVA/kV resulta em kA.
- Carregamento agregado: `S_carga / soma das potências nominais`.
- Cenário N-1: compara a carga crítica e a carga total, separadamente, com a potência nominal de um transformador.
- Compensação aproximada: `Qc = P × [tan(acos(FP_inicial)) - tan(acos(FP_alvo))]`.
- Estimativa de curto no secundário: `Isc = In / Zpu`, com fonte primária ideal e somente a impedância dos transformadores. Para unidades idênticas em paralelo, soma as contribuições.

## Resultados

| Resultado | Valor |
|---|---:|
| Carga aparente normal | {_fmt(n['apparent_mva'])} MVA |
| Carga reativa normal | {_fmt(n['reactive_mvar'])} MVAr |
| Corrente da carga em 138 kV | {_fmt(n['current_primary_ka'])} kA |
| Corrente da carga em 13,8 kV | {_fmt(n['current_secondary_ka'])} kA |
| Corrente nominal primária por transformador | {_fmt(rating['primary_current_each_ka'])} kA |
| Corrente nominal secundária por transformador | {_fmt(rating['secondary_current_each_ka'])} kA |
| Carregamento normal dos dois transformadores | {_fmt(n['transformer_loading_pct'], 1)}% |
| N-1: carga crítica / um transformador | {_fmt(n1['one_transformer_critical_loading_pct'], 1)}% |
| N-1: carga total sem rejeição / um transformador | {_fmt(n1['one_transformer_full_load_pct'], 1)}% |
| Compensação reativa estimada para FP {data.target_power_factor:g} | {_fmt(pf['estimated_compensation_mvar'])} MVAr |
| Curto 13,8 kV, uma unidade, estimativa simplificada | {_fmt(fault['transformer_only_one_unit_ka'])} kA |
| Curto 13,8 kV, duas unidades em paralelo, estimativa simplificada | {_fmt(fault['transformer_only_parallel_units_ka'])} kA |

O cálculo indica que o bloco crítico cabe na potência nominal de um transformador sob a premissa adotada; manter a carga total com apenas uma unidade exigiria rejeição de carga ou outra fonte, pois excede a potência nominal. A estimativa de curto não é base de ajuste ou seleção de disjuntor.

## Referências técnicas a revisar no projeto executivo

- ABNT NBR 14039:2021 (faixa indicada no título: 1,0 kV a 36,2 kV), para o sistema de 13,8 kV; confirmar versão vigente e escopo completo no catálogo ABNT.
- ABNT NBR 5419:2026, partes 1 a 4, para avaliação de proteção contra descargas; confirmar aplicabilidade ao sítio e versão vigente no catálogo ABNT.
- IEC 62271-203:2022 para GIS acima de 52 kV; IEC 62271-200:2021+AMD1:2024 para cubículos de 13,8 kV; IEC 62271-211:2024 para conexão direta GIS-transformador, se aplicável ao arranjo selecionado.

As referências são trilhas de verificação, não declaração de conformidade. A edição e o texto aplicável devem ser confirmados pelo responsável técnico antes de projeto executivo.

## Limitações e pendências

{chr(10).join('- ' + item.capitalize() + '.' for item in result['not_calculated'])}

Não foram definidos ajustes de proteção, classe de exatidão, TC/TP, sequência de manobra, capacidade interruptiva, nível de isolamento, malha de terra, fundações, ventilação, incêndio, ruído, óleo, gás isolante ou requisitos ambientais. Não usar para obra, compra, aprovação ou instalação.
"""
    memo_path.write_text(memo, encoding="utf-8")
    return result


def read_inputs(path: Path) -> StudyInput:
    raw = json.loads(path.read_text(encoding="utf-8"))
    return StudyInput(**raw)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inputs", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = write_outputs(read_inputs(args.inputs), args.output)
    print(json.dumps(result["results"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
