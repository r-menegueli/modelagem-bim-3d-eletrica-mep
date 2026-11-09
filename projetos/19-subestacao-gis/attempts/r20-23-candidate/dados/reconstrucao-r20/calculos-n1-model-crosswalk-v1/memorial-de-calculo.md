# Memorial de cálculo — Subestação GIS 138/13,8 kV

- Revisão: 0 | Data: 2026-11-07 | Estado: PREMISSA DE ESTUDO - NAO E PROJETO EXECUTIVO
- Base: trifásica equilibrada; tensões linha-linha; MVA/kV resulta em kA.
- Entrada explícita: [base-de-projeto.json](base-de-projeto.json).
- `E-01/circuito` identifica o tag no unifilar; `Marca(s) BIM` contém o crosswalk para os valores do parâmetro Revit `Mark`.
- O crosswalk de marcas Revit é auditado separadamente contra o CSV nativo informado na execução; presença de marca e identidade ElementId/UniqueId não comprovam conectividade elétrica.

## Resultados rastreáveis

| ID | E-01/circuito | Marca(s) BIM | Grandeza | Entradas | Método | Resultado | Unidade | Origem/estado |
|---|---|---|---|---|---:|---|---|---|
| BP-01 | A-F01, A-F02, A-F03, A-F04, A-F05, B-F01, B-F02, B-F03, B-F04, B-F05 | QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-A-F04, QMT-A-F05, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-B-F04, QMT-B-F05 | Potência aparente total | P=56 MW; FP=0.95 | `S=P/FP` | 58,947 | MVA | Premissa de estudo em base-de-projeto.json (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-02 | A-F01, A-F02, A-F03, A-F04, A-F05, B-F01, B-F02, B-F03, B-F04, B-F05 | QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-A-F04, QMT-A-F05, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-B-F04, QMT-B-F05 | Potência reativa total | S=58.947368 MVA; P=56 MW | `Q=sqrt(S^2-P^2)` | 18,406 | MVAr | Premissa de estudo em base-de-projeto.json (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-03 | GIS-L01, GIS-L02 | GIS-L01-ENV, GIS-L01-52, GIS-L01-89A, GIS-L01-89B, GIS-L01-89G, GIS-L01-CT, GIS-L01-VT, GIS-L01-TERM, GIS-L02-ENV, GIS-L02-52, GIS-L02-89A, GIS-L02-89B, GIS-L02-89G, GIS-L02-CT, GIS-L02-VT, GIS-L02-TERM | Corrente de carga no sistema 138 kV | S=58.947368 MVA; VLL=138 kV | `I=S/(sqrt(3)*VLL)` | 0,247 | kA | Premissa de estudo em base-de-projeto.json (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-04 | QMT-A-IN, QMT-B-IN, BUS-MV-A, BUS-MV-B, A-F01, A-F02, A-F03, A-F04, A-F05, B-F01, B-F02, B-F03, B-F04, B-F05 | QMT-A-IN, QMT-B-IN, BUS-MV-A, BUS-MV-B, QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-A-F04, QMT-A-F05, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-B-F04, QMT-B-F05 | Corrente de carga no sistema 13,8 kV | S=58.947368 MVA; VLL=13.8 kV | `I=S/(sqrt(3)*VLL)` | 2,466 | kA | Premissa de estudo em base-de-projeto.json (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-05 | TR-01, TR-02 | TR-01, TR-02 | Corrente nominal por transformador no lado 138 kV | Sr=40 MVA; VLL=138 kV | `In=Sr/(sqrt(3)*VLL)` | 0,167 | kA | Rating de estudo; substituir pela placa aprovada (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-06 | TR-01, TR-02 | TR-01, TR-02 | Corrente nominal por transformador no lado 13,8 kV | Sr=40 MVA; VLL=13.8 kV | `In=Sr/(sqrt(3)*VLL)` | 1,673 | kA | Rating de estudo; substituir pela placa aprovada (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-07 | TR-01, TR-02, A-F01, A-F02, A-F03, A-F04, A-F05, B-F01, B-F02, B-F03, B-F04, B-F05 | TR-01, TR-02, QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-A-F04, QMT-A-F05, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-B-F04, QMT-B-F05 | Carregamento normal agregado | S=58.947368 MVA; capacidade instalada=80 MVA | `S/capacidade instalada*100` | 73,684 | % | Assume divisão equilibrada entre os dois transformadores (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-08 | TR-01, TR-02, QMT-A-IN, QMT-B-IN, QMT-TIE, A-F01, A-F02, A-F03, B-F01, B-F02, B-F03 | TR-01, TR-02, QMT-A-IN, QMT-B-IN, QMT-TIE, QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-B-F01, QMT-B-F02, QMT-B-F03 | Carregamento da carga crítica agregada após transferência entre seções no N-1 | Pcrit=32 MW; FP=0.95; Sr=40 MVA | `(Pcrit/FP)/Sr*100` | 84,211 | % | Só representa atendimento das duas seções se o acoplador MT puder fechar e transferir a carga; manobra, proteção e capacidade do caminho não foram verificados (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-09 | TR-01, TR-02, A-F01, A-F02, A-F03, A-F04, A-F05, B-F01, B-F02, B-F03, B-F04, B-F05 | TR-01, TR-02, QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-A-F04, QMT-A-F05, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-B-F04, QMT-B-F05 | Carregamento da carga total sob N-1 | P=56 MW; FP=0.95; Sr=40 MVA | `(P/FP)/Sr*100` | 147,368 | % | Sem rejeição de carga; demonstra necessidade de contingência (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-10 | A-F01, A-F02, A-F03, A-F04, A-F05, B-F01, B-F02, B-F03, B-F04, B-F05 | QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-A-F04, QMT-A-F05, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-B-F04, QMT-B-F05 | Carga ativa a rejeitar sob N-1 | P=56 MW; Pcrit=32 MW | `P-Pcrit` | 24,000 | MW | Premissa do cenário de contingência (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-11 | QMT-TIE | QMT-TIE | Corrente nominal do acoplamento/cubículo de referência | In de transformador=1673.479 A; candidato=2000 A | `In/candidato*100` | 83,674 | % do candidato | Comparação com candidato de 2.000 A; a brochura GE Vernova lista faixas de tensão e corrente, mas não comprova essa combinação específica; confirmar rating, configuração e condição térmica com o fabricante (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-12 | TR-01, TR-02 | TR-01, TR-02 | Corrente de curto simplificada por transformador em 13,8 kV | In=1.673479 kA; Z=10% assumida | `Isc=In/Zpu` | 16,735 | kA | Fonte ideal; somente impedância percentual presumida do transformador (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-13 | TR-01, TR-02, BUS-MV-A, BUS-MV-B, QMT-TIE | TR-01, TR-02, BUS-MV-A, BUS-MV-B, QMT-TIE | Corrente de curto simplificada com transformadores em paralelo | 2 unidades idênticas; cada contribuição=16.734790 kA | `Soma de contribuições, sem impedâncias da rede/cabos` | 33,470 | kA | Sensibilidade; acoplador normalmente aberto no regime normal (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-14 | A-F01, A-F02, A-F03, A-F04, A-F05, B-F01, B-F02, B-F03, B-F04, B-F05 | QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-A-F04, QMT-A-F05, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-B-F04, QMT-B-F05 | Compensação idealizada de FP 0.95 para 0.98 | P=56 MW; FPin=0.95; FPout=0.98 | `P*(tan(acos(FPin))-tan(acos(FPout)))` | 7,035 | MVAr | Estimativa sem harmônicos, variação de carga ou estudo de ressonância (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-15 | QMT-A-IN, QMT-B-IN, BUS-MV-A, BUS-MV-B, A-F01, A-F02, A-F03, A-F04, A-F05, B-F01, B-F02, B-F03, B-F04, B-F05 | QMT-A-IN, QMT-B-IN, BUS-MV-A, BUS-MV-B, QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-A-F04, QMT-A-F05, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-B-F04, QMT-B-F05 | Corrente normal em cada seção de 13,8 kV | P por seção=28 MW; FP=0.95; VLL=13.8 kV | `(P/FP)/(sqrt(3)*VLL)` | 1,233 | kA | Assume cargas iguais nas seções A/B conforme alocação declarada (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-16 | TR-01, TR-02 | TR-01, TR-02 | Capacidade ativa de um transformador no fator de potência assumido | Sr=40 MVA; FP=0.95 | `P_limite=Sr*FP` | 38,000 | MW | Limite de placa em regime contínuo no FP assumido; não inclui sobrecarga de emergência (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-17 | TR-01, TR-02, QMT-TIE, BUS-MV-A, BUS-MV-B, A-F01, A-F02, A-F03, B-F01, B-F02, B-F03 | TR-01, TR-02, QMT-TIE, BUS-MV-A, BUS-MV-B, QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-B-F01, QMT-B-F02, QMT-B-F03 | Margem aparente na placa após atendimento agregado das cargas críticas no N-1 | Sr=40 MVA; Scrit=33.6842 MVA | `Sr-Scrit` | 6,316 | MVA | Só existe atendimento das duas seções se o acoplador MT e o caminho de transferência estiverem disponíveis; verificação pendente (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-18 | TR-01, TR-02, QMT-TIE, BUS-MV-A, BUS-MV-B, A-F04, A-F05, B-F04, B-F05 | TR-01, TR-02, QMT-TIE, BUS-MV-A, BUS-MV-B, QMT-A-F04, QMT-A-F05, QMT-B-F04, QMT-B-F05 | Carga não crítica adicional dentro da capacidade de placa após as cargas críticas | P_limite=38 MW; Pcrit=32 MW | `max(0, P_limite-Pcrit)` | 6,000 | MW | Envelope agregado teórico com acoplador fechado; seleção de alimentadores e capacidade do acoplador pendentes (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-19 | TR-01, TR-02, A-F01, A-F02, A-F03, A-F04, A-F05, B-F01, B-F02, B-F03, B-F04, B-F05 | TR-01, TR-02, QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-A-F04, QMT-A-F05, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-B-F04, QMT-B-F05 | Rejeição ativa mínima agregada para limitar o transformador remanescente à placa no N-1 | Pnormal=56 MW; P_limite=38 MW | `max(0, Pnormal-P_limite)` | 18,000 | MW | Envelope agregado contínuo a FP uniforme; não é filosofia de rejeição automática nem lista de prioridade de cargas (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-20 | TR-01, TR-02, QMT-A-IN, QMT-B-IN, QMT-TIE, A-F01, A-F02, A-F03, B-F01, B-F02, B-F03 | TR-01, TR-02, QMT-A-IN, QMT-B-IN, QMT-TIE, QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-B-F01, QMT-B-F02, QMT-B-F03 | Carga crítica em uma seção isolada com acoplador MT aberto | Máxima carga crítica de uma seção=16 MW | `max(Pcrit_A, Pcrit_B)` | 16,000 | MW | Carga crítica associada ao transformador indisponível sem caminho de transferência; resultado depende da alocação real por seção (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-21 | TR-01, TR-02, QMT-TIE, BUS-MV-A, BUS-MV-B, A-F01, A-F02, A-F03, B-F01, B-F02, B-F03, A-F04, A-F05, B-F04, B-F05 | TR-01, TR-02, QMT-TIE, BUS-MV-A, BUS-MV-B, QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-A-F04, QMT-A-F05, QMT-B-F04, QMT-B-F05 | Carregamento N-1 com cargas críticas e o maior alimentador não crítico individual que cabe | Pcrit=32 MW; Pnc=6 MW; FP=0.95; Sr=40 MVA | `((Pcrit+Pnc)/FP)/Sr*100` | 100,000 | % | Exige transferência entre seções; mostra apenas a maior opção individual e não escolhe prioridade operacional (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| BP-22 | TR-01, TR-02 | TR-01, TR-02 | Relação nominal de tensão linha-linha do transformador | VHVLL=138 kV; VLVLL=13.8 kV | `VHVLL/VLVLL` | 10:1 | relação VLL | Relação de tensões nominais do estudo; placa, grupo vetorial e taps devem ser confirmados (CALCULATED_FROM_STUDY_ASSUMPTIONS) |
| FDR-A-F01 | A-F01 | QMT-A-F01 | Corrente estimada do alimentador A-F01 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-A-F02 | A-F02 | QMT-A-F02 | Corrente estimada do alimentador A-F02 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-A-F03 | A-F03 | QMT-A-F03 | Corrente estimada do alimentador A-F03 | P=4 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,176 | kA | Carga crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-A-F04 | A-F04 | QMT-A-F04 | Corrente estimada do alimentador A-F04 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga não crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-A-F05 | A-F05 | QMT-A-F05 | Corrente estimada do alimentador A-F05 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga não crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-B-F01 | B-F01 | QMT-B-F01 | Corrente estimada do alimentador B-F01 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-B-F02 | B-F02 | QMT-B-F02 | Corrente estimada do alimentador B-F02 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-B-F03 | B-F03 | QMT-B-F03 | Corrente estimada do alimentador B-F03 | P=4 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,176 | kA | Carga crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-B-F04 | B-F04 | QMT-B-F04 | Corrente estimada do alimentador B-F04 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga não crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |
| FDR-B-F05 | B-F05 | QMT-B-F05 | Corrente estimada do alimentador B-F05 | P=6 MW; FP=0.95; VLL=13.8 kV | `If=S/(sqrt(3)*VLL), where S=P/FP` | 0,264 | kA | Carga não crítica assumida; sem método de instalação, comprimento ou agrupamento (CALCULATED_LOAD_CURRENT_ONLY_CONDUCTOR_NOT_SIZED) |

## Crosswalk do unifilar para as marcas BIM

O crosswalk abaixo liga o identificador desenhado em E-01 à marca `Mark` do modelo. Quando informado um CSV nativo, `model_mark_crosswalk_audit` também resolve cada marca para `ElementId` e `UniqueId`; isso comprova identidade, não conectividade.

| Identificador E-01/circuito | Marca(s) BIM correspondente(s) |
|---|---|
| A-F01 | QMT-A-F01 |
| A-F02 | QMT-A-F02 |
| A-F03 | QMT-A-F03 |
| A-F04 | QMT-A-F04 |
| A-F05 | QMT-A-F05 |
| B-F01 | QMT-B-F01 |
| B-F02 | QMT-B-F02 |
| B-F03 | QMT-B-F03 |
| B-F04 | QMT-B-F04 |
| B-F05 | QMT-B-F05 |
| BUS-MV-A | BUS-MV-A |
| BUS-MV-B | BUS-MV-B |
| GIS-L01 | GIS-L01-ENV, GIS-L01-52, GIS-L01-89A, GIS-L01-89B, GIS-L01-89G, GIS-L01-CT, GIS-L01-VT, GIS-L01-TERM |
| GIS-L02 | GIS-L02-ENV, GIS-L02-52, GIS-L02-89A, GIS-L02-89B, GIS-L02-89G, GIS-L02-CT, GIS-L02-VT, GIS-L02-TERM |
| QMT-A-IN | QMT-A-IN |
| QMT-B-IN | QMT-B-IN |
| QMT-TIE | QMT-TIE |
| TR-01 | TR-01 |
| TR-02 | TR-02 |

Auditoria de identidade do quantitativo Revit: `MODEL_MARK_CROSSWALK_COMPLETE`.
Fonte: `projetos/19-subestacao-gis/attempts/r20-23-candidate/dados/reconstrucao-r20/quantitativos-modelo.csv` (SHA-256 `4256899c5c585450f7d9a835be1fa1dfadac117a09d866b9a7c154081b9e58b4`).

| Marca | ElementId | UniqueId | Categoria | Família | Tipo |
|---|---:|---|---|---|---|
| BUS-MV-A | 357945 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-00057639 | Modelos genéricos | GIS_MVBusModule | MV_BUS_17K5_STUDY |
| BUS-MV-B | 357946 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-0005763a | Modelos genéricos | GIS_MVBusModule | MV_BUS_17K5_STUDY |
| GIS-L01-52 | 357799 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575a7 | Equipamento elétrico | GIS_CircuitBreaker | GIS_CB_145KV_STUDY |
| GIS-L01-89A | 357800 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575a8 | Modelos genéricos | GIS_Disconnector | GIS_DS_145KV_STUDY |
| GIS-L01-89B | 357801 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575a9 | Modelos genéricos | GIS_Disconnector | GIS_DS_145KV_STUDY |
| GIS-L01-89G | 357802 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575aa | Modelos genéricos | GIS_EarthingSwitch | GIS_ES_145KV_STUDY |
| GIS-L01-CT | 357803 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575ab | Equipamento elétrico | GIS_CurrentTransformer | GIS_CT_145KV_STUDY |
| GIS-L01-ENV | 357798 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575a6 | Modelos genéricos | GIS_BayFrame | GIS_BAY_F35_145KV_DIGITAL_SF6_BAY_ENVELOPE |
| GIS-L01-TERM | 357805 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575ad | Equipamento elétrico | GIS_Termination | GIS_TERMINATION_145KV_STUDY |
| GIS-L01-VT | 357804 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575ac | Equipamento elétrico | GIS_VoltageTransformer | GIS_VT_145KV_STUDY |
| GIS-L02-52 | 357825 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575c1 | Equipamento elétrico | GIS_CircuitBreaker | GIS_CB_145KV_STUDY |
| GIS-L02-89A | 357826 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575c2 | Modelos genéricos | GIS_Disconnector | GIS_DS_145KV_STUDY |
| GIS-L02-89B | 357827 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575c3 | Modelos genéricos | GIS_Disconnector | GIS_DS_145KV_STUDY |
| GIS-L02-89G | 357828 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575c4 | Modelos genéricos | GIS_EarthingSwitch | GIS_ES_145KV_STUDY |
| GIS-L02-CT | 357829 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575c5 | Equipamento elétrico | GIS_CurrentTransformer | GIS_CT_145KV_STUDY |
| GIS-L02-ENV | 357824 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575c0 | Modelos genéricos | GIS_BayFrame | GIS_BAY_F35_145KV_DIGITAL_SF6_BAY_ENVELOPE |
| GIS-L02-TERM | 357831 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575c7 | Equipamento elétrico | GIS_Termination | GIS_TERMINATION_145KV_STUDY |
| GIS-L02-VT | 357830 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575c6 | Equipamento elétrico | GIS_VoltageTransformer | GIS_VT_145KV_STUDY |
| QMT-A-F01 | 357934 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-0005762e | Equipamento elétrico | GIS_MV_Cubicle | MV_UNIGEAR_ZS1_17K5_2KA_REFERENCE |
| QMT-A-F02 | 357936 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-00057630 | Equipamento elétrico | GIS_MV_Cubicle | MV_UNIGEAR_ZS1_17K5_2KA_REFERENCE |
| QMT-A-F03 | 357938 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-00057632 | Equipamento elétrico | GIS_MV_Cubicle | MV_UNIGEAR_ZS1_17K5_2KA_REFERENCE |
| QMT-A-F04 | 357940 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-00057634 | Equipamento elétrico | GIS_MV_Cubicle | MV_UNIGEAR_ZS1_17K5_2KA_REFERENCE |
| QMT-A-F05 | 357942 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-00057636 | Equipamento elétrico | GIS_MV_Cubicle | MV_UNIGEAR_ZS1_17K5_2KA_REFERENCE |
| QMT-A-IN | 357930 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-0005762a | Equipamento elétrico | GIS_MV_Cubicle | MV_UNIGEAR_ZS1_17K5_2KA_REFERENCE |
| QMT-B-F01 | 357931 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-0005762b | Equipamento elétrico | GIS_MV_Cubicle | MV_UNIGEAR_ZS1_17K5_2KA_REFERENCE |
| QMT-B-F02 | 357933 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-0005762d | Equipamento elétrico | GIS_MV_Cubicle | MV_UNIGEAR_ZS1_17K5_2KA_REFERENCE |
| QMT-B-F03 | 357935 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-0005762f | Equipamento elétrico | GIS_MV_Cubicle | MV_UNIGEAR_ZS1_17K5_2KA_REFERENCE |
| QMT-B-F04 | 357937 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-00057631 | Equipamento elétrico | GIS_MV_Cubicle | MV_UNIGEAR_ZS1_17K5_2KA_REFERENCE |
| QMT-B-F05 | 357939 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-00057633 | Equipamento elétrico | GIS_MV_Cubicle | MV_UNIGEAR_ZS1_17K5_2KA_REFERENCE |
| QMT-B-IN | 357943 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-00057637 | Equipamento elétrico | GIS_MV_Cubicle | MV_UNIGEAR_ZS1_17K5_2KA_REFERENCE |
| QMT-TIE | 357944 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-00057638 | Equipamento elétrico | GIS_MV_BusCoupler | MV_TIE_17K5_STUDY |
| TR-01 | 357868 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-000575ec | Equipamento elétrico | GIS_Transformer_Tank | TR_40MVA_DAX_EA40_REFERENCE |
| TR-02 | 357910 | 49b70e22-936d-4421-8cd6-6a936c7e98a4-00057616 | Equipamento elétrico | GIS_Transformer_Tank | TR_40MVA_DAX_EA40_REFERENCE |
Esta auditoria resolve as marcas dos cálculos para registros Revit extraídos com ElementId e UniqueId. Confirma identidade e presença da marca; não prova conectividade elétrica, participação em circuito ou continuidade de condutor físico.

## Cenários N-1 nas barras de média tensão

A barra de 13,8 kV é dividida em seções A/B e o acoplador é normalmente aberto. Com o acoplador aberto, a seção associada ao transformador indisponível não recebe transferência neste modelo:

| Caso | E-01/circuito | Marca(s) BIM | Seção remanescente | Crítica remanescente (MW) | Crítica isolada (MW) | Carregamento remanescente |
|---|---|---|---|---:|---:|---:|
| TR da seção A indisponível | TR-01, QMT-A-IN, TR-02, QMT-B-IN, QMT-TIE, A-F01, A-F02, A-F03, A-F04, A-F05, B-F01, B-F02, B-F03, B-F04, B-F05 | TR-01, QMT-A-IN, TR-02, QMT-B-IN, QMT-TIE, QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-A-F04, QMT-A-F05, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-B-F04, QMT-B-F05 | B | 16.000 | 16.000 | 73.68% |
| TR da seção B indisponível | TR-02, QMT-B-IN, TR-01, QMT-A-IN, QMT-TIE, B-F01, B-F02, B-F03, B-F04, B-F05, A-F01, A-F02, A-F03, A-F04, A-F05 | TR-02, QMT-B-IN, TR-01, QMT-A-IN, QMT-TIE, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-B-F04, QMT-B-F05, QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-A-F04, QMT-A-F05 | A | 16.000 | 16.000 | 73.68% |

Com caminho de transferência disponível, a carga crítica agregada de 32 MW representa 33,684 MVA e 84,21% da placa de um transformador. A capacidade ativa equivalente de 40 MVA a FP 0,95 é 38 MW; o envelope permite até 6 MW adicionais não críticos e calcula rejeição mínima agregada de 18 MW para respeitar a placa. O cenário com acoplador fechado é condicional e não comprova uma sequência de operação.

A premissa atual rejeita 24.000 MW não críticos, ou 6.000 MW além do mínimo agregado calculado. Quais alimentadores podem permanecer depende da prioridade das cargas, da comutação e da capacidade não verificada do caminho.

Opções individuais de 6 MW que cabem no envelope agregado (sem priorização):

| Alimentador candidato a permanecer | Marcas BIM envolvidas | Carga crítica + alimentador (MW) | Carregamento do transformador remanescente |
|---|---|---:|---:|
| A-F04 | TR-01, TR-02, QMT-TIE, QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-A-F04 | 38.000 | 100.00% |
| A-F05 | TR-01, TR-02, QMT-TIE, QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-A-F05 | 38.000 | 100.00% |
| B-F04 | TR-01, TR-02, QMT-TIE, QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-B-F04 | 38.000 | 100.00% |
| B-F05 | TR-01, TR-02, QMT-TIE, QMT-A-F01, QMT-A-F02, QMT-A-F03, QMT-B-F01, QMT-B-F02, QMT-B-F03, QMT-B-F05 | 38.000 | 100.00% |

As opções são mutuamente alternativas no envelope de 6 MW e requerem transferência entre seções; nenhuma é selecionada como prioridade de operação.

### Hipóteses e limites do N-1

- Dois transformadores idênticos, cada um associado a uma seção MT A/B em regime normal.
- O acoplador MT é normalmente aberto conforme a base de projeto.
- FP uniforme igual ao valor assumido; capacidade ativa equivalente = MVA nominal × FP.
- O caso com acoplador fechado é condicional: capacidade do acoplador, intertravamentos, proteção, cabos e procedimento de transferência não foram verificados.
- Com acoplador aberto, a carga crítica da seção associada ao transformador perdido permanece sem transferência neste modelo.
- O envelope agregado com acoplador fechado não prova que o equipamento ou os alimentadores suportem a transferência.
- A seleção de alimentadores não críticos, os estados de manobra, a rejeição automática, as interligações e a sequência de transferência continuam pendentes.
- Não foi modelada capacidade de sobrecarga de emergência; o limite usado é a placa de 40 MVA.

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

- `all_calculations_have_diagram_and_model_refs`: OK.
- `feeders_sum_to_normal_load`: OK.
- `feeders_sum_to_critical_load`: OK.
- `critical_load_within_one_transformer_nameplate`: OK.
- `full_load_requires_shedding_in_n_minus_one`: OK.
- `normal_transformers_not_overloaded_if_equally_shared`: OK.
- `n1_open_tie_serves_all_critical_sections`: FALHOU.
- `n1_critical_load_fits_one_unit_if_transfer_path_is_available`: OK.
- `n1_minimum_shedding_satisfies_one_unit_nameplate`: OK.
- `n1_full_noncritical_shedding_is_at_least_minimum_nameplate_shedding`: OK.

## Limites

- All loads, ratings and the 10% transformer impedance are assumptions for the portfolio study.
- A calculated load current does not establish ampacity or a conductor cross-section.
- Transformer-only short-circuit values are sensitivity estimates, not interrupting ratings or protection settings.
- No regulatory compliance or construction suitability is claimed.

As referências de fabricante indicam opções de produtos e faixas; não são especificação deste estudo. A IEC 62271-200:2021+AMD1:2024 descreve o escopo geral de conjuntos metal-enclosed acima de 1 kV até 52 kV e até 60 Hz; confirmar requisitos ABNT e da concessionária antes de qualquer projeto executivo.

Não usar para obra, compra, aprovação ou instalação.
