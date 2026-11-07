# Comparativo técnico — Subestação GIS R8 × R9

**Escopo:** somente o Projeto 19, Subestação GIS 138/13,8 kV. A R8 e seus arquivos de origem foram preservados. Esta revisão registra o que foi efetivamente gerado no Revit 2027.3, os resultados calculados e as pendências que impedem tratar o modelo como projeto executivo.

## Resumo da revisão

| Indicador extraído | R8 | R9 | Leitura |
|---|---:|---:|---|
| Instâncias de família | 191 | 169 | Redução de instâncias não representa redução de equipamentos; a R9 substitui parte das curvas de rota por geometria DirectShape. |
| Equipamentos elétricos / modelos genéricos | 46 / 145 | 45 / 124 | Famílias e parâmetros de identidade continuam auditáveis; as famílias de potência permanecem sem conectividade elétrica. |
| DirectShapes de rota/carcaça | 0 | 84 | Geometria representativa, sem conectores elétricos; não equivale a condutor instalado. |
| Curvas de modelo | 180 | 175 | Rotas de desenho antigas foram parcialmente substituídas por formas de fase e invólucros. |
| Leitos nativos CableTray | não separados como categoria na auditoria R8 | 8 | R8 lista 29 instâncias da família genérica `GIS_CableTray`; elas não foram extraídas como leitos nativos. Na R9, 16 conectores de leito; 8 marcados `IsConnected`, formando quatro juntas internas nas duas rotas MV. As extremidades e os demais leitos seguem abertos. |
| Conectores MEP em equipamentos | 93 | 3 | R8: zero `IsConnected`. R9: 2 conectores `DomainElectrical` conectados, QAC-01 e UPS-01; o terceiro é lógico e não está fisicamente conectado. |
| Sistemas elétricos / terminais | 1 / 1 | 1 / 1 | O sistema é somente o auxiliar `AUX-UPS-01`; seu terminal é UPS-01. Não representa a rede primária ou os alimentadores. |
| Fios Revit 2D | não discriminados na auditoria R8 | 1 | Um `Wire` nativo, view-specific no plano E-06, com 2/2 conectores confirmados entre QAC-01 e UPS-01. Não é cabo 3D nem comprova a alimentação completa da UPS. |
| Folhas / vistas 3D | 21 / 8 | 14 / 4 | A R9 concentra a documentação e mantém quatro vistas 3D focadas. PDF final: 14 folhas A3. |
| Avisos do documento Revit | não informado na auditoria R8 | 0 | Contagem retornada por `Document.GetWarnings()` na extração R9; não substitui revisão de engenharia. |

Os quantitativos vêm das auditorias `auditoria-modelo-r8.json` e `auditoria-modelo-r9.json`, e dos relatórios de conectividade gerados a partir do RVT aberto. As contagens de conectores são distintas de famílias de equipamentos e de elementos de geometria.

## Conectividade: o que existe e o que falta

| Trecho / relação | Estado no RVT R9 | Evidência ou pendência |
|---|---|---|
| QAC-01 → UPS-01 | Um `ElectricalSystem` auxiliar e um fio 2D nativo; ambos os conectores elétricos do fio estão conectados. | Confirmado por `IsConnected`/`AllRefs` e listado em E-06. Sem cabo 3D, dimensionamento, circuito de saída CC ou demonstração da fonte a montante. |
| Leitos MV A e B | Três trechos nativos por rota, com duas juntas conectadas em cada. | `ConnectTo` e `IsConnected` foram registrados para as quatro juntas. Leito é infraestrutura, não circuito, não contém condutores e não está conectado eletricamente aos equipamentos. |
| Entrada L01/L02 → seletores GIS → barras A/B | Relação topológica representada em E-01 e por invólucros geométricos. | Não há conectores/circuitos de potência no RVT; contato e posição de manobra são premissas documentadas. |
| Barras GIS → disjuntor/seccionadoras do acoplador 52-BC | Aparece no diagrama como acoplador de dupla barra, normalmente aberto. | Carcaças/stubs geométricos; contatos, intertravamento e conexão de potência não foram modelados. |
| Bay GIS T01/T02 → buchas AT dos transformadores | Três caminhos geométricos separados por fase para cada transformador. | DirectShapes aproximados. Sem terminais físicos conectados, barramento selecionado ou cabo/busduct escolhido. |
| Buchas MT dos transformadores → cubículos de entrada QMT-A-IN/QMT-B-IN | Três caminhos de geometria por fase, mais leitos de infraestrutura A/B. | Sem condutor/circuito, seleção cabo ou barramento, terminações, ocupação de leito e verificação de ampacidade. |
| Barras de 13,8 kV → 10 alimentadores | Relação documentada no unifilar e posições de cubículos. | Não há circuitos Revit, cabos de saída, destino, carga terminal ou dimensionamento de alimentador. O tie 52-TIE permanece premissa normalmente aberta. |

O relatório por equipamento é `conectividade-equipamentos-r9.csv`; as relações são separadas em `relacoes-rede-r9.csv`. A extração lista os equipamentos sem conectores MEP em vez de inferir ligação elétrica pela proximidade geométrica.

## Unifilar E-01

O E-01 foi reconstruído para mostrar duas barras de 138 kV, os seletores 89A/89B por bay, disjuntores 52, acoplador 52-BC, TCs, dois bays de transformador, barras seccionadas de 13,8 kV, dez alimentadores e acoplador MT 52-TIE. O estado representado como hipótese coloca L01 e TR-01 na Barra A, L02 e TR-02 na Barra B, e ambos os acopladores normalmente abertos. A folha explicita que estados, intertravamentos, ajustes e seletividade ainda precisam de validação.

As marcas E-01 (GIS-L01, GIS-T01, GIS-BC01, GIS-T02, GIS-L02, TR-01/02, QMT-A/B, FDR-A/B) correspondem às marcas e relações exportadas no quantitativo e CSV de conectividade. O unifilar declara as relações documentais; a auditoria deixa claro que não são circuitos conectados no Revit.

## Referências dimensionais e estado da geometria

As referências abaixo orientam classe e envelope de estudo. Nenhuma representa seleção de fornecedor aprovada nem confirma que o modelo é uma família de fabricante.

| Equipamento de referência | Dado publicado usado | Tratamento na R9 |
|---|---|---|
| GIS GE Vernova F35 Dual Gas | Classe até 145 kV; a brochura publica baia de 800 × 3.700 × 2.500 mm. [Brochura F35 145 kV](https://www.gevernova.com/grid-solutions/sites/default/files/2026-02/F35-145-kV-Dual-Gas-GIS-EN-33282-202504-LTR-R002_0.pdf) | Referência de classe e envelope. Famílias GIS R9 são estudo geométrico; não incorporam contatos internos, compartimentos pressurizados, acionamentos e GA de fabricante. Confirmar dimensões e configuração antes de substituir pelo arranjo de referência. |
| Transformador DAX TRP-EA40-DML-01 | 138/13,8 kV, 30/40 MVA, Dyn1, LTC, Z=11% e óleo mineral. [Catálogo de transformadores DAX](https://dax.energy/produtos/transformadores-de-potencia) | Produto de referência, não selecionado. R9 preserva os valores preliminares R8 de 40 MVA e Z=10% para não misturar a hipótese do estudo com os dados de outro produto. Tanque, buchas, radiadores, ventiladores e afastamentos continuam aproximados; perdas, dimensões GA e acessórios finais dependem de folha de dados aprovada. |
| Cubículo ABB UniGear ZS1 | Linha até 17,5 kV; manual apresenta larguras e dimensões variáveis conforme corrente e configuração, incluindo painel de 800 mm para opções de 2.000 A em certas configurações. [Manual ABB UniGear ZS1](https://library.e.abb.com/public/9b13db70cee34a58a50ca7978b17fb67/UniGear%20ZS1_IOM%20instruction%20manual_1VLM000363_RevO_EN.pdf) | Referência de classe e dimensional para 13,8 kV, não modelo ABB selecionado. Frente, compartimentos e profundidade dos cubículos modelados são representativos; barramento interno, carro do disjuntor, obturadores e terminação não foram fabricados sem documentação específica. |
| Cabo Nexans EPR105-PVC 8,7/15 kV 1×240 mm² Cu | Diâmetro externo 33,5 mm, 105 °C contínuo e 250 °C em curto, conforme ficha publicada. [Ficha Nexans 1×240 mm²](https://www.nexans.com.br/pt/products/Transmission.and.Distribution/Insulated.Copper-Aluminium-CablesForMediumVoltage/Nexans-EPR105-PVC/Nexans-EPR37761/product~ID540477955~.html) | Candidato apenas para sensibilidades elétricas; não foi inserido como cabo no RVT nem selecionado para os alimentadores. A ampacidade depende da instalação e permanece pendente. |

A API do Revit separa `Wire` (fio 2D associado a vista de planta/teto) de `CableTray`, e conectores só se ligam quando os seus domínios e tipos são compatíveis. A leitura dessa API fundamenta a opção por registrar as rotas de alta tensão como geometria e não forçar conectores incompatíveis: [Wire API 2027](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-API/files/Revit_API_Developers_Guide/Discipline_Specific_Functionality/MEP_Engineering/MEP_Element_Creation/Revit_API_Revit_API_Developers_Guide_Discipline_Specific_Functionality_MEP_Engineering_MEP_Element_Creation_Creating_Wires_html.html), [Connector.ConnectTo](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/04ee99c9-f411-aabe-7b87-013a6f9adb1d.htm), [CableTray.Create](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/9c48be09-8466-3ad1-85a4-c0ac1eb0f9ed.htm).

## Cálculos mantidos e acrescentados

- Os identificadores BP-01 a BP-15 e a base R8 foram preservados. Correntes de carga dos alimentadores foram mantidas como corrente, sem inferir seção ou ampacidade.
- A extensão de cabos calcula queda de tensão e perdas para 4/6 MW, FP 0,95, rotas de 100/250/500 m e parâmetros candidatos explícitos. No caso de 6 MW e 500 m: 264,2 A, 0,203% e 10,037 kW de perda resistiva estimada.
- A corrente de curto foi separada em três sensibilidades de fonte (500/1.000/5.000 MVA), um ou dois transformadores e cabo candidato. Não substitui o equivalente da concessionária, ensaios/placa do transformador, contribuições de motores/conversores, análise assimétrica, aterramento ou tempos de proteção.
- A resistência do cobre usa 100% IACS e 105 °C; X=0,1 Ω/km é hipótese. Não se calculou ampacidade, perdas reais do transformador, autonomia de bateria, demanda CA completa, proteção ou seletividade, pois faltam dados de entrada.
- Os testes automáticos verificam soma e coerência de cargas, monotonicidade das sensibilidades, contribuição de transformadores em paralelo e ausência de alegação de ampacidade. Todos passaram.

## Execução e validação

- Revit 2027.3 abriu `19-subestacao-gis-r9.rvt`; o título da janela indicou o documento R9 e a vista de implantação. O add-in registrou o assembly `staging-build-r17` no journal de execução.
- A extração produz `status=GENERATED_FROM_OPEN_REVIT_DOCUMENT`, lista de famílias/tipos, equipamentos, parâmetros, marcas, conectores, referências, sistemas, fios, leitos, rotas geométricas e folhas. `Document.GetWarnings()` retornou lista vazia na execução final.
- O modelo e o PDF foram salvos e exportados pelo Revit: `revit/reconstrucao-r9/19-subestacao-gis-r9.rvt` e `pranchas/reconstrucao-r9/19-subestacao-gis-r9.pdf` (14 folhas A3). Foram inspecionados visualmente o E-01, o E-06 e as quatro vistas 3D exportadas.
- Compilação do add-in: 0 erros; 3 avisos de referência `Microsoft.VisualBasic` reportados pelo build.
- Testes: `python -m unittest tests.test_gis_substation_r9 tests.test_gis_substation_study tests.test_gis_substation_auxiliary` — 14 testes aprovados.

## Pendências que bloqueiam detalhamento executivo

1. Desenvolver uma representação de potência validada para entradas, bays GIS, transformadores, barras MT, acopladores e alimentadores; criar condutores/circuitos compatíveis somente onde o Revit os suporte e registrar cada ligação pendente.
2. Obter GA, folhas de dados e seleção de fornecedor dos bays, transformadores, cubículos, cabos, terminações, suportes e acessórios; reconciliar envelopes, cotas e afastamentos de manutenção.
3. Definir fonte, carregamento, impedâncias reais, aterramento, instalação dos condutores, dimensionamento térmico, coordenação e intertravamentos.
4. Completar a arquitetura auxiliar CA/CC, cargas, saída da UPS, capacidade e missão de bateria, proteção e autonomia.

**Conclusão:** a R9 é um avanço verificável na separação entre ligações elétricas reais, geometria de infraestrutura e relações do unifilar, além de acrescentar estudo paramétrico e detalhe externo. A rede principal de potência ainda não é uma rede elétrica conectada no Revit, e os equipamentos permanecem envelopes de estudo. A revisão não é projeto executivo nem comprova dimensionamento, seletividade ou aptidão para construção.
