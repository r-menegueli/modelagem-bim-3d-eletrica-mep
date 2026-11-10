# Comparativo técnico — R8 versus R11

Data da extração final: 07/11/2026. R8 permanece preservada como baseline. Esta comparação usa a auditoria R8 salva e a nova extração do RVT R11 no Revit 2027.3. R11 melhora representação, rastreabilidade e documentação; não converte o estudo em projeto executivo.

## Modelo Revit e documentação

| Métrica | R8 | R11 | Evidência / leitura |
|---|---:|---:|---|
| Instâncias FamilyInstance | 191 | 170 | A contagem muda com a revisão geométrica; redução de objetos não é indicador isolado de qualidade. |
| Electrical Equipment | 46 | 46 | Os mesmos 46 equipamentos estão inventariados. |
| Generic Model families | 145 | 124 | R11 usa 84 DirectShapes em envelopes e trechos geométricos; eles não são cabos nem circuitos. |
| Paredes / pisos nativos | 21 / 7 | 21 / 7 | Sem alteração. |
| DirectShapes | 0 | 84 | Duas carcaças de barramento, stubs de seletores e envelopes de rota por fase. |
| ModelCurves | 180 | 175 | A malha de aterramento continua conceitual; contagem geométrica não comprova desempenho elétrico. |
| CableTray nativo | não extraído | 8 | R11: 16 conectores de bandeja e 8 com estado conectado, correspondentes a quatro juntas internas confirmadas; infraestrutura apenas. O campo não foi extraído em R8. |
| Conectores MEP | 93 | 3 | R8 registra 0 conectados; R11 registra 2 conectados e 1 sem estado físico. Em R11, 44/46 equipamentos não têm conectores. |
| ElectricalSystem / terminais | 1 / 1 | 1 / 1 | R11 identifica QAC-01 → UPS-01 como relação lógica auxiliar; não é circuito primário. |
| Wire Revit 2D | não inventariado | 1; 2/2 conectores conectados | Vista E-06; específico da vista, sem cabo 3D. R8 não tem campo equivalente na auditoria salva. |
| DirectShapes/rotas faltantes no grafo | — | 0 | R11 identifica todas as relações geométricas previstas, sem classificá-las como conexão elétrica. |
| Folhas / vistas 3D | 21 / 8 | 14 / 4 | R11 substitui folhas redundantes por 14 folhas técnicas; o PDF foi renderizado novamente e inspecionado. |
| Avisos de documento Revit | não registrado no JSON R8 | 0 | O esquema R8 não inclui esse campo; não se compara como zero. |
| Instâncias sem metadados de identidade | 0 | 0 | Conferidos no CSV de instâncias por revisão. |

**Fontes:** [auditoria R8](../reconstrucao-r8/auditoria-modelo-r8.json), [auditoria R11](auditoria-modelo-r11.json), [quantitativos R11](quantitativos-modelo.csv) e [conectividade por equipamento](conectividade-equipamentos-r11.csv).

## Conectividade: o que existe e o que falta

R11 tem 62 nós e 58 relações classificadas: 14 `GEOMETRIC_ROUTE_ONLY`, 43 `DIAGRAM_ONLY` e 1 `NATIVE_ELECTRICAL_SYSTEM_LOGICAL`. Há cinco placeholders de TC para cinco posições esquemáticas; isso não define relação, núcleo, burden ou zona de proteção. Os dois nós de entrada 138 kV e dez destinos remotos de alimentadores ficam fora do limite do modelo. A extração não encontrou nó esperado interno nem rota geométrica prevista ausente.

O único vínculo elétrico nativo é o `ElectricalSystem` AUX-UPS-01, com quadro QAC-01 e UPS-01 como terminal. A API também confirmou um fio 2D na vista E-06 com os dois conectores ligados. O relatório não o trata como cabo 3D, nem como prova da origem a montante, saída da UPS ou circuito de bateria.

As conexões de potência entre fontes 138 kV, disjuntores, seccionadoras, barras GIS A/B, transformadores, barras 13,8 kV e dez alimentadores são documentadas no unifilar e no grafo, mas não estão implementadas como PowerCircuits ou condutores nativos conectados no modelo. Stubs de seccionadora, carcaças GIS e envelopes por fase são formas geométricas sem conectores. As juntas CableTray comprovam apenas continuidade dos próprios segmentos de infraestrutura. Assim, R11 melhora a identificação das lacunas sem inflar estatísticas de circuitos.

Arquivos: [relatório de conectividade](conectividade-r11.md), [CSV de relações com ElementId/UniqueId](relacoes-rede-r11.csv) e [grafo JSON](grafo-conectividade-r11.json).

## Unifilar E-01 e detalhamento 3D

E-01 mostra duas barras de 138 kV, bays de duas linhas e dois transformadores, cinco bays GIS contando o acoplador, seletores 89A/89B, 52-BC e seções MT A/B com tie normalmente aberto. Os estados identificados como premissas estão descritos na própria folha: o acoplador está normalmente aberto e seus seletores normalmente fechados; as posições de linha e transformação alternam as barras; 52-TIE da MT é normalmente aberto. TC do acoplador permanece placeholder. A coerência visual foi verificada contra marcas e relações presentes no inventário e no grafo.

E-02 aprofunda um bay classe 145 kV; E-03 detalha externamente um transformador candidato 138/13,8 kV e 40 MVA; E-04 amplia uma seção de cubículos de 13,8 kV. Geometrias são envelopes representativos, não internos ou GA proprietários. As referências consultadas são [GE Vernova F35](https://www.gevernova.com/grid-solutions/sites/default/files/2025-09/F35_Universal-brochure-EN-N50036-Hybrid-202503-R002.pdf), [transformadores DAX](https://dax.energy/produtos/transformadores-de-potencia), [ABB UniGear ZS1](https://www.abb.com/global/en/areas/electrification/medium-voltage/switchgear/air-insulated/unigear/zs1) e [cabo Nexans](https://www.nexans.com.br/pt/products/Transmission.and.Distribution/Insulated.Copper-Aluminium.CablesForMediumVoltage/Nexans-EPR105-PVC/Nexans-EPR37761/product~ID540477955~.html). Nenhum equipamento foi selecionado por esses links.

A exportação final do PDF foi renderizada folha a folha a 90 dpi. A inspeção em resolução integral cobriu A-01, A-02, E-01 e E-05, além da folha de contato completa. Chamadas redundantes foram retiradas das vistas em A-01, A-02 e E-05 e movidas para campos laterais; E-01 manteve textos e simbologia sem colisão de rótulos. As folhas estão no [índice PNG R11](../../pranchas/reconstrucao-r11/README.md).

## Cálculos

Os **25 IDs R8** (BP-01 a BP-15 e FDR-A/B-F01 a F05) estão enumerados em `preserved_r8_calculation_ids` em [estudo-complementar-r11.json](estudo-complementar-r11.json). Seus resultados não foram substituídos. Na base assumida R8 de 56 MW e FP 0,95: S=58,947 MVA; I=0,2466 kA em 138 kV e 2,466 kA em 13,8 kV; carregamento agregado de dois transformadores 40 MVA=73,684%; em N−1, 32 MW críticos representam 84,21% de uma unidade e os 56 MW totais excederiam sua capacidade, exigindo 24 MW de alívio. O curto simplificado de 16,735/33,470 kA mantém Z=10% provisória do(s) transformador(es), sem impedância da fonte ou do alimentador.

R11 acrescenta sensibilidades com hipóteses explícitas, sem emitir dimensionamento:

- Cabo candidato Nexans de cobre 1×240 mm², 8,7/15 kV, 33,5 mm de diâmetro externo; para 6 MW, FP 0,95, 13,8 kV e 250 m, calcula 264,23 A, queda de 13,99 V (0,101%) e perdas trifásicas de cobre de 5,02 kW. Resistência é ajustada para 105 °C; reatância 0,1 Ω/km é hipótese. Ampacidade, método de instalação, agrupamento e correções não foram verificados.
- Curto-circuito acrescenta fontes hipotéticas de 500, 1.000 e 5.000 MVA, Z de transformador 10% provisória e trecho de cabo candidato. Com 1.000 MVA e 250 m, a corrente simétrica barra/remoto é 11,95/11,51 kA com um transformador e 18,59/17,54 kA com dois em paralelo. O modelo trata a impedância da fonte como reatância e despreza resistências, contribuições de motores/conversores, assimetria, aterramento e tempo de eliminação. Não é estudo de capacidade de interrupção nem de seletividade.
- Os estudos preliminares de serviços auxiliares, UPS e bateria R8 permanecem como estimativas condicionadas às premissas originais; não foram convertidos em dimensionamentos aprovados.

## Verificações realizadas

- Revit 2027.3 abriu e gerou `19-subestacao-gis-r11.rvt`; o add-in extraiu auditoria, CSV por equipamento, CSV por relação, 14 folhas A3 e quatro vistas 3D. A extração R11 registra `GENERATED_FROM_OPEN_REVIT_DOCUMENT`, documento `19-subestacao-gis-r11`, zero avisos e zero instâncias sem identidade. O RVT foi também aberto na interface do Revit e a folha E-01 foi exibida.
- Build do add-in C# para Revit 2027, configuração Release, resultado: 0 erros e 3 avisos MSB3277 relativos a referências Autodesk/Visual Basic.
- Testes Python: `python -m unittest discover -s tests -p 'test_*.py' -q` — 41 testes passaram.
- Grafo: 62 nós, 58 relações, 5/5 placeholders, nenhum nó interno/rota geométrica ausente e 0 condutores primários confirmados.
- PDF: 14 páginas A3, 1190,52 × 841,8 pt; exportado pelo Revit e renderizado para PNG a 90 dpi para inspeção visual.

## Pendências para uma revisão de engenharia

R11 ainda não tem condutores ou circuitos Revit de potência primária, lógica de manobra/intertravamento, dados da concessionária, placas aprovadas, fabricante/GA escolhido, ampacidade ou suportação verificada, parâmetros de aterramento, dados completos das cargas auxiliares, missão de bateria confirmada, estudos de proteção/seletividade, validação civil, afastamentos ou coordenação de construção. Sem esses dados, qualquer corrente, envelope ou posição de manobra permanece de estudo. R11 não está aprovada para obra, compra, aprovação regulatória ou instalação.
