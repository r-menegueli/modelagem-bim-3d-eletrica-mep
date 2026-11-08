# Checkpoint comparativo GIS R8–R13

**Estado: R13 preparada no código, ainda não executada no Revit.** Este arquivo é um checkpoint intermediário; não aprova nem conclui a R13.

## Evidência disponível

| Métrica | R8 | R12 auditada no Revit | R13 neste checkpoint |
|---|---:|---:|---|
| FamilyInstances | 191 | 170 | Não gerada |
| Electrical Equipment | 46 | 46 | Não gerada |
| Generic Model families | 145 | 124 | Não gerada |
| Conectores MEP | 93 | 14 | Não auditada |
| Conectores `IsConnected=true` | Não consta no mesmo esquema | 2 | Não auditada |
| `ElectricalSystem` | 1 | 1 (`AUX-UPS-01`) | Não auditada |
| Condutores primários físicos confirmados | Não consta no mesmo esquema | 0 | Não auditada |
| CableTray / conectores conectados | Não consta no mesmo esquema | 8 trechos / 8 de 16 | Não auditada |
| Wire 2D | Não consta no mesmo esquema | 1, 2 de 2 extremidades | Não auditada |
| Folhas / vistas 3D | 21 / 8 | 14 / 4 | Não gerada |
| Avisos do documento | 0 | 0 | Não auditada |

Campos ausentes nos relatórios R8/R9 não são tratados como zero. `IsConnected` em leitos comprova somente juntas de infraestrutura. O `ElectricalSystem` auxiliar e o Wire view-specific não comprovam cabos de potência.

## Revisões técnicas já confirmadas

- R12 foi aberta no Revit 2027.3 e a auditoria foi extraída do documento aberto.
- O grafo independente aplicado aos CSVs reais da R12 espera 73 nós e 69 relações: 14 relações geométricas, 43 documentadas somente no diagrama e 12 sistemas nativos não confirmados.
- Cinco placeholders de TC estão presentes na R12, inclusive `GIS-BC01-CT`; isso não especifica relação, núcleos, fases ou zona de proteção.
- As 11 tentativas de circuitos auxiliares por painel foram revertidas. O Revit não confirmou carga útil nem `LoadName`; não são contadas como circuitos válidos.
- Os estudos BP-01 a BP-15 e as sensibilidades existentes são mantidos. Este checkpoint não acrescenta resultados numéricos.

## Alterações R13 preparadas, sem efeito demonstrado no RVT

- A rotina de composição de viewport agora tenta ampliar vistas subdimensionadas e mantém um limite contra extrapolar a área útil.
- O E-01 imprime o identificador placeholder de TC junto aos símbolos de cada bay e identifica o placeholder do acoplador.
- Os textos herdados do gerador GIS foram alinhados para 2 × 40 MVA, Z=10% provisória, implantação de estudo 72 × 56 m e saídas R13.
- O relatório de auxiliares aceita `--revision R13` e valida o nome da revisão antes de compor o caminho de saída.
- Testes: `python -m unittest discover -s tests -v` — 53 testes aprovados.
- Compilação de staging com o SDK .NET 10.0.402 — 0 erros; avisos de conflito de referências Autodesk/.NET. O build regular não substituiu `bin/PortfolioExpansion.dll` porque o Revit mantém a DLL carregada.
- `git diff --check` — sem erros de whitespace; Git alertou que aplicará conversão LF/CRLF nos dois arquivos Python editados.

## Condições pendentes para aceitar R13

1. Fechar o documento R12 salvo, compilar a DLL de destino e executar o gerador apenas para o Projeto 19.
2. Abrir o RVT R13 no Revit 2027.3 e extrair auditoria do documento efetivamente aberto.
3. Gerar PDF no Revit, renderizar as 14 folhas e revisar E-01, BIM-02, BIM-04 e as demais páginas quanto a legibilidade, encaixe e sobreposições.
4. Gerar o grafo R13 a partir dos CSVs extraídos e verificar a correspondência das cinco marcas CT no unifilar e no modelo.
5. Comparar as métricas R8/R12/R13, atualizar este documento com evidências R13 e só então publicar o checkpoint de modelo e PDF.

## Limite de aprovação

Até essas etapas serem feitas, a R13 é apenas uma alteração de código compilada em diretório temporário. Não há RVT, PDF, imagem, família ou auditoria R13 validada neste checkpoint.
