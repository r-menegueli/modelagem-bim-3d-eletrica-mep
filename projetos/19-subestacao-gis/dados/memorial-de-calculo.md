# Memorial de cálculo — Projeto 19: Subestação GIS

- Revisão: A | Data do cálculo: 2026-11-07 | Estado: estudo preliminar com premissas
- Base de potência: trifásica equilibrada; valores em SI; tensão linha-linha.

## Entradas e origem

Os valores abaixo são premissas de estudo criadas para este exercício; não são dados de concessionária, fabricante ou levantamento de campo.

| Grandeza | Valor |
|---|---:|
| Tensão primária | 138 kV |
| Tensão secundária | 13.8 kV |
| Carga ativa agregada | 32 MW |
| Fator de potência de carga | 0.95 |
| Carga ativa crítica | 18 MW |
| Transformadores | 2 × 25 MVA |
| Impedância de placa adotada | 8% por unidade |
| FP-alvo para estimativa de compensação | 0.98 |

Base da premissa: Premissas de estudo para o portfólio; sem dados de concessionária, fabricante ou levantamento de campo.

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
| Carga aparente normal | 33,684 MVA |
| Carga reativa normal | 10,518 MVAr |
| Corrente da carga em 138 kV | 0,141 kA |
| Corrente da carga em 13,8 kV | 1,409 kA |
| Corrente nominal primária por transformador | 0,105 kA |
| Corrente nominal secundária por transformador | 1,046 kA |
| Carregamento normal dos dois transformadores | 67,4% |
| N-1: carga crítica / um transformador | 75,8% |
| N-1: carga total sem rejeição / um transformador | 134,7% |
| Compensação reativa estimada para FP 0.98 | 4,020 MVAr |
| Curto 13,8 kV, uma unidade, estimativa simplificada | 13,074 kA |
| Curto 13,8 kV, duas unidades em paralelo, estimativa simplificada | 26,148 kA |

O cálculo indica que o bloco crítico cabe na potência nominal de um transformador sob a premissa adotada; manter a carga total com apenas uma unidade exigiria rejeição de carga ou outra fonte, pois excede a potência nominal. A estimativa de curto não é base de ajuste ou seleção de disjuntor.

## Referências técnicas a revisar no projeto executivo

- ABNT NBR 14039:2021 (faixa indicada no título: 1,0 kV a 36,2 kV), para o sistema de 13,8 kV; confirmar versão vigente e escopo completo no catálogo ABNT.
- ABNT NBR 5419:2026, partes 1 a 4, para avaliação de proteção contra descargas; confirmar aplicabilidade ao sítio e versão vigente no catálogo ABNT.
- IEC 62271-203:2022 para GIS acima de 52 kV; IEC 62271-200:2021+AMD1:2024 para cubículos de 13,8 kV; IEC 62271-211:2024 para conexão direta GIS-transformador, se aplicável ao arranjo selecionado.

As referências são trilhas de verificação, não declaração de conformidade. A edição e o texto aplicável devem ser confirmados pelo responsável técnico antes de projeto executivo.

## Limitações e pendências

- Curto-circuito em 138 kv e impedância equivalente da rede.
- Curto-circuito completo em 13,8 kv com rede, motores e cabos.
- X/r, componente assimétrica, capacidade de interrupção e coordenação de proteção.
- Perdas e elevação térmica dos transformadores (sem dados de fabricante).
- Queda de tensão, cabos, aterramento, toque/passo e malha de terra.
- Perfil de carga, energia mensal e contingência de duração definida.

Não foram definidos ajustes de proteção, classe de exatidão, TC/TP, sequência de manobra, capacidade interruptiva, nível de isolamento, malha de terra, fundações, ventilação, incêndio, ruído, óleo, gás isolante ou requisitos ambientais. Não usar para obra, compra, aprovação ou instalação.
