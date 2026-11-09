# Comparativo técnico — R8 × R20-23

**Escopo:** Projeto 19 — Subestação GIS 138/13,8 kV. R8 e as revisões anteriores foram preservadas. R20-23 é uma revisão de estudo, não executiva. Os dados abaixo vieram da auditoria extraída do RVT aberto no Revit 2027.3, das pranchas impressas e dos testes de contrato do gerador.

## Síntese comparativa

| Indicador verificado | R8 | R20-23 | Interpretação |
|---|---:|---:|---|
| Instâncias `FamilyInstance` | 191 | 168 | A contagem não mede desenvolvimento geométrico; parte da geometria de rotas foi criada como `DirectShape`. |
| Equipamentos elétricos | 46 | 46 | Quantidade preservada; 33 equipamentos R20-23 não têm conectores MEP. |
| Modelos genéricos | 145 | 122 | A redução decorre da revisão da organização geométrica, não de validação de fabricante. |
| `DirectShape` | 0 | 24 | R20-23 separa 12 trajetos geométricos por fase e 12 envelopes/stubs GIS. Nenhum é condutor ou circuito. |
| Conectores MEP | 93 | 25 | O total R8 não significava rede conectada. A R20-23 reduziu conectores incompatíveis ou sem ligação comprovada. |
| Conectores MEP `IsConnected` | 0 | 2 | Os dois conectores confirmados pertencem ao trecho auxiliar QAC-01–UPS-01. Não há conexão MEP de potência principal. |
| Sistemas elétricos / terminais | 1 / 1 | 1 / 1 | A R20-23 contém somente o sistema auxiliar `AUX-UPS-01`; ele não representa a rede 138/13,8 kV. |
| Fio Revit 2D | não registrado | 1 (2/2 conectores) | O fio é específico da vista E-06; não é cabo 3D nem comprova a alimentação completa da UPS. |
| Leitos nativos de cabos | 0 extraídos | 8 (8/16 conectores conectados) | As conexões confirmadas são juntas entre segmentos de infraestrutura, não ligações elétricas. |
| Rotas por fase | não extraídas | 12 `DirectShape` | Quatro transições trifásicas: GIS→TR-01, GIS→TR-02, TR-01→QMT-A-IN e TR-02→QMT-B-IN. Pontas coincidem com envelopes de família, não com portas terminais. |
| Folhas / vistas 3D | 21 / 8 | 14 / 4 | A documentação foi concentrada em 14 folhas A3; todas as 14 páginas foram renderizadas para inspeção. |
| Avisos do documento | sem comparação auditável nesta extração | 0 | `Document.GetWarnings()` retornou lista vazia na extração R20-23. Isso não substitui revisão de engenharia. |

Fonte dos números R8: `dados/reconstrucao-r8/auditoria-modelo-r8.json`. Fonte R20-23: `dados/reconstrucao-r20/auditoria-modelo-r20.json` dentro desta revisão. O quantitativo, a lista por equipamento e as relações estão nos CSVs adjacentes.

## Melhorias verificadas na R20-23

- **Conectividade documentada com limites claros:** 8 trechos nativos de CableTray têm 8 de 16 conectores `IsConnected`; o CSV separa essas juntas de infraestrutura das conexões elétricas. O fio 2D QAC-01–UPS-01 tem 2 de 2 conectores ligados. Não foram criados circuitos de potência para inflar contagens.
- **Trajetos de potência representados como geometria:** 12 formas por fase registram coordenadas, equipamento de origem/destino e distância até o envelope. A auditoria declara `electrical_connection_confirmed=false`, `terminal_port_identified=false` e `native_electrical_system_confirmed=false`. Fases e dimensões são conceituais.
- **Unifilar E-01 reconstruído:** mostra barras A/B de 138 kV, seletores 89A/89B, cinco bays, acoplador 52-BC, TCs, dois transformadores, duas seções de barra de 13,8 kV, tie MT 52-TIE e dez posições de alimentador. A folha marca estados e intertravamentos como premissas, não como lógica validada.
- **Detalhe de equipamento concentrado:** a BIM-02 apresenta o bay GIS 145 kV com envelope externo translúcido para leitura da geometria. A BIM-03 e a BIM-04 mostram o transformador e a seção de cubículos. Dimensões de referência F35 publicadas foram separadas de estimativas e geometrias de estudo; nenhum interior OEM foi inferido.
- **Caminhos de infraestrutura documentados:** a extração confirma 4 aberturas geométricas em paredes e registra 21 paredes, 7 pisos, 8 leitos e 24 formas de rota. A abertura geométrica não confirma vedação, rating corta-fogo ou aprovação construtiva.
- **Cálculos com limitações explícitas:** M-01 conserva os valores preliminares e sensibilidades documentados nas revisões anteriores. Não foi executado um novo estudo de fluxo/corrente de falta para R20-23; não há dados suficientes para declarar ampacidade, seletividade ou dimensionamento final.

## Execução e validação realizadas

1. O RVT `19-subestacao-gis-r20.rvt` foi aberto na sessão Autodesk Revit 2027.3. A extração retornou `GENERATED_FROM_OPEN_REVIT_DOCUMENT`, 168 instâncias, 25 conectores MEP, 1 sistema, 1 Wire 2D, 8 CableTrays e zero avisos.
2. A auditoria registra quatro vistas 3D sem template, com 21–379 elementos visíveis reportados pela API, sem isolamento temporário. A área de desenho da janela do Revit permaneceu visualmente em branco nesta sessão; portanto, a conferência visual das vistas foi feita pelas folhas PDF renderizadas, não por uma captura confiável da tela de trabalho.
3. A DLL executada foi `staging-build-r20-visualaudit-27/PortfolioExpansion.dll` (432.128 bytes; SHA-256 `96B61588377435C4FE06FC4D972F2C04D9334857E2B082BA9B478D4CFCEE0706`). Os 26 testes de contrato do gerador R20-23 passaram; a [saída de teste](validacao-testes-revit-generator.txt) foi preservada. Eles verificam o contrato de geração e documentação; não são validação elétrica nem certificação de engenharia.
4. O PDF contém 14 folhas A3, sem criptografia. As 14 páginas foram renderizadas em PNG a 120 dpi; todas contêm desenho/texto. Foram inspecionadas em resolução maior as folhas A-01, BIM-01, E-01, E-02 e Q-01, além da visão geral das 14 folhas. O contato e os PNGs individuais estão em `pranchas/reconstrucao-r20/preview/`.
5. O Exportar PDF nativo do Revit falhou nesta instalação com a mensagem de renderer/material libraries indisponíveis. A Autodesk documenta que esse aviso pode ocorrer quando a biblioteca de materiais não está instalada ([troubleshooting oficial](https://help.autodesk.com/cloudhelp/2022/ENU/Revit-Troubleshooting/files/GUID-652639E1-0629-451F-8953-DE6BF43C0422.htm)). Como contingência, o Revit imprimiu as 14 folhas via Microsoft Print to PDF em papel Carta; a cópia A3 entregue foi obtida escalando uniformemente as páginas vetoriais e ajustando suas MediaBoxes para A3. O PDF Carta original está preservado ao lado para rastreabilidade. Não houve redesenho do conteúdo nessa normalização.
6. As tentativas de criar 11 sistemas auxiliares CA não satisfizeram as verificações da API para painel, terminal, carga e dados calculados. Todas foram revertidas. O RVT final conserva as declarações e pendências no relatório, sem tratá-las como circuitos existentes.

## Pendências que permanecem

1. Conexões efetivas da rede primária, contatos internos GIS, entradas dos transformadores, ligações dos transformadores aos barramentos MT e saídas dos dez alimentadores não existem como circuitos/cabos conectados no RVT.
2. A continuidade geométrica até um envelope não prova terminal, conector, conexão elétrica, seção, ampacidade ou raio mínimo de curvatura.
3. Permanecem pendentes dados aprovados de fonte, placas/impedâncias de transformadores, estudo de curto-circuito completo, aterramento, cargas, parâmetros de proteção, coordenação, intertravamentos, fabricante/GA, instalação de condutores e dados da bateria/UPS.
4. O canvas 3D da sessão Revit permaneceu em branco apesar da extração da API e das vistas impressas. Para uma inspeção gráfica dentro da janela do Revit, ainda é necessário resolver essa condição local de visualização.

**Conclusão:** R20-23 melhora a documentação da topologia, o registro de infraestrutura física, os trajetos geométricos por fase e a distinção entre conexão real e desenho. A ligação elétrica principal continua documental/geométrica, não conectada no modelo. A revisão está validada como estudo BIM demonstrativo e não deve ser apresentada como projeto executivo, dimensionamento aprovado ou seletividade comprovada.
