# Estudo complementar de cabos e curto-circuito — R9

Os resultados BP-01 a BP-15 e as correntes FDR-* da R8 permanecem inalterados. Este anexo acrescenta sensibilidades com uma resistência de cobre derivada de 100% IACS e parâmetros explicitamente assumidos.

## Queda de tensão e perdas: caso candidato

- Cabo candidato: Nexans EPR105-PVC 8,7/15 kV, 1×240 mm² Cu por fase; sem validação de ampacidade.
- R calculada a 105 °C: 0.09583 Ω/km; X=0.100 Ω/km assumido.
- Hipóteses de instalação (método, agrupamento, formação, temperatura ambiente, solo/ventilação, blindagem, terminação e limite de queda) permanecem pendentes; resultados são comparação paramétrica.

| ID | P MW | I A | L m | ΔV % | Perda Cu kW | Estado |
|---|---:|---:|---:|---:|---:|---|
| CAB-04MW-100M | 4.0 | 176.2 | 100 | 0.027 | 0.892 | Ampacidade e instalação não verificadas |
| CAB-04MW-250M | 4.0 | 176.2 | 250 | 0.068 | 2.230 | Ampacidade e instalação não verificadas |
| CAB-04MW-500M | 4.0 | 176.2 | 500 | 0.135 | 4.461 | Ampacidade e instalação não verificadas |
| CAB-06MW-100M | 6.0 | 264.2 | 100 | 0.041 | 2.007 | Ampacidade e instalação não verificadas |
| CAB-06MW-250M | 6.0 | 264.2 | 250 | 0.101 | 5.018 | Ampacidade e instalação não verificadas |
| CAB-06MW-500M | 6.0 | 264.2 | 500 | 0.203 | 10.037 | Ampacidade e instalação não verificadas |

## Curto-circuito: sensibilidade fonte–transformador–cabo

A fonte de 138 kV é representada por sua potência de curto assumida; a impedância do transformador é provisoriamente 10% na base de 40 MVA. Acrescenta-se a impedância série do cabo candidato. X/R da fonte, R/X reais do transformador, motores e contribuição de geração não estão disponíveis; valores são corrente simétrica RMS aproximada, não corrente de interrupção nem ajuste de proteção.

| Scc fonte MVA | Transformadores em paralelo | L cabo m | Icc no barramento kA | Icc remoto kA |
|---:|---:|---:|---:|---:|
| 500 | 1 | 500 | 9.297 | 8.772 |
| 500 | 2 | 500 | 12.873 | 11.880 |
| 1000 | 1 | 500 | 11.953 | 11.095 |
| 1000 | 2 | 500 | 18.594 | 16.568 |
| 5000 | 1 | 500 | 15.495 | 14.071 |
| 5000 | 2 | 500 | 28.853 | 24.170 |

## Resultados R8 mantidos

25 identificadores R8 referenciados em `resultados.json` e preservados sem sobrescrita.

## Verificações automáticas

- `all_voltage_drops_nonnegative`: OK
- `longer_route_not_lower_drop_for_same_feeder`: OK
- `greater_source_strength_not_lower_fault_current_same_case`: OK
- `two_transformers_increase_bus_fault_current`: OK
- `ampacity_not_claimed`: OK

## Limitações

- Cable ampacity is not calculated: installation method, grouping, mutual heating, ambient/soil conditions, tray fill, screen bonding and correction factors are missing.
- Cable resistance is calculated from 100% IACS copper resistivity; reactance is an explicit 0.1 ohm/km assumption, not a value supplied by Nexans for this installation.
- The cable candidate is a scenario, not a selected conductor or proof that one 240 mm2 cable per phase is adequate for the feeder current.
- Source short-circuit strengths are sensitivities because the utility source equivalent and X/R are unavailable.
- Transformer impedance remains the provisional R8 value of 10%; use actual nameplate/test impedance and tolerances for design.
- The short-circuit calculations neglect source and transformer resistance, motor contribution, converter contribution, asymmetry, decay, grounding path and protection clearing time.
- No breaker interrupting rating, protection setting, selectivity, conductor short-circuit withstand or construction suitability is established.
