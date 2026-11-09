# Add-in do portfólio para Revit 2027

Este projeto C# usa a API instalada do Revit 2027 e o alvo .NET 10. A execução mais recente gerou a candidata R20-23 do Projeto 19, Subestação GIS 138/13,8 kV. A documentação da Autodesk sobre a migração da API para .NET 10 está em [Revit 2025 API changes](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-WhatsNew/files/GUID-8D7A4715-EAF8-4BD1-BE78-061F900D0BCE.htm).

## Limites do modelo elétrico

- A rede primária de 138 kV e a distribuição de potência de 13,8 kV são documentadas no unifilar e em geometrias representativas. No estado validado R19, elas não formam circuitos de potência nativos no Revit.
- `CableTray` representa infraestrutura de encaminhamento. `DirectShape` representa trajetos geométricos. Nenhum dos dois comprova condutor instalado ou continuidade elétrica.
- O sistema auxiliar `QAC-01 → UPS-01` foi auditado como relação lógica; o Wire existente é 2D e específico de uma vista. Isso não comprova cabo 3D nem toda a alimentação da UPS.
- A revisão R20 separa no `ProtectionPanel` a interface de controle em 125 Vcc da conexão de aquecedor em 220 Vca. Os circuitos CA só devem ser contados se o próprio Revit confirmar tipo, painel, terminal, identificação e valores calculados. Essa tentativa ainda não foi executada no Revit.
- Envelopes de equipamentos e detalhes internos permanecem representações de estudo; valores de catálogo, dimensões estimadas e itens pendentes são identificados nos dados R20.

## Preparação e execução

- Compile a DLL em uma pasta de staging. Não substitua a DLL de `bin` durante a preparação de uma revisão para auditoria.
- O manifesto `.addin` é local à máquina. Confirme que seu caminho de assembly aponta exatamente para a compilação autorizada antes de carregar o add-in.
- `batch-range.txt` limita a execução. O conteúdo `DISABLED` impede o disparo automático do gerador neste momento; a revisão em preparação é o Projeto 19, sem lote de outros empreendimentos.
- O add-in usa o evento `Idling` do Revit. Depois de qualquer execução autorizada, confira o log, abra o RVT gerado, inspecione as vistas diretamente, audite tabelas/conectores/sistemas e revise o PDF. Um build bem-sucedido não substitui essas verificações.

## Execução R20-23 — 09/11/2026

- A DLL `staging-build-r20-visualaudit-27/PortfolioExpansion.dll` (432.128 bytes; SHA-256 `96B61588377435C4FE06FC4D972F2C04D9334857E2B082BA9B478D4CFCEE0706`) foi executada no Revit 2027.3 somente para o Projeto 19, com saída em `projetos/19-subestacao-gis/attempts/r20-23-candidate/`.
- A extração do documento aberto registrou 168 instâncias, 46 equipamentos elétricos, 122 modelos genéricos, 24 DirectShapes, 8 CableTray, 25 conectores MEP (2 ligados no auxiliar), 1 ElectricalSystem, 1 Wire 2D, 14 folhas, 4 vistas 3D e zero avisos do documento. O relatório comparativo inclui os limites e pendências.
- Os 12 trajetos por fase são DirectShapes entre envelopes de família, não condutores ou portas terminais. Oito conectores de CableTray formam juntas de infraestrutura. A rede principal 138/13,8 kV não tem circuito/cabo nativo conectado; as tentativas auxiliares sem leitura coerente de carga foram revertidas.
- Os 26 testes `tests.test_revit_generator_contract` passaram. O PDF nativo do Revit não foi exportado nesta instalação por indisponibilidade de renderer/bibliotecas de material; foi impresso via Microsoft Print to PDF e normalizado uniformemente para páginas A3. As 14 folhas foram renderizadas e revisadas.
- O arquivo abriu na sessão Revit 2027.3 e a API extraiu o documento; a área gráfica da interface ficou em branco. A inspeção visual das vistas foi feita nas folhas PDF renderizadas. Não declarar validação visual dentro do canvas.
- O manifesto local foi restaurado para `bin/PortfolioExpansion.dll` (SHA-256 `A8FC62E8A5D454953CB19DD5658C7814A06B793713D4832768ABB18D695001B6`) e `batch-range.txt` está `DISABLED`. Nenhum outro empreendimento ou post do LinkedIn foi executado.

## Histórico de execução R20-22 — 09/11/2026 (superada pela R20-23)

- A DLL staging-build-r20-layout-review-26/PortfolioExpansion.dll (426.496 bytes; SHA-256 FEC97084AB0424A7AC2C5D8244B8C955A414FC04B04C59804F928BB665DEEDFD) foi executada uma vez no Revit 2027.3, limitada a BATCH_PROJECTS 19. A compilação teve 0 erros e 3 avisos MSB3277.
- Foram salvos RVT e auditoria JSON/CSV, exportadas 14 folhas A3 e quatro previews 3D. O documento registrou zero avisos do Revit. O PDF foi renderizado e conferido; Q-01 agora usa F35 REF. e não transborda.
- Auditoria: 168 instâncias, 46 equipamentos elétricos, 24 DirectShapes de geometria, 8 CableTray, 25 conectores MEP, 2 conectores elétricos ligados ao Wire auxiliar 2D, 1 ElectricalSystem auxiliar, 33 equipamentos sem conector MEP e nenhum condutor físico primário. Os conectores de CableTray ligam trechos de infraestrutura; não ligam equipamentos.
- O RVT salvo foi aberto manualmente em Revit 2027.3 e a folha E-01 e a vista 3D-01 aparecem no Navegador, mas ambos os canvases permaneceram visualmente vazios após espera e reenquadramento. Testei Cores consistentes e gráficos acelerados na vista 3D sem resultado, restaurei Sombreado e desliguei a aceleração sem salvar o RVT. A inspeção visual direta dentro do Revit não foi aprovada; as capturas de E-01 e 3D-01 estão preservadas no diretório da candidata. Não inferir sucesso de tela a partir da exportação PDF.
- A versão é candidata parcial e não está liberada para publicação: as conexões HV/MV seguem diagramáticas/geométricas e não há circuitos nativos de potência. O [comparativo R8 × R20-22](../../projetos/19-subestacao-gis/dados/reconstrucao-r20/relatorio-comparativo-r8-r20-22.md) diferencia avanços e limitações.
- O manifesto foi restaurado para bin/PortfolioExpansion.dll (hash BB3B5D0BE2E14829DADD5945AA1041E72F0311AD6F6858D2FB31A5816C177E4C) e batch-range.txt para DISABLED. Nenhum outro projeto foi executado nem post de LinkedIn publicado.

## Histórico de preparação R20 — 08/11/2026 (antes da execução R20-22)

- Duas execuções isoladas no Revit 2027.3 rodaram somente o Projeto 19. A primeira parou antes de salvar por erro de orientação da câmera; a segunda passou pela criação das vistas e folhas, salvou um RVT e extraiu quantitativos, mas parou porque `ElectricalSystemType` foi lido em um conector fora de `DomainElectrical`.
- O RVT e o CSV parciais da tentativa 2 estão preservados sob `projetos/19-subestacao-gis/{revit,dados}/reconstrucao-r20/attempts/r20-02-model-saved-quantity-export-failed/`; não foram promovidos como entrega. Não há PDF/PNG nem auditoria final R20 e o modelo parcial ainda precisa de inspeção visual.
- A correção atual consulta o tipo de sistema apenas para conectores de `DomainElectrical`; demais domínios recebem `NOT_APPLICABLE`. O grafo também marca ElementIds/UniqueIds ausentes ou duplicados como identidade não verificável. Suíte completa: 121 testes aprovados; compilação: 0 erros e 3 avisos `MSB3277`.
- DLL mais recente em staging: `staging-build-r20-audit-safe-05/PortfolioExpansion.dll`, 391.168 bytes, SHA-256 `E48667069CF5D462C2F19ADCDA632C34190074B515D33DAAAF132D4CB61E5AB9`. Ela ainda não foi carregada; aguarda autorização específica antes de executar outra vez no Revit.
- O manifesto local está restaurado para `bin/PortfolioExpansion.dll` e `batch-range.txt` está em `DISABLED`. As revisões R8/R19 e os 30 RFA existentes foram preservados. Nenhum outro projeto foi executado.
- O comparativo R8 versus R20 continua provisório até salvar a revisão completa, auditar conectores/sistemas, abrir o RVT, revisar vistas e exportar/verificar o PDF e as prévias.

## Atualização: envelope F35 revalidado e build R20-09 — 08/11/2026

- A brochura GE Vernova F35-145 de 2025 publica `800 × 3.700 × 2.500 mm` para uma baia digital SF₆. Os dados, rótulos e testes agora classificam essas medidas como referência da configuração publicada, não como GA de pedido ou folga de instalação. A geometria da família é de suportes/envelope simplificados.
- O E-01 agora explicita 52-BC e 52-TIE abertos, 89T-A/B fechadas e os estados 89A/89B/89G por bay. Os marcadores `*` identificam hipóteses sem lógica de intertravamento nativa.
- Para preservar os 30 RFA da tentativa parcial com metadados antigos, o próximo lote gravará as famílias em `projetos/19-subestacao-gis/familias/reconstrucao-r20-catalog-f35-2025/`.
- A suíte completa atual passou com 126 testes; `compileall` e `git diff --check` passaram, com avisos LF/CRLF já existentes. Build Revit 2027: 0 erros e 3 avisos `MSB3277`.
- DLL isolada: `staging-build-r20-audit-safe-09/PortfolioExpansion.dll`, 392.192 bytes, SHA-256 `2EFCE6B34F79B690C6AB4510397BC01614BCB88F26931DF1EA0BF865723C3E47`. Ela ainda não foi carregada no Revit.
- O manifesto local continua apontando para `bin/PortfolioExpansion.dll`; `batch-range.txt` continua `DISABLED`. A geração, extração nativa, abertura visual do RVT e conferência de todas as folhas ainda estão pendentes.

## Preparação de layout R20-10 — 08/11/2026

- O log parcial R20-02 mediu excesso vertical em E-01/M-01/Q-01 e viewports de detalhe com caixas incompatíveis com os textos em tamanho de papel. A R20-10 aumenta as escalas para 55/70/70 e usa texto técnico de 1,8 mm nessas três vistas; os valores finais ainda precisam ser medidos no Revit.
- BIM-02/03/04 agora usam toda a área útil para cada vista 3D. Os detalhes funcionais continuam nas folhas E-02/03/04; os mini viewports suplementares que geravam caixas enormes foram removidos.
- Suíte completa: 126 testes aprovados; `compileall` passou. Revit 2027 foi compilado em staging com 0 erros e 3 avisos `MSB3277`.
- DLL isolada: `staging-build-r20-audit-safe-10/PortfolioExpansion.dll`, 392.192 bytes, SHA-256 `B5C94DB8A1A2C488CF684642C5FCD30D8C99F7F4D0EBB7D7D128A64D5994AC9F`. Essa build ainda não foi carregada.
- O manifesto permanece apontando para `bin/PortfolioExpansion.dll` e `batch-range.txt` permanece `DISABLED`. A aprovação anterior foi para `AAD48B54…FDDAD504`, já usada na tentativa R20-02 e supersedida. As builds safe-09, safe-10 e safe-11 não têm autorização específica de carga.

## Integração de cálculos auxiliares R20-11 — 08/11/2026

- O M-01 agora exibe resultados reproduzidos da linha de base R8: demanda CA 57,938 kW, 64,375 kVA e 64,38% de um transformador candidato de 100 kVA; bateria 125 Vcc com 194,82 Ah calculados/200 Ah candidatos; e energia preliminar da UPS de 3,791 kWh. Cada valor aparece como hipótese R8, não como especificação aprovada.
- A reprodução R8 tem 23 linhas e é comparada diretamente com `servicos-auxiliares-r8.json` pelo teste `test_r20_r8_baseline_reproduction_matches_current_calculation`.
- Suíte completa: 127 testes aprovados; `compileall` passou. Build Revit 2027: 0 erros e 3 avisos `MSB3277`.
- DLL em staging: `staging-build-r20-audit-safe-11/PortfolioExpansion.dll`, 393.728 bytes, SHA-256 `814911693EE23DE78212B54DDDD0E1895347AE5A7F584524613153FC3C98DFEC`. Não carregada no Revit.
- O manifesto continua em `bin/PortfolioExpansion.dll` e `batch-range.txt` em `DISABLED`. Esta build aguarda autorização para a hash exata; nenhuma geração ou validação nativa R20-11 existe ainda.

## Candidato visual R20-12 — 08/11/2026

- E-01 conecta a derivação gráfica de 89G-BC ao nó do acoplador e desloca o rótulo para fora dos rótulos de 89A/89B/52-BC.
- O envelope ilustrativo da barra MT foi colocado no topo da fileira de cubículos; o parâmetro de localização acompanha a posição. O comentário preserva que condutor interno, suportes comerciais e ratings não estão representados.
- Q-01 foi reorganizada em duas colunas, com texto de 2,0 mm e escala 1:60 para aproveitar melhor a folha.
- Build em staging: `staging-build-r20-audit-safe-13/PortfolioExpansion.dll`, 396.288 bytes, SHA-256 `FCC23E2277202A77D218ECCC049A8C69A5954855EF3B38D33CD61FE185FC02BE`; compilação com 0 erros e 3 avisos `MSB3277`.
- O Revit 2027.3 gerou RVT, PDF A3 de 14 folhas, quatro previews 3D e 31 famílias RFA na pasta isolada `projetos/19-subestacao-gis/attempts/r20-12-candidate/`; R20-11 foi preservada.
- A extração do documento aberto registrou 168 elementos, 25 conectores MEP (2 conectados), 1 sistema lógico, 8 conectores CableTray (8 marcados conectados), 1 Wire 2D e 11 tentativas de circuitos auxiliares revertidas. Existem 4 elementos Opening; a auditoria R20-12 falhou em classificá-los como passagens de rota porque dependia de comentários vazios (`EXPECTED_4_ROUTE_WALL_OPENINGS_FOUND_0`).
- A inspeção visual no Revit e no PDF confirmou a exportação de E-01, M-01 e das vistas 3D. Q-01 tem sobreposição de códigos longos; a caixa do acoplador ainda encosta visualmente na barra B; BIM-02/03/04 precisam de recorte mais justo. R20-12 fica preservada como candidata não publicável. A rede primária 138/13,8 kV continua sem circuito MEP Revit.
- A inspeção visual R20-12 também encontrou textos de nota cortados nas folhas E-02/E-03/E-04 e a malha conceitual de aterramento sem legenda suficiente em E-05. Esses defeitos permanecem registrados como pendências até a nova geração.

## Execução e inspeção do candidato R20-13 — 08/11/2026

- O Revit 2027.3 gerou RVT, PDF A3 de 14 folhas, quatro previews 3D e 30 famílias RFA em `projetos/19-subestacao-gis/attempts/r20-13-candidate/`. R20-11/R20-12 foram preservadas.
- A extração do documento aberto registrou 168 instâncias (46 equipamentos elétricos e 122 modelos genéricos), 4 aberturas, 175 curvas, 8 leitos CableTray/16 conectores, 25 conectores MEP (2 `IsConnected`), 1 sistema elétrico lógico e 1 Wire 2D com 2 extremidades conectadas. As quatro aberturas foram atribuídas por contagem esperada, não por identidade geométrica; isso foi rejeitado como classificação insuficiente e motivou a revisão seguinte.
- Inspeção visual do PDF confirmou notas legíveis em E-02/E-03/E-04, rótulo 89G-BC sem sobreposição e Q-01 legível. O desenho principal de E-02/E-03/E-04 ainda ocupa pouco espaço de folha. O modelo conserva conectividade auxiliar lógica e não possui circuito MEP de potência primária nem cabo 3D.
- A R20-13 permanece como candidata isolada; não foi publicada. A compilação global `compileall scripts tests` continua limitada por uma string sem fechamento já existente em `scripts/generate_bim_mep_model.py:22`.

## Execução e inspeção da candidata R20-14 — 08/11/2026

- A DLL `staging-build-r20-r14-layoutfix-01/PortfolioExpansion.dll` (396.288 bytes; SHA-256 `332F0AB71611D19D3AD8327085C0A821F5317DE4E64E8CE475651B50688CEE8E`) foi carregada no Revit 2027.3 para executar somente o Projeto 19, em `projetos/19-subestacao-gis/attempts/r20-14-candidate/`.
- Build: 0 erros e 3 avisos `MSB3277` de referências Microsoft.VisualBasic. As verificações anteriores à execução registraram 128 testes aprovados, `compileall scripts/engineering tests` aprovado e `git diff --check` aprovado com avisos de conversão LF/CRLF.
- O Revit gerou RVT, PDF A3 de 14 folhas, quatro previews 3D e 30 famílias GIS mais a família de carimbo. O RVT foi aberto no Revit 2027.3 e a vista 3D `3D-03 - Transformador TR-01 R20` foi conferida na tela. A API registrou 168 instâncias, 46 equipamentos elétricos, 122 modelos genéricos, 21 paredes, 7 pisos, 72 DirectShape, 175 curvas de modelo, 4 aberturas de rota, 8 CableTray, 25 conectores MEP, 1 sistema elétrico e 1 Wire 2D; zero avisos do documento.
- As quatro aberturas foram associadas por caixa geométrica a IDs de rota, tolerância 25 mm; nenhuma ficou sem classe. O grafo integrado tem 73 nós e 69 relações; 60 geometrias candidatas a condutor foram observadas, nenhuma aceita como condutor 3D. Rotas HV/MV continuam representativas, sem circuito primário nativo. Os conectores CableTray comprovam somente juntas de infraestrutura.
- A inspeção do PDF encontrou ganho de escala em E-02/E-03/E-04; E-03/E-04 ainda necessitam revisão de composição. R20-14 permanece candidata não publicável e não substitui R20-13/R20-11.
- O manifesto do usuário foi restaurado para a DLL original do diretório `bin`; SHA-256 `A8FC62E8A5D454953CB19DD5658C7814A06B793713D4832768ABB18D695001B6`. `batch-range.txt=DISABLED`; `bin/PortfolioExpansion.dll` manteve SHA-256 `BB3B5D0BE2E14829DADD5945AA1041E72F0311AD6F6858D2FB31A5816C177E4C`.
- Relatório comparativo da candidata: `projetos/19-subestacao-gis/attempts/r20-14-candidate/dados/reconstrucao-r20/revisao-r20-13-r20-14.md`.

## Preparação de geometria de rotas R20-15 — 08/11/2026

- A geração foi alterada para agrupar os cinco segmentos de cada faixa geométrica A/B/C em uma DirectShape por rota: 12 elementos no total para as quatro ligações GIS/transformador e transformador/cubículo. Os rótulos A/B/C descrevem faixas geométricas; a fase elétrica não foi atribuída.
- A extração pelo Revit registra ElementId/UniqueId, sólidos, volume, vértices e bounding box real da DirectShape. Mede também o menor afastamento de cada ponta ao `get_BoundingBox(null)` da FamilyInstance identificada, com tolerância de estudo de 25 mm. Essa medição não identifica porta/terminal e não comprova conexão MEP.
- A candidata R20-15 corrige os centros de saída no envelope do TC e ajusta a chegada das rotas MT ao limite geométrico dos cubículos. Cabo, busduct, contato GIS e circuitos primários seguem não implementados.
- Suíte completa: 131 testes aprovados; `compileall scripts/engineering tests` e `git diff --check` aprovados. Build Revit 2027: 0 erros e 3 avisos `MSB3277` preexistentes de referências Microsoft.VisualBasic.
- DLL isolada: `staging-build-r20-routeaudit-16/PortfolioExpansion.dll`, 421.888 bytes, SHA-256 `03E260C95A900EA10BD8EC58520616088FEA63B56E565B7411C426FBC7AFCF78`. Não carregada no Revit; autorização anterior era para outra hash.
- Nenhuma saída R20-15 foi gerada ainda. `projetos/19-subestacao-gis/attempts/r20-15-candidate/` não existe; R20-14 e revisões anteriores permanecem intactas. O manifesto local continua em `bin/PortfolioExpansion.dll` e `batch-range.txt=DISABLED`.

## Preparação R20-16 — conector primário da carga auxiliar — 08/11/2026

- A auditoria da execução R20-14 encontrou zero corrente/carga calculada nos conectores de aquecedor CA dos 11 painéis; o helper não encontrou parâmetros de corrente (`candidate_count=0`). As tentativas de PowerCircuit foram revertidas e permanecem classificadas como não criadas.
- Hipótese de correção: no `ProtectionPanel`, o conector `Controls` de 125 Vcc é criado antes do conector de carga `PowerCircuit` de 220 Vca. A API agora chama `AssignAsPrimary()` no conector CA, confirma `IsPrimary` antes de calcular a carga e registra a propriedade no log. A documentação Autodesk informa que os dados elétricos da família exibidos em tabelas derivam do conector primário e permite reatribuí-lo; isso torna a mudança plausível, mas somente a execução no Revit poderá confirmar o efeito.
- A política de execução não mudou: cada circuito auxiliar continua sendo revertido se o Revit não confirmar tensão, potência, FP, painel e terminal; não se aceita circuito com carga nula. Cabo 3D, circuito de potência GIS/MT e conexão aos terminais GIS continuam não implementados.
- Verificações locais: `python -m unittest discover -s tests -v` — 132 aprovados; `python -m compileall -q scripts/engineering tests` — aprovado; build Revit 2027 — 0 erros e 3 avisos `MSB3277` existentes; `git diff --check` — aprovado com avisos de LF/CRLF.
- A build aponta a saída exclusivamente para `projetos/19-subestacao-gis/attempts/r20-16-candidate/`; a pasta ainda não existe. R20-15 também não foi executada.
- DLL final em staging: `staging-build-r20-auxprimary-19/PortfolioExpansion.dll`, 422.912 bytes; SHA-256 `8E7F228EBBAE9B0A12BF42C9669299475E89BA9B45104DF521AE5125F8A6E7A7`. Não carregada no Revit e sem saída gerada. O manifesto permanece em `bin/PortfolioExpansion.dll`, `batch-range.txt=DISABLED`; R20-11 a R20-14 foram preservadas.

## Preparação R20-17 — CT em série no acoplador — 08/11/2026

- E-01 agora representa CT-BC01* em série entre 52-BC e 89B, com ligações gráficas contínuas até a Barra B. O símbolo não afirma conexão MEP nem implementação de proteção. Relação, núcleos, fases e posição permanecem pendentes; 87B/50BF seguem como funções em estudo.
- O trecho do acoplador foi reespaçado para acomodar o símbolo e manter visíveis as seccionadoras seletoras de barra; as premissas da folha e as notas do unifilar foram alinhadas. As posições de manobra continuam marcadas como hipóteses.
- Build: `staging-build-r20-unifilar-ct-20/PortfolioExpansion.dll`, 422.912 bytes, SHA-256 `B45762C243924DE041EA1037568FFB485BD0EB52744A0A825A288CE59977723E`; 0 erros e 3 avisos `MSB3277` de referências Microsoft.VisualBasic.
- Verificações locais: 138 testes aprovados; `compileall -q scripts/engineering tests` e `git diff --check` aprovados. A compilação não inspeciona o resultado gráfico no Revit.
- A DLL ainda não foi carregada. O destino está isolado em `projetos/19-subestacao-gis/attempts/r20-17-candidate/`, que ainda não existe; R20-14 e as demais revisões são preservadas. O manifesto local segue na DLL original e `batch-range.txt=DISABLED`.

## Preparação R20-18 — foco das vistas E-03 e E-04 — 08/11/2026

- A revisão visual das páginas 5 e 6 do PDF R20-14 mostrou que o crop do transformador em E-03 começa em x=30,5 m, embora radiadores e ventiladores do TR-01 se estendam para oeste desde aproximadamente x=28,4 m; a vista principal recorta o conjunto. Em E-04, a planta principal mistura rotas geométricas com a fileira de cubículos e dificulta a leitura do equipamento.
- O gerador agora usa vistas 3D ortogonais dedicadas no campo principal de E-03 e E-04. E-03 foca o TR-01 e acessórios externos estimados; E-04 foca os sete envelopes de cubículo e a barra superior. Os detalhes funcionais 2D e notas de referência existentes são mantidos.
- Isso corrige a seleção das vistas na fonte, mas ainda não comprova composição no PDF exportado. A validação visual depende da execução no Revit.
- Build isolada: `staging-build-r20-sheetfocus-21/PortfolioExpansion.dll`, 423.424 bytes; SHA-256 `0EEE8523B9F50BDA305485080719383DE34D33F48207289F1532CA17AFDA7E9F`. Compilação com 0 erros e 3 avisos `MSB3277` relacionados às referências Microsoft.VisualBasic.
- Verificações: 138 testes aprovados; `compileall -q scripts/engineering tests` e `git diff --check` aprovados. Ainda não carregada e sem saída RVT/PDF; a pasta `attempts/r20-18-candidate/` não existe. A pergunta anterior de autorização R20-17 foi supersedida por esta hash. O manifesto permanece na DLL original e `batch-range.txt=DISABLED`.

## Preparação R20-19 — vistas de foco sem rotas geométricas — 08/11/2026

- A fonte R20-18 criava vistas 3D próprias para E-03/E-04, mas elas ainda exibiriam as DirectShapes das rotas e poderiam manter a interferência visual que motivou o ajuste. A função `HideGISRouteGeometry` filtra pelo `ApplicationId` exato `PortfolioBIM.GIS.R20.GeometryOnly` e oculta essas geometrias somente nas vistas `transformerSheet3D` e `mvSheet3D` usadas nas folhas E-03/E-04. As vistas BIM-03/BIM-04 e E-05 preservam as rotas.
- A ocultação altera apenas a representação gráfica por vista. As rotas permanecem DirectShape representativos, sem conectores elétricos, condutores ou circuitos primários; não implica conexão elétrica.
- Build isolada: `staging-build-r20-sheetfocus-22/PortfolioExpansion.dll`, 423.936 bytes; SHA-256 `9703976B4DD6FAE5FF7B261FE8ED2CBACAA98A0877E7CAF49515D7F9A1F95F9C`. Compilação: 0 erros e 3 avisos `MSB3277` de conflito de referências Microsoft.VisualBasic.
- Verificações locais: 138 testes aprovados; `python -m compileall -q scripts/engineering tests` aprovado; `git diff --check` aprovado com avisos de conversão LF/CRLF preexistentes.
- A build ainda não foi carregada no Revit e as folhas não foram exportadas ou inspecionadas; a execução visual continua pendente. O destino configurado é exclusivamente `attempts/r20-19-candidate/`, ainda inexistente. A autorização de R20-18 não cobre esta hash e fica supersedida; manifesto original e `batch-range.txt=DISABLED` permanecem intactos.

## Preparação R20-20 — invólucro externo GIS — 08/11/2026

- `GIS_BayFrame` passa de postes/travessas para uma casca externa fechada dentro do envelope publicado de 800 x 3.700 x 2.500 mm da variante totalmente digital SF6 F35-145. Faces de acesso/controle, acionamento e a divisão dos painéis são aproximações visuais sem dimensões ou posição comercial confirmadas. Nenhum componente interno OEM, terminal primário ou conexão elétrica foi acrescentado.
- A brochura GE Vernova identifica os componentes externos e a variante de dupla barra, mas não fornece o GA de pedido nem cotas/posições de cada módulo. A família mantém esse limite explícito. Os barramentos e rotas geométricas existentes continuam como invólucros representativos, sem condutor ou circuito primário.
- A saída da próxima geração está isolada em `attempts/r20-20-candidate/`; essa pasta ainda não existe. A correção altera a geometria de família no gerador, mas ainda não foi carregada nem conferida no Revit 2027. PDF, RVT, vistas e quantitativos R20-20 não foram gerados.
- Build final em `staging-build-r20-gis-shell-25/PortfolioExpansion.dll`: 426.496 bytes, SHA-256 `42F168A8336738930A0DC381D0F6A5C4EC859D56755738D4086161EEEFF3460C`; 0 erros e 3 avisos `MSB3277` recorrentes de Microsoft.VisualBasic.
- Verificação local: 138 testes aprovados, `compileall -q scripts/engineering tests` aprovado e `git diff --check` aprovado com avisos de conversão LF/CRLF em arquivos modificados previamente. O SDK 10.0.403 foi obtido do pacote oficial portátil, validado por SHA-512 e extraído em `C:\Users\dakra\.codex\tmp\revit-portfolio-dotnet-sdk-10.0.403`; o SDK global, manifesto local e DLL `bin` não foram alterados.
- A aprovação anterior de R20-13 se refere a outra hash e não cobre a DLL R20-20. A execução depende de autorização específica para a hash acima; somente o Projeto 19 deve rodar. `batch-range.txt` permanece `DISABLED` e nenhuma sessão Revit foi modificada nesta preparação.

## Retificação F35-145 e candidata R20-21 — 08/11/2026

- Uma errata anterior interpretou incorretamente a brochura. A página impressa 6, tabela **Bay Dimensions**, lista `Width x Depth x Height (mm) 800 x 3,700 x 2,500`; a referência R21 e a fonte do gerador já foram atualizadas para refletir esse envelope publicado de baia.
- A dimensão é referência de produto, não GA do pedido específico. Variante comprada, composição e posição dos módulos, terminais, interfaces e folgas de instalação/manutenção do estudo permanecem pendentes. PDF/RVT/CSV históricos não demonstram uma configuração selecionada.
- A build anterior R20-21 `staging-build-r21-catalog-correction-21/PortfolioExpansion.dll` (426.496 bytes; SHA-256 `690E867BB5E05B79DE2D0D91D421526DA6F6942823E5AED50225321544C2FBAA`) continha a classificação dimensional errada. A build 22 `staging-build-r20-f35-envelope-22/PortfolioExpansion.dll` (426.496 bytes; SHA-256 `974A65F1A3136BAE88C7BC15D1881509528226DA91318BA003694BD17FA7168F`) corrigiu a matriz, mas deixou textos gerados desatualizados dizendo que profundidade/altura eram estimativas. As duas builds estão supersedidas e não devem ser carregadas.
- A build atual está em `staging-build-r20-f35-envelope-23/PortfolioExpansion.dll` (426.496 bytes; SHA-256 `F435D2BE5B1386AFF3BF2DB24D547F6B033A1DF93BF28B024AF0042A9E0A20B4`), compilada com 0 erros e 3 avisos `MSB3277` de conflito entre versões `Microsoft.VisualBasic`. A suíte completa aprovou 142 testes; `python -m compileall -q scripts/engineering tests` e `git diff --check` passaram, este último com avisos LF/CRLF.
- O destino segue isolado em `attempts/r20-21-candidate/` e as famílias em `familias/reconstrucao-r21-corrected-f35-reference/`. A build 23 ainda não foi carregada; não existe saída RVT/PDF R20-21. A nova hash requer autorização específica.
- O manifesto local mantém a DLL canônica `bin/PortfolioExpansion.dll` (hash `BB3B5D0BE2E14829DADD5945AA1041E72F0311AD6F6858D2FB31A5816C177E4C`) e `batch-range.txt` permanece `DISABLED`.
