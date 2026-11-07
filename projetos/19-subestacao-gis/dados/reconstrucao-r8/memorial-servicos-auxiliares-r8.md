# Memorial preliminar de serviços auxiliares — Projeto 19

- Revisão: R8 | Estado: ESTIMATIVA_DE_ESTUDO - nao usar para especificacao ou compra
- Entradas editáveis: [servicos-auxiliares-r8.json](../servicos-auxiliares-r8.json).
- Resultados rastreáveis: [JSON](servicos-auxiliares-resultados-r8.json) e [CSV](servicos-auxiliares-resultados-r8.csv).

## Resultados

| ID | Grandeza | Método | Resultado | Unidade | Estado / hipótese |
|---|---|---|---:|---|---|
| AUX-AC-01 | HVAC de salas técnicas | `P_demanda=quantidade*P_unit*fator_demanda` | 35.000 | kW | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Estimativa de estudo; carga térmica e datasheets pendentes |
| AUX-AC-02 | Iluminação interna | `P_demanda=quantidade*P_unit*fator_demanda` | 7.291 | kW | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Área construída estimada 1302 m² x 7 W/m²; projeto luminotécnico pendente |
| AUX-AC-03 | Iluminação de emergência | `P_demanda=quantidade*P_unit*fator_demanda` | 0.977 | kW | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Área construída estimada 1302 m² x 1,5 W/m²; autonomia e fotometria pendentes |
| AUX-AC-04 | Iluminação externa e vias | `P_demanda=quantidade*P_unit*fator_demanda` | 1.120 | kW | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Área de pátio iluminada estimada 2800 m² x 0,5 W/m²; estudo fotométrico pendente |
| AUX-AC-05 | Ventilação e exaustão | `P_demanda=quantidade*P_unit*fator_demanda` | 4.400 | kW | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Potência unitária e simultaneidade assumidas; levantamento térmico pendente |
| AUX-AC-06 | Painéis de proteção e controle | `P_demanda=quantidade*P_unit*fator_demanda` | 1.650 | kW | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Potência unitária assumida; listas de cargas dos IEDs pendentes |
| AUX-AC-07 | Telecomunicações e supervisão | `P_demanda=quantidade*P_unit*fator_demanda` | 2.000 | kW | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Carga concentrada assumida; arquitetura e datasheets pendentes |
| AUX-AC-08 | Carregador do sistema 125 Vcc | `P_demanda=quantidade*P_unit*fator_demanda` | 4.000 | kW | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Candidato derivado da estimativa de recuperação da bateria; topologia e fabricante pendentes |
| AUX-AC-09 | Carga crítica alimentada por UPS | `P_demanda=quantidade*P_unit*fator_demanda` | 1.500 | kW | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Estimativa de estudo; lista crítica e datasheets pendentes |
| AUX-AC-10 | Potência conectada CA estimada | `soma(quantidade*P_unit)` | 81.417 | kW | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Entradas em servicos-auxiliares-r8.json |
| AUX-AC-11 | Demanda ativa CA estimada | `soma(quantidade*P_unit*fator_demanda)` | 57.938 | kW | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Estimativa; lista de cargas e simultaneidade reais pendentes |
| AUX-AC-12 | Demanda aparente CA estimada | `S=P/FP` | 64.375 | kVA | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; FP e cargas assumidos; estudo de cargas pendente |
| AUX-AC-13 | Corrente trifásica CA estimada | `I=S/(sqrt(3)*VLL)` | 97.808 | A | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Tensão auxiliar de 380 V assumida; confirmar arquitetura real |
| AUX-AC-14 | Utilização do candidato de transformador auxiliar | `S_demanda/S_candidato*100` | 64.375 | % | CANDIDATE_CHECK_ONLY; 100 kVA é candidato ilustrativo, sem especificação |
| DC-01 | Carga contínua equivalente da bateria | `Ah=P*1000*h/V` | 76.800 | Ah | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Perfil de missão provisório; tensões, sequência de manobra, temperatura, envelhecimento e baterias reais pendentes |
| DC-02 | Energia equivalente de operações de emergência | `Ah=I*t*n/3600` | 0.278 | Ah | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Corrente, duração e número de operações assumidos |
| DC-03 | Capacidade corrigida requerida para bateria 125 Vcc | `Ah_corrigido=(Ah_contínuo+Ah_manobras)*margem*envelhecimento*temperatura/(DoD*eficiência)` | 194.820 | Ah | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Não inclui curva de descarga, perfil de cargas ou catálogo de célula |
| DC-04 | Capacidade nominal candidata da bateria | `arredondar para cima ao próximo múltiplo de 10 Ah` | 200.000 | Ah | CANDIDATE_CHECK_ONLY; Valor de capacidade não seleciona química, tensão final ou fabricante |
| DC-05 | Corrente de carregador candidata para recuperação | `I_charger=I_contínua+Ah_candidato/tempo_recuperação` | 34.600 | A | CANDIDATE_CHECK_ONLY; Sem perfil de carga, curva de carga ou redundância especificados |
| DC-06 | Potência CC de saída para o carregador candidato | `P=V*I` | 4.325 | kW | CANDIDATE_CHECK_ONLY; Potência de saída CC; rendimento de CA e seleção de equipamento pendentes |
| UPS-01 | Demanda aparente da carga crítica UPS | `S=P/FP` | 1.875 | kVA | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Candidato de estudo; cargas críticas, barramento CC, arquitetura, autonomia e catálogo pendentes |
| UPS-02 | Utilização do candidato UPS | `S_demanda/S_candidato*100` | 62.500 | % | CANDIDATE_CHECK_ONLY; Candidato de 3 kVA sem catálogo ou fator de crista verificado |
| UPS-03 | Energia nominal mínima preliminar da bateria UPS | `E=P*h*margem*envelhecimento*temperatura/(DoD*eficiência)` | 3.791 | kWh | ESTIMATE_FROM_EXPLICIT_ASSUMPTIONS; Energia apenas; tensão CC, curvas, eficiência por carga e catálogo pendentes |

## Verificações internas da hipótese

- `ac_candidate_below_assumed_utilization_limit`: OK
- `dc_candidate_capacity_covers_calculated_study_capacity`: OK
- `charger_candidate_power_covers_calculated_dc_demand`: OK
- `ups_candidate_rating_covers_study_apparent_load`: OK

## Limites

- Todas as potências, fatores de demanda, FP, tensões auxiliares e autonomias são premissas explícitas, não dados medidos.
- Não há levantamento de cargas, cálculo térmico, projeto luminotécnico, estudo de seletividade CA ou dados de fabricante.
- Os valores resultantes não demonstram atendimento a requisitos normativos, da concessionária ou de segurança.
- Revisar a base com lista de cargas aprovada, sequência de manobras e condições ambientais antes de selecionar equipamento.
