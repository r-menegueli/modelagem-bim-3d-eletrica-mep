# Auditoria consolidada da R20-23 — GIS

**Data:** 2026-11-09
**Escopo:** conferência de identidades, rotas geométricas, leitos e vínculos dos cálculos com o modelo extraído pelo Revit 2027.3. Esta auditoria não modifica o RVT nem converte geometria em conectividade elétrica.

## Evidências de entrada

O documento aberto no Revit é `19-subestacao-gis-r20.rvt`, na vista `3D-02 - Bay GIS-T01 e seleção de barras R20`. A API extraiu 168 instâncias de família, 24 DirectShapes, 8 segmentos nativos CableTray, 25 conectores MEP, 1 ElectricalSystem e 1 Wire 2D. A vista 3D ao vivo estava preta/vazia durante esta verificação; a inspeção visual da geometria foi feita pelas folhas e prévias PDF já exportadas, não por uma vista ao vivo funcional.

| Fonte | SHA-256 |
|---|---|
| `auditoria-modelo-r20.json` | `19533c8fbddd0189d5e06885489e8d6690ec4d470b9d0532b5db538607437018` |
| `conectividade-equipamentos-r20.csv` | `2a755fe6e840d76c7443c38b49965a25eb6ac1a87a6d036bf6d69577c07ccea2` |
| `quantitativos-modelo.csv` | `4256899c5c585450f7d9a835be1fa1dfadac117a09d866b9a7c154081b9e58b4` |
| `base-de-projeto.json` usada nos cálculos | `8a91cf2f842b36c2f952d7b0d4aa1f364c774989df7387d87642882653cbe6b6` |

`conectividade-equipamentos-r20.csv` reúne as classes usadas pelo grafo. `quantitativos-modelo.csv` é a tabela de famílias e parâmetros usada para resolver os cálculos; não contém as 24 geometrias DirectShape. A auditoria combina cada classe somente com seu inventário de origem e compara ElementId/UniqueId, marca e geometria extraída.

A cópia da entrada usada no crosswalk está publicada como `../calculos-n1-model-crosswalk-v1/base-de-projeto-fonte.json`; seu SHA-256 confere com a base original indicada acima. A CLI reproduziu memorial, CSV e JSON com os mesmos SHA-256 dos resultados desta candidata.

## Resultado de conectividade

- 73 nós e 70 relações esperados; nenhum nó ausente, parcial ou ambíguo.
- Nenhuma marca, ElementId, UniqueId ou SystemId duplicado; nenhuma rota geométrica declarada ausente.
- As 12 DirectShapes de rota foram verificadas contra a extração API: 12/12 aceitas e 0 rejeitadas. Elas formam quatro grupos geométricos A/B/C: GIS→TR-01 em 138 kV, GIS→TR-02 em 138 kV, TR-01→QMT-A-IN em 13,8 kV e TR-02→QMT-B-IN em 13,8 kV.
- A checagem mede sólidos, quantidade de segmentos, limites geométricos e distância aos envelopes das famílias identificadas. Distância zero ao envelope não é porta, terminal, contato interno nem união elétrica. Os rótulos A/B/C são faixas geométricas; a associação com fases elétricas não foi comprovada.
- 8 segmentos CableTray e 16 conectores foram auditados; 8 conectores reportam `IsConnected`. São juntas de infraestrutura, sem cabos, preenchimento, ligação a equipamento ou circuito.
- 1 associação `ElectricalSystem` auxiliar QAC-01→UPS-01 e 1 Wire nativo 2D view-specific foram confirmados. Não há cálculo de carga Revit confirmado para esse sistema.
- Condutores primários 3D confirmados: 0. Sistemas/circuitos de potência primários 138 kV e 13,8 kV confirmados: 0. A topologia primária permanece documentação de diagrama mais rotas geométricas; não é uma rede eletricamente conectada no Revit.

| Classe das 70 relações | Quantidade |
|---|---:|
| `GEOMETRIC_ROUTE_ONLY` | 14 |
| `DIAGRAM_ONLY` | 44 |
| `NATIVE_ELECTRICAL_SYSTEM_LOGICAL` | 1 |
| `NATIVE_SYSTEM_NOT_CONFIRMED` | 11 |

Os 14 casos geométricos incluem as quatro rotas trifásicas e os invólucros/stubs de barra e seletoras. Todos permanecem sem condutor selecionado, porta MEP ou contato comutável.

## Cálculos e vínculo BIM

O estudo executado contém 32 cálculos e relaciona 33 marcas de equipamentos/circuitos aos elementos do modelo. O cruzamento `Mark → ElementId/UniqueId` foi completo: 0 marcas ausentes, 0 marcas duplicadas e 0 identidades incompletas. Isso demonstra correspondência de identidade, não conectividade elétrica.

As entradas de carga seguem as premissas documentadas da revisão-base: 56 MW, FP 0,95, 32 MW críticos e dois transformadores de 40 MVA; o FP de placa e os perfis reais de carga não foram fornecidos. Resultados representativos:

- Carga normal: 58,947 MVA; corrente de carga calculada de 0,247 kA em 138 kV e 2,466 kA em 13,8 kV.
- Correntes nominais por transformador de 40 MVA: 0,167 kA em 138 kV e 1,673 kA em 13,8 kV.
- Carregamento normal agregado com divisão uniforme: 73,7% de 80 MVA.
- N-1: 56 MW não cabem em um único transformador (147,4% do limite ativo assumido de 38 MW a FP 0,95). A rejeição indicada de 24 MW deixa 32 MW críticos; isso corresponde a 84,2% de um transformador apenas se existir caminho de transferência entre seções.
- Com o acoplamento de MT normalmente aberto, a checagem registra **falso** para atendimento de todas as cargas críticas após perda de transformador. Fechar/operar a transferência é cenário condicional, ainda sem validação de intertravamento, seletividade, rating do acoplamento ou procedimento operacional.
- O curto simplificado com Z provisória de 10% resulta em 16,73 kA por transformador e 33,47 kA com dois em paralelo no barramento de 13,8 kV. É uma sensibilidade baseada só nos transformadores e fonte ideal; não seleciona disjuntor nem substitui curto com equivalente da concessionária, X/R, cabos e motores.
- Permanecem pendentes dimensionamento de cabos/queda, ajuste e coordenação de proteção, demanda de auxiliares baseada em cadastro de cargas, missão de bateria baseada em sequência de manobras e aterramento com dados de solo/falta.

A prancha M-01 mantém as linhas de base R8 de auxiliares e bateria com asteriscos de hipótese: 57,938 kW e 64,375 kVA para auxiliares; 194,82 Ah calculados para a missão de 125 Vcc/8 h, com 200 Ah como candidato; UPS 1,5 kW/1 h = 3,791 kWh. Esses valores não são dimensionamento aprovado nem foram convertidos em dados de fabricante.

## Referências de equipamento e limites geométricos

- A brochura oficial GE Vernova F35 Dual Gas 145 kV publica a classe 145 kV, até 3.150 A, 40 kA por 3 s e o envelope de baia 800 × 3.700 × 2.500 mm. A família Revit usa esse envelope como referência; módulos, terminais, clearance de pedido e GA ainda não foram selecionados. [Brochura GE Vernova F35 Dual Gas](https://www.gevernova.com/grid-solutions/sites/default/files/2026-02/F35-145-kV-Dual-Gas-GIS-EN-33282-202504-LTR-R002_0.pdf)
- O catálogo oficial ABB UniGear ZS1 apresenta configurações de 17,5 kV com painel de 800 mm de largura e 1.340 mm de profundidade; altura e corrente variam com a unidade e configuração. A família modelada continua um envelope dimensional funcional, sem pedido, rating de curto ou configuração interna selecionados. [Catálogo ABB UniGear ZS1](https://library.e.abb.com/public/e63c3b0ceac143cda1f6be64e3647991/UniGear%20ZS1_Catalogue_1VCP000138_RevS_EN.pdf)
- A DAX lista o candidato TRP-EA40-DML-01 como 138/13,8 kV, 30/40 MVA, Dyn1 e Z=11%. O modelo e o estudo preservam a hipótese-base R8 de Z=10%; 11% serve apenas como sensibilidade do candidato, sem selecionar o transformador ou afirmar dimensões. [Dados publicados pela DAX](https://dax.energy/produtos/transformadores-de-potencia)

## Validação e limite do arquivo

- A saída desta auditoria está em `grafo-conectividade-r20.json`, `relacoes-rede-r20.csv` e `conectividade-r20.md` nesta pasta.
- A correlação dos cálculos está em `../calculos-n1-model-crosswalk-v1/`.
- A prancha R20-23 é um PDF A3 de 14 folhas renderizado e inspecionado em prévias. A exportação PDF nativa do Revit não completou por indisponibilidade de recursos de renderização/material na instância; a emissão preserva também o PDF bruto gerado via Microsoft Print to PDF. A vista 3D ao vivo continua pendente.
- **Classificação:** estudo BIM demonstrativo. Não é projeto executivo, aprovação de equipamento, certificação normativa ou documento para construção.
