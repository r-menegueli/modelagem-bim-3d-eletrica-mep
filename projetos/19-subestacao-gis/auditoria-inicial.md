# Auditoria inicial — Projeto 19: Subestação GIS

Data: 2026-11-07
Revisão: 0
Estado: fotografia histórica feita antes da reconstrução R8. Os números e estados desta auditoria descrevem o modelo original, não o pacote atual. Consulte o [progresso R8](progresso.md) e a [auditoria do RVT R8](dados/reconstrucao-r8/auditoria-modelo-r8.json) para o estado posterior.

## Base inspecionada

- Repositório: branch `desenvolvimento/expansao-portfolio` (nome atual; à época da auditoria era `codex/portfolio-expansion-2026-11-07`), commit `aa7e4945c6965ae577c200f6ebbe5efb3f9107ce`.
- RVT aberto diretamente no Revit 2027.3, com a vista `BIM-01 - Axonometria - Subestação de alta tensão GIS`.
- PDF existente: oito páginas A3 paisagem, 268.133 bytes, criado em 2026-11-07 às 03:37 (hora de Brasília).
- Quantitativo existente: 55 linhas de elementos do projeto — 23 em `Equipamento elétrico` e 32 em `Modelos genéricos` — conforme `dados/quantitativos-modelo.csv`.
- Pastas do modelo contêm `Portfolio_A3.rfa`; não há famílias de equipamento específicas no pacote inspecionado.
- Configuração do add-in: `scripts/revit-addin/batch-range.txt` contém `19`. O RVT continua aberto; não reiniciar o Revit antes de controlar esse disparo automático.
- O branch não tinha alterações rastreadas pendentes no momento da auditoria. O trabalho interrompido do projeto 20 está no stash `suspender projeto 20 ate reconstrucao GIS`.

## Evidência e avaliação

| Área | Estado encontrado | Avaliação frente ao novo escopo |
|---|---|---|
| Arranjo elétrico | Entradas JSON declaram 138/13,8 kV, 32 MW, FP 0,95, 18 MW críticos e 2 × 25 MVA. O modelo coloca cinco bays GIS, mas o esquema apresenta somente uma entrada de rede. | Reprojetar para 2 × 40 MVA, duas entradas de linha e contingências declaradas; verificar topologia e seletividade antes de publicar. |
| Modelo físico | 55 DirectShapes, dos quais 23 envelopes de equipamentos e 32 volumes genéricos. A vista aberta confirma paredes simples, blocos e poucos elementos lineares. | Não representa os conjuntos de disjuntor, seccionadoras, aterramento, TC/TP, compartimentos, terminações, radiadores, acessórios e infraestrutura exigidos. |
| Famílias/parâmetros | O pacote contém família de carimbo A3; os equipamentos são volumes criados por extrusão e marcados por texto/parâmetros básicos. | Criar famílias genéricas paramétricas por sistema, com parâmetros identificadores e valores rastreáveis; sem declarar fabricante ou especificação sem fonte. |
| Cálculos | O script atual calcula carga aparente/reativa, correntes, carregamento, uma verificação N-1, compensação de FP e uma estimativa de curto limitada à impedância do transformador. | Não cobre curto da rede, alimentadores/cabos, proteção, serviços CA/CC, UPS/baterias, aterramento ou malha. Recalcular após congelar a base do estudo. |
| Documentação | O PDF tem oito folhas. A inspeção visual das prévias A-01 e E-03 mostra muito espaço vazio; E-03 omite a segunda entrada, dispositivos de manobra e proteção por bay. | Substituir por folhas distintas, coordenadas com o modelo, memorial e tabela de resultados. Alvo atualizado: 15 folhas A3 e 8 vistas 3D, definidas em `dados/matriz-de-pranchas.md` e `dados/especificacao-de-complexidade-dos-desenhos.md`. |
| Validação anterior | O README dizia que o projeto estava validado com oito folhas e cinco testes. | Essa declaração não comprova o escopo novo. Os cinco testes não foram reexecutados nesta auditoria, portanto seu estado atual é não verificado. |
| Publicação | A versão inicial está no branch público e existe publicação LinkedIn anterior do piloto. | Não alterar a publicação anterior nem criar outra. Não enviar novos commits até a validação técnica do piloto. |

## Conclusão

A versão existente é um estudo visual preliminar, não o modelo GIS detalhado pedido agora. Ela não atende aos critérios de arranjo, famílias, cálculos, infraestrutura e documentação. O projeto fica classificado como **em reconstrução**. A versão atual será preservada no histórico Git; novos arquivos serão produzidos sobre ela sem sobrescrever a evidência original.

## Sequência de controle

1. Auditoria inicial — concluída.
2. Base de projeto e matriz de folhas — em elaboração; nenhum novo desenho é emitido nesta etapa.
3. Modelo físico e famílias paramétricas — pendente da base aprovada internamente e fontes de geometria.
4. Cálculos rastreáveis — pendente.
5. Pranchas e memoriais — pendente.
6. Auditoria cruzada RVT, quantitativos, cálculos e PDFs — pendente.
7. Publicação de atualização no GitHub — pendente de aprovação nos critérios técnicos; nenhuma publicação nova no LinkedIn sem aprovação do usuário.
