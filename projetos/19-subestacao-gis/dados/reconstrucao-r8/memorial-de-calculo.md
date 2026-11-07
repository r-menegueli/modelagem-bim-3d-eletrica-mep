# Memorial de cálculo — Subestação GIS 138/13,8 kV

- Revisão: 8 | Data: 2026-11-07 | Estado: PREMISSA DE ESTUDO - NAO E PROJETO EXECUTIVO
- Base: trifásica equilibrada; tensões linha-linha; MVA/kV resulta em kA.
- Entradas: [base-de-projeto.json](base-de-projeto.json) e [base de projeto](../base-de-projeto.md).

## Resultados rastreáveis

| ID | Grandeza | Entradas | Método | Resultado | Unidade | Origem/estado |
|---|---|---|---|---:|---|---|
| BP-01 | Potência aparente total | P=56 MW; FP=0.95 | `S=P/FP` | 58,947 | MVA | Premissa de estudo em base-de-projeto.json (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-02 | Potência reativa total | S=58.947368 MVA; P=56 MW | `Q=sqrt(S^2-P^2)` | 18,406 | MVAr | Premissa de estudo em base-de-projeto.json (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-03 | Corrente de carga no sistema 138 kV | S=58.947368 MVA; VLL=138 kV | `I=S/(sqrt(3)*VLL)` | 0,247 | kA | Premissa de estudo em base-de-projeto.json (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-04 | Corrente de carga no sistema 13,8 kV | S=58.947368 MVA; VLL=13.8 kV | `I=S/(sqrt(3)*VLL)` | 2,466 | kA | Premissa de estudo em base-de-projeto.json (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-05 | Corrente nominal por transformador no lado 138 kV | Sr=40 MVA; VLL=138 kV | `In=Sr/(sqrt(3)*VLL)` | 0,167 | kA | Rating de estudo; substituir pela placa aprovada (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-06 | Corrente nominal por transformador no lado 13,8 kV | Sr=40 MVA; VLL=13.8 kV | `In=Sr/(sqrt(3)*VLL)` | 1,673 | kA | Rating de estudo; substituir pela placa aprovada (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-07 | Carregamento normal agregado | S=58.947368 MVA; capacidade instalada=80 MVA | `S/capacidade instalada*100` | 73,684 | % | Assume divisão equilibrada entre os dois transformadores (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-08 | Carregamento da carga crítica sob N-1 | Pcrit=32 MW; FP=0.95; Sr=40 MVA | `(Pcrit/FP)/Sr*100` | 84,211 | % | Assume FP uniforme e sem sobrecarga de emergência (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-09 | Carregamento da carga total sob N-1 | P=56 MW; FP=0.95; Sr=40 MVA | `(P/FP)/Sr*100` | 147,368 | % | Sem rejeição de carga; demonstra necessidade de contingência (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-10 | Carga ativa a rejeitar sob N-1 | P=56 MW; Pcrit=32 MW | `P-Pcrit` | 24,000 | MW | Premissa do cenário de contingência (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-11 | Corrente nominal do acoplamento/cubículo de referência | In de transformador=1673.479 A; candidato=2000 A | `In/candidato*100` | 83,674 | % do candidato | GE Vernova M-Tier lista opção de 2.000 A em 17,5 kV; candidato apenas, sujeito a dados e seleção (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-12 | Corrente de curto simplificada por transformador em 13,8 kV | In=1.673479 kA; Z=10% assumida | `Isc=In/Zpu` | 16,735 | kA | Fonte ideal; somente impedância percentual presumida do transformador (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-13 | Corrente de curto simplificada com transformadores em paralelo | 2 unidades idênticas; cada contribuição=16.734790 kA | `Soma de contribuições, sem impedâncias da rede/cabos` | 33,470 | kA | Sensibilidade; acoplador normalmente aberto no regime normal (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-14 | Compensação idealizada de FP 0.95 para 0.98 | P=56 MW; FPin=0.95; FPout=0.98 | `P*(tan(acos(FPin))-tan(acos(FPout)))` | 7,035 | MVAr | Estimativa sem harmônicos, variação de carga ou estudo de ressonância (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-15 | Corrente normal em cada seção de 13,8 kV | P por seção=28 MW; FP=0.95; VLL=13.8 kV | `(P/FP)/(sqrt(3)*VLL)` | 1,233 | kA | Assume cargas iguais nas seções A/B conforme alocação declarada (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| FDR-A-F01 | Corrente estimada do alimentador A-F01 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-A-F02 | Corrente estimada do alimentador A-F02 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-A-F03 | Corrente estimada do alimentador A-F03 | P=4 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,176 | kA | Carga crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-A-F04 | Corrente estimada do alimentador A-F04 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga não crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-A-F05 | Corrente estimada do alimentador A-F05 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga não crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-B-F01 | Corrente estimada do alimentador B-F01 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-B-F02 | Corrente estimada do alimentador B-F02 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-B-F03 | Corrente estimada do alimentador B-F03 | P=4 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,176 | kA | Carga crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-B-F04 | Corrente estimada do alimentador B-F04 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga não crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-B-F05 | Corrente estimada do alimentador B-F05 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga não crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |

## Itens sem cálculo numérico

| ID | Grandeza | Estado | Dados faltantes |
|---|---|---|---|
| SC-HV-01 | Curto-circuito em 138 kV | PENDING_NETWORK_EQUIVALENT | MVA de curto / impedância equivalente, X/R, configuração de fontes e tempos da concessionária |
| SC-MV-01 | Curto-circuito completo em 13,8 kV | PENDING_SOURCE_TRANSFORMER_CABLE_DATA | Rede, impedâncias reais de transformadores, motores, cabos, X/R e decaimento |
| CAB-01 | Seções de condutores e queda de tensão | PENDING_INSTALLATION_DATA | Comprimento real, método, material, temperatura, agrupamento, formação, limites de queda e curto |
| PROT-01 | Ajustes e coordenação de proteção | PENDING_NETWORK_AND_DEVICE_DATA | Curvas dos equipamentos, TCs/TPs, seletividade, tempos, rede e filosofia da concessionária |
| AUX-01 | Demanda de serviços auxiliares CA | PENDING_LOAD_REGISTER | Lista de HVAC, bombas, iluminação, tomadas, motores e regimes simultâneos |
| DC-01 | Capacidade de baterias e missão da UPS | PENDING_MISSION_PROFILE | Tensão CC escolhida, cargas contínuas, sequência de manobras, autonomia, temperatura, envelhecimento e tensão final |
| GND-01 | Malha de aterramento e tensões de toque/passo | PENDING_SOIL_AND_FAULT_DATA | Resistividade por camada, corrente/tempo de falta à terra, geometria e condições da superfície |
| XFMR-01 | Perdas, regulação e elevação térmica dos transformadores | PENDING_NAMEPLATE_AND_TEST_DATA | Perdas em vazio/carga, tensão de curto, taps, refrigeração, temperatura e relatório de ensaio |

## Verificações de consistência interna

- `feeders_sum_to_normal_load`: OK.
- `feeders_sum_to_critical_load`: OK.
- `critical_load_within_one_transformer_nameplate`: OK.
- `full_load_requires_shedding_in_n_minus_one`: OK.
- `normal_transformers_not_overloaded_if_equally_shared`: OK.

## Limites

- All loads, ratings and the 10% transformer impedance are assumptions for the portfolio study.
- A calculated load current does not establish ampacity or a conductor cross-section.
- Transformer-only short-circuit values are sensitivity estimates, not interrupting ratings or protection settings.
- No regulatory compliance or construction suitability is claimed.

As referências de fabricante indicam opções de produtos e faixas; não são especificação deste estudo. A IEC 62271-200:2021+AMD1:2024 descreve o escopo geral de conjuntos metal-enclosed acima de 1 kV até 52 kV e até 60 Hz; confirmar requisitos ABNT e da concessionária antes de qualquer projeto executivo.

Não usar para obra, compra, aprovação ou instalação.
