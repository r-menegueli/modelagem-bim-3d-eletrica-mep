# Revisão crítica do Projeto 19 — R8

Data: 2026-11-07. Esta é uma revisão simulada pelas três perspectivas solicitadas; não substitui revisão assinada por engenheiro, coordenação BIM externa ou validação de concessionária/fabricante.

## Engenheiro eletricista

A arquitetura de estudo mostra duas entradas em 138 kV, dupla barra com acoplador, dois transformadores candidatos de 40 MVA e duas seções de 13,8 kV com tie normalmente aberto. As hipóteses de 56 MW normal e 32 MW críticos estão publicadas. O cálculo N-1 indica alívio de 24 MW para manter apenas a carga crítica com um transformador. O curto disponível da rede, impedâncias verificadas, capacidade interruptiva e ajustes seletivos não existem nos dados de entrada; não há base para aprovar operação nem proteção.

## Coordenador BIM

A versão R8 foi gerada no Autodesk Revit 2027.3. A auditoria registra 191 instâncias FamilyInstance, 30 arquivos RFA, 21 paredes e 7 pisos nativos, zero DirectShapes e metadados de identidade presentes nas instâncias examinadas. Há 93 portas de conectores MEP, mas nenhuma está fisicamente conectada. O único sistema elétrico é o circuito lógico auxiliar QAC-01–UPS-01, sem cabo físico. Canaletas e leitos são famílias com posição conceitual; não são rotas de cabos verificadas. Os elementos têm geometria estimada e não demonstram coordenação LOD 300.

## Recrutador técnico

O projeto apresenta evidência verificável de uso de Revit, famílias, categorias, API, tabelas, cálculos Python e documentação. É apropriado apresentá-lo como estudo acadêmico BIM em desenvolvimento, com hipóteses e limitações visíveis. Não deve ser descrito como subestação executiva, comissionada, certificada ou inteiramente conectada. A vista geral ainda oculta partes internas sob os edifícios e várias folhas têm áreas livres grandes; a extensão técnica ainda precisa aparecer com mais clareza na documentação.

## Cinco principais problemas e resolução

| # | Problema | Evidência R8 | Tentativa / estado |
|---:|---|---|---|
| 1 | Falta de famílias BIM identificáveis no modelo original | O modelo inicial tinha 55 DirectShapes sem famílias funcionais. | Reconstruído parcialmente: 30 arquivos RFA e 191 FamilyInstances no R8; zero DirectShapes e metadados de identidade presentes. Os 145 Generic Models e a geometria estimada ainda limitam a classificação/LOD de parte do modelo. |
| 2 | Não há circuito físico ou cabo MEP validado | A auditoria conta 93 conectores, zero conectores fisicamente ligados e um sistema com um terminal. | Documentado e corrigido na comunicação: há somente o sistema lógico QAC-01–UPS-01; não foi criada conexão física. Continua bloqueador para validação dos circuitos principais. |
| 3 | Operação N-1 e cálculos de sistema incompletos | A hipótese usa dois transformadores de 40 MVA, 56 MW normais e 32 MW críticos; falta dado da rede e de fabricante. | Cálculos trifásicos e N-1 reproduzíveis foram implementados. O estudo exige alívio de 24 MW em N-1. Curto da rede, seletividade e ratings verificados continuam sem dados. |
| 4 | Rotas de cabos e aterramento são apenas conceituais | Há famílias de canaleta/leito e 180 ModelCurves de malha; não há cabos MEP conectados nem resistividade/corrente/tempo de falta. | Estado e notas atualizados nas pranchas. Não foi apresentado dimensionamento de cabo, toque/passo ou aterramento como se estivesse concluído. A solução elétrica permanece pendente. |
| 5 | Densidade visual e documentação ainda não alcançam o padrão pretendido | O PDF tem 21 páginas A3 e 8 vistas 3D; as folhas gerais têm áreas livres e a cobertura encobre áreas internas na vista geral. | A Q-01 foi espaçada para remover a interseção da última linha; textos de MEP foram corrigidos. Todas as páginas foram renderizadas e contact sheets revisadas. A apresentação geral ainda exige mais detalhe funcional e melhor exposição interna. |

## Resultado da revisão

A revisão R8 melhora substancialmente a estrutura BIM em relação aos DirectShapes do arquivo inicial e entrega cálculos, auditoria rastreável, 21 pranchas e 21 PNGs. O critério de aceitação definitiva não foi alcançado: faltam dados externos para fechar cálculos e geometria, e faltam ligações físicas MEP. Os cinco bloqueadores estão registrados para orientar a continuação. Nenhum dos outros projetos foi modificado nesta revisão.