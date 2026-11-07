# Matriz de pranchas — Subestação GIS 138/13,8 kV

Revisão 2 — 2026-11-07. Esta matriz registra a base de **15 pranchas A3** e **8 vistas 3D nomeadas**. A reconstrução R8 gerou um PDF A3 de 21 páginas: 13 folhas de implantação, arquitetura, diagramas, quantitativos e cálculos, além de oito folhas BIM com vistas 3D. Os códigos das 15 pranchas-base estão presentes; a presença no PDF e de PNG correspondente comprova a exportação, mas não o atendimento aos critérios técnicos abaixo. Requisitos detalhados estão em [Especificação de complexidade dos desenhos](especificacao-de-complexidade-dos-desenhos.md); o estado efetivamente verificado da R8 está no registro ao final deste arquivo.

## Regras comuns de desenvolvimento

- Base do estudo a verificar antes de modelar: duas entradas de linha 138 kV, dois transformadores 40 MVA, sistema GIS, barramentos de 13,8 kV seccionados, casa de controle e serviços auxiliares. Valores e topologia ficam como premissas até a conferência de dados reais.
- A modelagem deve separar componentes que tenham função ou identificação próprias. Um único bloco por bay ou transformador não satisfaz o alvo.
- Cada equipamento terá tag, sistema/tensão, bay ou alimentador, estado de desenvolvimento e origem dos dados. Dimensões de catálogo só serão usadas quando confirmadas em fonte pública oficial; o restante será identificado como estimativa geométrica.
- Famílias serão genéricas e paramétricas. Não atribuir fabricante, curva, classe, capacidade ou certificação sem fonte. Conectores MEP só serão incluídos quando sua orientação, classificação e parâmetros puderem ser coerentemente testados.
- Elevações e cortes deverão corresponder às vistas do modelo. Diagramas terão circuitos, contatos e funções desenhados explicitamente e serão cruzados com tags, cargas e cálculos.
- Todas as folhas deverão ter escala ou declarar “sem escala”, legenda, notas, fontes de dados, revisão e limite de uso. Evitar reutilizar o mesmo esquema como se fossem plantas independentes.

## Complexidade prevista por folha

| Folha | Conteúdo e representação | Complexidade mínima esperada | Verificação de consistência |
|---|---|---|---|
| A-01 | Implantação geral | Limites do terreno de estudo, norte, acessos, edifícios, pátio, vias internas, áreas de transformadores, drenagem conceitual, canaletas principais, cotas de locação e zonas de acesso/manutenção. | Conferir dimensões e posições contra a planta do modelo e área declarada. |
| A-02 | Arquitetura e setorização | Planta cotada de salas GIS, cubículos, controle, baterias e serviços; acessos, portas, rotas de fuga conceituais e separação funcional; cortes e fachadas essenciais. | Ambientes e elementos visíveis devem existir no modelo; limitações normativas e de incêndio explicitadas. |
| E-01 | Unifilar geral 138/13,8 kV | Duas entradas, bays de linha, topologia de barras 138 kV, acoplamento, dois bays de transformador, TCs/TPs, seccionamento/aterramento, níveis de tensão, tags e condições normal/contingência. | Topologia coincide com base de projeto e com o modelo; estados de manobra documentados. |
| E-02 | Arranjo GIS classe 145 kV (sistema nominal 138 kV) | Planta, elevação e corte da GIS; compartimentos por bay, disjuntores, seccionadoras, chaves de aterramento, TCs/TPs, barras, terminais e suportes identificados. | Contagem de componentes e tags casa com famílias e unifilar; dimensões têm fonte ou marca de estimativa. |
| E-03 | Transformadores de potência | Plantas e elevações separadas dos dois 40 MVA com tanque, radiadores, ventiladores, conservador/acessórios aplicáveis, buchas, dispositivos de proteção, base, acesso e contenção de óleo conceitual. | Geometria separada por conjunto; dados de placa não inventados; áreas de acesso e contenção marcadas como conceituais. |
| E-04 | Distribuição 13,8 kV | Unifilar e arranjo de cubículos: entradas de transformador, seções A/B, acoplamento normalmente aberto (se confirmado), alimentadores, medição, proteção e cargas prioritárias. | Número de cubículos, barramentos e cargas bate com tabelas e estudo de contingência/rejeição. |
| E-05 | Proteção, controle, medição e SCADA | Arquitetura por bay, IEDs, TCs/TPs, circuitos de trip/close, intertravamentos, sincronismo, falha de disjuntor, proteção de barra/transformador/linha e comunicação. | Cada função e sinal tem origem/destino, equipamento e premissa; ajustes só se houver dados suficientes. |
| E-06 | Infraestrutura de cabos | Rotas de força, controle, proteção e comunicação separadas; canaletas/eletrocalhas, travessias, entradas de sala, raios/ocupação conceituais e tabela cabo-origem-destino. | Comprimentos, seção e ocupação referenciam modelo e cálculos; conflitos e separações pendentes ficam identificados. |
| E-07 | Serviços auxiliares CA/CC | Unifilar CA, quadros, fontes, retificadores/carregadores, banco de baterias, UPS, cargas por regime e diagramas de distribuição. | Cargas instaladas, demanda, autonomia e queda de tensão são rastreáveis; autonomia depende de missão definida. |
| E-08 | Aterramento e proteção contra descargas | Planta conceitual de malha, condutores/eletrodos, ligações de estruturas/equipamentos e captores/descidas quando aplicáveis. | Premissas de resistividade e corrente de falta não são inventadas; toque/passo e conformidade ficam pendentes sem dados de solo/falta. |
| C-01 | Civil, bases e drenagem | Bases de equipamentos, poços/canaletas, travessias, contenção de óleo, pisos, acesso de manutenção e drenagem conceitual em planta/corte. | Geometria civil alinha com equipamentos; cargas de fundação, geotecnia e dimensionamento estrutural continuam fora do estudo até haver dados. |
| BIM-01 | Axonometria geral | Modelo completo com zonas, edifícios e sistemas; vista sombreada, escala visual controlada, tags principais, legenda de sistemas e indicação de elementos estimados. | Captura diretamente do RVT; conferir categorias, visibilidade, posição e quantitativos. |
| Q-01 | Tabelas de equipamentos e quantitativos | Schedules extraídos do RVT para GIS por bay, transformadores/acessórios, cubículos por seção, painéis, auxiliares, rotas e elementos civis; subtotais por sistema e estado dos dados. | Quantidades recalculadas a partir dos elementos; comparar com E-01 a E-08. |
| BIM-02 | Cortes 3D e detalhe construtivo | Vista explodida/seccionada do GIS e de um transformador, com componentes separados, suportes, cabos, canaletas, acessos e relação entre sistemas. | Elementos têm parâmetros e tags; conferir ausência de sobreposição grosseira e integridade geométrica no Revit. |
| M-01 | Memorial e quadro consolidado de resultados | Tabela rastreável de cargas, correntes, carregamento, contingência, curtos, alimentadores, funções de proteção e serviços auxiliares, com IDs de cálculo e fontes. | Cada número aponta para entrada, método, unidade, premissa/fonte e estado de verificação; resultados coincidem com scripts e folhas. |

## Portão antes da emissão

Uma folha só passa a `EMITIDA` quando existir no RVT, aparecer corretamente no PDF A3, tiver PNG correspondente e passar conferência de conteúdo, escala, legibilidade e referências cruzadas. A R8 tem 21 páginas e PNGs correspondentes, mas a inspeção visual encontrou conteúdo ainda parcial em relação aos requisitos desta matriz. As folhas permanecem `PARCIAIS`; quantidade de páginas não certifica projeto executivo nem substitui dados de concessionária, fabricante, solo ou responsável técnico.

## Estado comprovável da revisão R8

| Página | Conteúdo exportado | Estado frente ao critério | Pendência principal registrada |
|---:|---|---|---|
| 01 — A-01 | Implantação geral | Parcial | Faltam cotas e verificações completas de acessos, afastamentos, drenagem e interfaces. |
| 02 — A-02 | Arquitetura e setorização | Parcial | Planta esquemática; cortes, fachadas, cotagem e verificações de incêndio/acessibilidade não concluídos. |
| 03 — E-01 | Unifilar geral | Parcial | Topologia é hipótese; faltam dados de concessionária, estudos de falta e detalhamento validado das proteções. |
| 04 — E-02 | Arranjo GIS | Parcial | Planta de estudo presente; elevações/cortes dimensionados e dados confirmados de fabricante não disponíveis. |
| 05 — E-03 | Transformadores e acessórios | Parcial | Geometria estimada; placa, contorno, perdas, resfriamento e contenção não confirmados. |
| 06 — E-04 | Cubículos de 13,8 kV | Parcial | Planta presente; ratings, curto-circuito, diagrama completo de alimentadores e coordenação pendentes. |
| 07 — E-05 | Proteção e controle | Parcial | Arquitetura funcional conceitual; lógica por sinal, ajustes, arquitetura de rede e testes não implementados. |
| 08 — E-06 | Caminhos de cabos | Parcial | Canaletas/leitos são conceituais; não há cabos físicos, lista, dimensionamento ou conexões MEP verificadas. |
| 09 — E-07 | Serviços auxiliares CA/CC | Parcial | Diagrama conceitual; apenas QAC-01–UPS-01 como sistema lógico, sem conexões físicas ou autonomia calculada. |
| 10 — E-08 | Aterramento conceitual | Parcial | Malha modelada como linhas; faltam dados de solo/falta e verificações de toque e passo. |
| 11 — C-01 | Bases e infraestrutura civil | Parcial | Representação de estudo; geotecnia, dimensionamento estrutural, drenagem e interfaces não verificados. |
| 12 — Q-01 | Equipamentos e quantitativos | Parcial | Tabela extraída do RVT; escopo e propriedades ainda não cobrem todos os campos e sistemas previstos. |
| 13 — BIM-01 | Vista geral isométrica | Parcial | Vista real do RVT; coberturas ocultam interiores e a leitura geral ainda carece de densidade funcional. |
| 14 — BIM-02 | Interior do GIS | Parcial | Componentes estão visíveis; não há validação dimensional por fabricante nem coordenação completa de interfaces. |
| 15 — BIM-03 | Transformadores e acessórios | Parcial | Famílias separadas; geometria, conexão e acessórios continuam estimados. |
| 16 — BIM-04 | Cubículos de 13,8 kV | Parcial | Vista 3D real; ratings e conexões físicas continuam pendentes. |
| 17 — BIM-05 | Proteção e controle | Parcial | Vista 3D real; painéis e sistemas são conceituais, sem configuração funcional validada. |
| 18 — BIM-06 | Vista explodida controlada | Parcial | Vista exportada; relações, deslocamentos e referências entre componentes exigem conferência BIM adicional. |
| 19 — BIM-07 | Corte espacial GIS e rotas | Parcial | Corte exportado; rotas continuam conceituais e conexões físicas não verificadas. |
| 20 — BIM-08 | Infraestrutura e interfaces | Parcial | Vista exportada; ausência de cabo físico e de coordenação de interferências limita o aceite. |
| 21 — M-01 | Memorial e resultados | Parcial | Resultados reproduzíveis para entradas declaradas; curto da rede, cabos, proteção e aterramento permanecem pendentes. |

As 21 páginas e 21 PNGs foram inspecionados visualmente. A inspeção confirma sua presença e enquadramento geral, não comprova legibilidade integral em tamanho impresso nem atendimento aos requisitos de projeto executivo. Nenhuma folha da R8 está classificada como `EMITIDA`.
