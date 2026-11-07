# Projeto 19 — Subestação GIS 138/13,8 kV

**Estado: estudo BIM parcial R11.** A revisão foi gerada e auditada no Revit 2027.3. Ela corrige a topologia documentada, detalha externamente um bay GIS, um transformador e uma seção MT, e separa com clareza conexões nativas, geometria de infraestrutura e relações existentes apenas no diagrama. O estudo ainda não é executivo nem comprova LOD 300.

## Entrega R11

- Modelo: [19-subestacao-gis-r11.rvt](revit/reconstrucao-r11/19-subestacao-gis-r11.rvt)
- Pranchas: [PDF A3 com 14 folhas](pranchas/reconstrucao-r11/19-subestacao-gis-r11.pdf)
- PNGs das 14 folhas e das quatro vistas 3D: [índice de imagens](pranchas/reconstrucao-r11/README.md)
- Biblioteca Revit: [30 famílias RFA](familias/reconstrucao-r11/)
- Auditoria extraída do modelo: [JSON](dados/reconstrucao-r11/auditoria-modelo-r11.json) e [quantitativos CSV](dados/reconstrucao-r11/quantitativos-modelo.csv)
- Conectividade por equipamento: [CSV de conectores e estados](dados/reconstrucao-r11/conectividade-equipamentos-r11.csv)
- Relações de rede e rastreabilidade: [CSV](dados/reconstrucao-r11/relacoes-rede-r11.csv), [grafo JSON](dados/reconstrucao-r11/grafo-conectividade-r11.json) e [relatório](dados/reconstrucao-r11/conectividade-r11.md)
- Cálculos preservados e sensibilidades adicionais: [entradas/resultados JSON](dados/reconstrucao-r11/estudo-complementar-r11.json), [CSV](dados/reconstrucao-r11/estudo-complementar-r11.csv) e [memorial](dados/reconstrucao-r11/memorial-complementar-r11.md)
- Comparação, validação e pendências: [R8 versus R11](dados/reconstrucao-r11/comparativo-r8-r11.md)
- Vista geral: ![Implantação BIM geral da subestação R11](pranchas/reconstrucao-r11/preview/19-subestacao-gis-r11-3D-01%20-%20Vista%203D%20-%203D-01%20-%20Implantacao%20BIM%20geral%20R11.png)

## Auditoria Revit R11

O relatório foi extraído por add-in de um documento Revit aberto e registra 170 instâncias FamilyInstance (46 equipamentos elétricos e 124 modelos genéricos), 21 paredes, 7 pisos, 84 DirectShapes, 175 ModelCurves, 8 elementos CableTray nativos, 14 folhas, 4 vistas 3D e zero avisos do documento. As 30 famílias foram geradas ou reutilizadas no diretório R11; todas as instâncias têm os metadados de identidade exigidos.

Há 3 conectores MEP auditados, dos quais 2 aparecem conectados; 44 dos 46 equipamentos não têm conector MEP. O único ElectricalSystem associa QAC-01 a UPS-01 e tem um terminal. Um fio nativo 2D, específico da vista E-06, tem seus dois conectores ligados. **Isso não representa cabo 3D, circuito de potência primária ou alimentação completa da UPS.**

O grafo contém 62 nós e 58 relações: 14 `GEOMETRIC_ROUTE_ONLY`, 43 `DIAGRAM_ONLY` e 1 `NATIVE_ELECTRICAL_SYSTEM_LOGICAL`. Não há condutor primário de potência confirmado. Oito segmentos CableTray têm 16 conectores e 8 conectores com estado conectado; essas ligações comprovam juntas de infraestrutura, não continuidade elétrica. Os dois pontos de entrada externos e dez destinos remotos dos alimentadores estão fora do modelo.

## Unifilar, geometria e referências

A E-01 representa duas barras de 138 kV com acoplador, bays para duas linhas e dois transformadores, seletores de barra e duas seções de 13,8 kV com tie normalmente aberto. Os estados de manobra são hipóteses explicitadas na prancha; contatos e intertravamentos não são simulados no Revit.

O bay GIS é uma referência de classe 145 kV, com envelope baseado em dados publicados da família [GE Vernova F35](https://www.gevernova.com/grid-solutions/sites/default/files/2025-09/F35_Universal-brochure-EN-N50036-Hybrid-202503-R002.pdf). Para o transformador foi consultado o [catálogo DAX 138/13,8 kV, 30/40 MVA](https://dax.energy/produtos/transformadores-de-potencia); para os cubículos, a família [ABB UniGear ZS1 17,5 kV](https://www.abb.com/global/en/areas/electrification/medium-voltage/switchgear/air-insulated/unigear/zs1); e para o cabo candidato, a ficha [Nexans EPR105-PVC 8,7/15 kV, 1×240 mm²](https://www.nexans.com.br/pt/products/Transmission.and.Distribution/Insulated.Copper-Aluminium.CablesForMediumVoltage/Nexans-EPR105-PVC/Nexans-EPR37761/product~ID540477955~.html). São referências para envelope e cenários, não equipamentos selecionados nem desenhos GA de fabricação.

As rotas HV/MV e stubs de seccionadoras são geometria representativa sem conectores elétricos. CableTrays são infraestrutura nativa. Não foram inventados internos proprietários de GIS, transformador ou cubículos.

## Cálculos e limites

Os 25 IDs de cálculo R8 permanecem registrados em R11. Nas hipóteses de 56 MW e FP 0,95, S é 58,95 MVA; as correntes trifásicas são 0,2466 kA em 138 kV e 2,466 kA em 13,8 kV. Dois transformadores de 40 MVA resultam em 73,68% de carregamento agregado; em N−1, 32 MW críticos correspondem a 84,21% de uma unidade, enquanto 56 MW totais excedem sua capacidade e exigem 24 MW de alívio.

A sensibilidade de cabo para 6 MW, FP 0,95, 13,8 kV e 250 m apresenta 264,23 A, queda de 13,99 V (0,101%) e perdas de cobre trifásicas de 5,02 kW para um candidato de cobre 1×240 mm². A ampacidade não foi verificada; X=0,1 Ω/km é hipótese. Curto-circuito adicional combina fonte, impedância provisória de transformador de 10% e trecho de cabo: com fonte assumida de 1.000 MVA e 250 m, o resultado simétrico é 11,95/11,51 kA (barra/remoto) com uma unidade e 18,59/17,54 kA com duas em paralelo. Esses cenários não definem capacidade de interrupção, proteção ou seletividade.

Permanecem pendentes dados da concessionária, placas e impedâncias aprovadas, ampacidade e instalação dos cabos, materiais e terminações, aterramento por resistividade e tempo de falta, missão DC/UPS, ajustes e seletividade, estados e intertravamentos, afastamentos, rotas civis e desenhos dimensionais dos fabricantes. Não usar para obra, compra, aprovação ou instalação.

## Histórico R8 preservado

O baseline anterior permanece disponível sem sobrescrita: [RVT R8](revit/reconstrucao-r8/19-subestacao-gis-r8.rvt), [PDF R8 com 21 folhas](pranchas/reconstrucao-r8/19-subestacao-gis-r8.pdf), [auditoria](dados/reconstrucao-r8/auditoria-modelo-r8.json), [cálculos](dados/reconstrucao-r8/resultados.json) e [índice de imagens](pranchas/reconstrucao-r8/README.md). A comparação inclui os números R8 e R11 lado a lado.
