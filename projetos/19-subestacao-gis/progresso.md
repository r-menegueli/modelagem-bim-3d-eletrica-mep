# Progresso — Projeto 19: Subestação GIS

Atualizado em 2026-11-07, revisão R8.

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
