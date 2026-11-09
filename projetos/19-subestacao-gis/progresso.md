# Progresso — Projeto 19: Subestação GIS

## Estado atual — 09/11/2026

- **R20-23 é a revisão mais recente gerada e extraída do documento aberto no Revit 2027.3.** O [RVT](attempts/r20-23-candidate/revit/reconstrucao-r20/19-subestacao-gis-r20.rvt), o [PDF A3](attempts/r20-23-candidate/pranchas/reconstrucao-r20/19-subestacao-gis-r20-23-A3.pdf), os [CSV de conectividade](attempts/r20-23-candidate/dados/reconstrucao-r20/conectividade-equipamentos-r20.csv) e [relações de rede](attempts/r20-23-candidate/dados/reconstrucao-r20/relacoes-rede-r20.csv), além da [comparação R8 × R20-23](attempts/r20-23-candidate/dados/reconstrucao-r20/comparativo-r8-r20-23.md), estão preservados na candidata isolada.
- A extração registrou 168 instâncias, 46 equipamentos, 122 modelos genéricos, 24 DirectShapes, 8 CableTrays com 8/16 conectores em juntas, 25 conectores MEP com 2 ligados no auxiliar QAC-01–UPS-01, um Wire 2D, 14 folhas, quatro vistas 3D e zero avisos.
- Os 12 trajetos por fase são geometria até envelopes de família, não portas nem condutores. A rede primária HV/MV continua sem circuitos/cabos Revit conectados. As 11 tentativas de circuitos auxiliares sem confirmação de dados foram revertidas.
- As 14 folhas foram renderizadas e inspecionadas no PDF A3. A exportação PDF nativa não ficou disponível nesta instalação; o relatório registra a saída pelo Microsoft Print to PDF e a normalização uniforme do formato. O canvas Revit permaneceu em branco nesta sessão, embora a extração API e as vistas impressas tenham sido obtidas.
- Os 26 testes de contrato do gerador R20-23 passaram. O estado da DLL local, do manifesto e de `batch-range.txt` foi restaurado. A revisão fica documentada no branch de trabalho; não houve LinkedIn nem execução de outros empreendimentos.
- R20-23 é validada como estudo BIM demonstrativo; não é projeto executivo, verificação de conformidade ou dimensionamento aprovado.

## Registro anterior — 08/11/2026 (R20-14 e preparação R20-21; superado pela R20-23)

- **R20-14 é a última candidata realmente executada e extraída do Revit 2027.3.** Há RVT e PDF A3 de 14 folhas; a auditoria registra 168 instâncias, 25 conectores MEP, um sistema auxiliar lógico, 72 DirectShapes de rota e nenhum condutor primário de potência confirmado. Os detalhes estão no [registro R20-14](attempts/r20-14-candidate/README-R20.md).
- A inspeção do PDF R20-14 ainda aponta problemas de composição em E-03/E-04. A topologia e as rotas HV/MV não equivalem a conexão elétrica nativa ou cabo instalado.
- A especificação do grafo complementar foi alinhada ao E-01 atual: CT-BC01* está entre 52-BC e 89B; o bypass direto foi removido. O teste confirma essa sequência como relação documentada, sem promovê-la a circuito Revit. O grafo da extração R20-14 permanece histórico até haver execução nova.
- A página impressa 6 da brochura GE Vernova F35-145 lista o envelope de baia 800 × 3.700 × 2.500 mm. A antiga errata que negava profundidade/altura foi retificada; configuração comprada, GA de pedido, posições dos módulos, terminais e folgas do projeto permanecem pendentes. As builds `690E867B…44C2FBAA` e `974A65F1…FA7168F` estão supersedidas. A build atual está em `scripts/revit-addin/staging-build-r20-f35-envelope-23/PortfolioExpansion.dll` (426.496 bytes; SHA-256 `F435D2BE5B1386AFF3BF2DB24D547F6B033A1DF93BF28B024AF0042A9E0A20B4`), ainda não autorizada/carregada. Ainda não existe RVT/PDF R20-21. Consulte a [referência R21](dados/reconstrucao-r21/referencias-equipamentos-r21.json) e a [retificação dimensional](dados/reconstrucao-r20/errata-referencia-f35.md).
- A suíte local tem 142 testes aprovados; `compileall` e `git diff --check` passaram, com avisos LF/CRLF. Isso valida verificações de código, não abre nem valida o modelo Revit. Nenhum post em rede social foi feito.

## Snapshot histórico — 07/11/2026 (R8)

| Fase | Estado | Evidência |
|---|---|---|
| 1. Auditoria do projeto existente | Concluída | [Auditoria inicial](auditoria-inicial.md); RVT inicial e oito folhas A3 examinados; 55 DirectShapes identificados no modelo antigo. |
| 2. Arranjo e premissas | Parcial | [Base de projeto](dados/base-de-projeto.md), premissas explícitas, [matriz de pranchas](dados/matriz-de-pranchas.md) e [especificação por desenho](dados/especificacao-de-complexidade-dos-desenhos.md). Topologia e ratings permanecem como hipótese. |
| 3. Modelo Revit e famílias | Parcial, gerado no Revit | R8: RVT salvo no Revit 2027.3; 30 arquivos RFA, 191 FamilyInstances, 46 Electrical Equipment, 145 Generic Models, 21 paredes, 7 pisos, zero DirectShapes, 93 portas MEP e uma conexão lógica auxiliar. Zero conectores fisicamente ligados. |
| 4. Cálculos | Parcial, reproduzível | Memoriais e JSON/CSV para cálculos implementados. Dez testes automatizados passaram. Curto da rede, coordenação de proteção, cabos e aterramento não foram dimensionados por falta de dados. |
| 5. Documentação | Parcial, exportada | PDF com 21 páginas A3, 8 vistas 3D e 21 imagens PNG a 120 dpi. Contact sheets inspecionadas; Q-01 corrigida. As imagens gerais ainda exibem espaços livres e ocultação de áreas internas. |
| 6. Auditoria cruzada | Parcial, bloqueadores documentados | RVT gerado a partir do documento aberto no Revit; auditoria CSV/JSON rastreia elementos e parâmetros. A revisão crítica identifica cinco problemas e as verificações ainda necessárias. LOD 300 não foi atingido. |
| 7. GitHub e LinkedIn | Branch de revisão publicada | A branch codex/portfolio-expansion-2026-11-07 contém a reconstrução parcial R8; não foi mergeada em main. O rascunho de LinkedIn está preparado e não foi publicado. |

## Bloqueadores técnicos

1. Obter níveis de curto da rede e dados de concessionária para validar a topologia e proteção.
2. Obter desenhos e dados de placa de transformadores GIS, cubículos, TCs/TPs e equipamentos auxiliares.
3. Definir lista de cabos e critérios de instalação para modelar rotas MEP e conexões físicas.
4. Obter resistividade do solo, corrente e tempo de eliminação para avaliar aterramento.
5. Confirmar cargas auxiliares, missão da bateria/UPS e requisitos civis, incêndio, manutenção e segurança.

## Próximas ações condicionadas a dados

- Completar conexões e rotas MEP nativas no Revit e auditar a ligação dos conectores.
- Atualizar cálculos, diagramas e tabelas com dados aprovados e rastreáveis.
- Substituir geometrias estimadas por referências dimensionais verificadas quando disponíveis.
- Rever as folhas gerais para expor os ambientes internos e reduzir os espaços livres sem função.
- Reexecutar a auditoria e a inspeção visual depois de cada alteração relevante.

Os outros projetos continuam fora do escopo desta revisão. O Projeto 19 permanece estudo parcial, sem uso executivo.
