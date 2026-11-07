# Especificação de complexidade dos desenhos — Subestação GIS

Revisão 2 — 2026-11-07. Define conteúdo e evidência mínima para as pranchas e vistas pedidas no escopo. A revisão R8 gerou 21 páginas A3 e oito vistas 3D no Revit; a presença desses arquivos não comprova por si só que os requisitos abaixo foram atendidos. O registro de produção e lacunas da R8 está no fim deste documento.

## Escopo fechado

O alvo de conteúdo é composto por **15 pranchas A3**, incluindo BIM-01 e BIM-02, e **8 vistas tridimensionais nomeadas**, associadas às pranchas. Na R8, os 13 conteúdos de implantação, arquitetura, diagramas, quantitativos e cálculos aparecem em folhas próprias, e as oito vistas 3D foram colocadas em oito folhas BIM, totalizando 21 páginas. Os oito painéis 3D não substituem desenhos técnicos, plantas, seções ou diagramas. Cada prancha usa geometria do RVT quando ela existe; diagramas de função podem ser Drafting Views do Revit, desde que identificados como esquemáticos e cruzados com o modelo.

| Grupo | Quantidade | Regra |
|---|---:|---|
| Pranchas A3 | 15 | Uma folha Revit por código; nenhuma folha duplicada para aparentar quantidade. |
| Vistas 3D requeridas | 8 | Vistas salvas no RVT, com enquadramento próprio e finalidade explícita. |
| Tabela de equipamentos e quantitativos | 1 prancha Q-01 | Extraída de elementos do RVT; tabelas auxiliares também aparecem em E-02, E-03 e E-04. |
| Memorial/tabela central de cálculos | 1 prancha M-01, mais arquivos-fonte | Cada número precisa apontar para entrada, método, unidade e estado de verificação. |

## Rastreabilidade do pedido para os desenhos

| Entrega pedida | Desenho(s) que a atende(m) |
|---|---|
| Implantação geral | A-01 e 3D-01 |
| Perspectivas e vistas 3D | BIM-01 e BIM-02; vistas 3D-01 a 3D-08 |
| Arranjo físico GIS | E-02 e 3D-02 |
| Transformadores | E-03 e 3D-03 |
| Sala de média tensão | E-04 e 3D-04 |
| Proteção e controle | E-05 e 3D-05 |
| Diagramas unifilares | E-01, E-04 e E-07 |
| Serviços auxiliares | E-07 e 3D-05 |
| Caminhos de cabos | E-06 e 3D-08 |
| Cortes e detalhes | A-02, C-01, BIM-02 e 3D-07 |
| Equipamentos e quantitativos | Q-01 |
| Resumo dos cálculos | M-01 |

## Regras de complexidade para todo o pacote

1. **Complexidade técnica, não decoração.** Cada forma representa equipamento, estrutura, acesso, interface ou rota funcional. Elemento sem função identificável ou sem consequência no arranjo não entra para preencher vazio.
2. **Um elemento, uma identidade.** Equipamentos que tenham função própria recebem elemento/família e marca próprios; não agrupar todo um bay, transformador ou painel num único bloco.
3. **Dados de placa e geometria rastreáveis.** Fabricante/modelo só quando a fonte pública oficial corresponder à configuração. Sem desenho dimensional, declarar “geometria estimada” na família e na prancha. Não converter referência de catálogo em seleção efetiva.
4. **Estado do desenvolvimento visível.** Classificar sistema e família como `REFERENCIADO`, `ESTIMADO`, `CONCEITUAL`, `CONDICIONAL` ou `PENDENTE`. LOD 300-like é meta somente para objetos com informação dimensional suficiente; não atribuir LOD 300 global ao estudo.
5. **Modelo é a fonte das plantas e vistas.** Planta, seção, quantitativo e 3D precisam ser vistas/elementos do RVT. Um Drafting View pode explicar topologia, proteção ou cálculo; deve trazer “ESQUEMÁTICO / SEM ESCALA” e usar os mesmos tags do modelo.
6. **Atributos BIM mínimos por equipamento.** `Mark/ID`, função/tipo, sistema, bay/alimentador, tensão, corrente quando definida, potência quando aplicável, localização, estado geométrico, fabricante/modelo quando selecionado, fonte e comentário de hipótese. Campo sem dado fica vazio ou `PENDENTE`, nunca inventado.
7. **Conectividade sem simulação falsa.** Só mostrar condutor como ligado quando as extremidades e o percurso corresponderem ao sistema representado. Linhas conceituais e conexões gráficas devem ser identificadas como tais. Não afirmar circuito MEP funcional sem conectores, sistema e verificação no Revit.
8. **Conteúdo de folha legível.** Cada vista deve usar escala gráfica coerente ou declarar “SEM ESCALA”. Texto, tags, cotas, legenda e notas precisam ser legíveis no PDF A3 em tamanho real; notas não podem esconder a geometria. Espaço em branco só é aceito para separar áreas e facilitar leitura.
9. **Carimbo comum.** Título, código, revisão, data, unidade, escala, autoria, status de estudo, fonte, limites de uso e chave de estimativa consistentes entre RVT, PDF e PNG.
10. **Sem selo de aprovação indevido.** Estudo conceitual não deve dizer “aprovado”, “conforme”, “executivo” ou “LOD 300” sem evidência técnica correspondente.

## Matriz de complexidade das 15 pranchas

| Código / prancha | Conteúdo obrigatório e complexidade mínima | Vistas/elementos que sustentam o desenho | Conferência cruzada e estado permitido |
|---|---|---|---|
| **A-01 — Implantação geral e arranjo físico** | Limite e dimensões do terreno de estudo; norte; portão e acesso; circulação de manutenção; edifício GIS, sala MT, casa de controle e pátio de transformação; posição e zona de manutenção de TR-01/TR-02; áreas de terminação de linha; barreira corta-fogo se adotada; vias; drenagem conceitual; canaletas principais; malha de terra; iluminação externa conceitual; cotas de locação e legenda de zonas. | Planta nativa de implantação; incluir miniatura da `3D-01` quando a folha suportar leitura. | Limite e cotas iguais às coordenadas do RVT; rotas conectam aos destinos; áreas de acesso não podem ser cobertas por equipamento. Drenagem e afastamentos ficam como conceito se não calculados. |
| **A-02 — Arquitetura e setorização** | Planta cotada do GIS, cubículos MT, comando/proteção/telecom, baterias, auxiliares e circulação; portas/acessos; paredes corta-fogo quando necessárias; áreas de operação/manutenção; identificação de ambiente; cortes e elevações arquitetônicas principais; legenda de parede/piso. | Plantas e cortes nativos Revit; paredes, pisos e aberturas existentes no modelo. | Ambientes, cotas, aberturas e cortes correspondem ao RVT. Incêndio, fuga, ventilação, acessibilidade e aprovação legal devem aparecer como pendentes enquanto não verificados. |
| **E-01 — Diagrama unifilar geral 138/13,8 kV** | Duas entradas; cinco bays de estudo (L01, TR01, acoplador, TR02, L02); barra dupla 138 kV e acoplamento; disjuntores, seccionadoras, chaves de terra, TCs/TPs e terminais; transformadores 40 MVA; barras A/B de 13,8 kV; dois incomers, medição, tie normalmente aberto e cinco alimentadores por seção; tags; regimes normal e N-1; 32 MW críticos e 24 MW a rejeitar como hipótese. | Drafting View vetorial esquemática com legendas de símbolo e tags iguais às famílias/schedule. | Contar bays, transformadores, seções e feeders contra RVT e base JSON. Mostrar de forma inequívoca o estado normal e a ação de contingência. Sem dados de rede, curto e ajustes permanecem pendentes. |
| **E-02 — Arranjo físico GIS 145 kV** | Planta, elevação longitudinal e corte transversal; compartimentos e limites de bay; disjuntores, DS, ES, TC, TP, barras encapsuladas, terminação, suportes, bases, invólucros, mecanismo e espaço de inspeção; cinco funções de bay e legenda; tabela de equipamentos GIS com marca, quantidade, categoria, tensão/classe quando comprovada e estado de geometria. | Planta/corte/elevação do RVT e `3D-02` como vista de detalhe. | Cada marca existe como família/elemento. Orientação de barras e bays coincide com E-01. Ratings, isolamento, arco interno e afastamentos não são inferidos de dimensões estimadas. |
| **E-03 — Transformadores de potência** | Planta e pelo menos duas elevações de cada unidade; tanque, tampas, radiadores, ventiladores, buchas AT/MT, terminais, conservador apenas se a construção for confirmada, acessórios locais, painel, base, contenção conceitual, barreira e corredores de manutenção; tabela de acessórios por TR. | Cortes/elevações nativos do RVT e `3D-03`. | TR-01/TR-02 têm geometria separada e marcas próprias. Potência 40 MVA e Z=10% ficam identificadas como hipótese até placa. Óleo, resfriamento, volume de contenção, perdas e distâncias não dimensionados não são apresentados como finais. |
| **E-04 — Distribuição 13,8 kV** | Planta da sala com duas seções; `A-IN`, `A-MED`, `A-F01…05`, tie e `B-F01…05`, `B-MED`, `B-IN`; barra A/B; traseira/frente de cubículos conforme arquitetura; corredores; entradas de cabos; unifilar da distribuição; circuitos prioritários e carga não crítica a rejeitar; quadro de tags e dados disponíveis. | Planta nativa, diagrama Drafting e `3D-04`. | Total de 15 cubículos (7 por seção + 1 tie) e 10 alimentadores cruzado com modelo/base. Corrente de referência de 2.000 A deve constar como candidata, não rating selecionado; Icc, arco e proteção ficam pendentes. |
| **E-05 — Proteção, controle, medição e SCADA** | Arquitetura por bay e função; papéis de IED de linha, barra e transformador; circuitos funcionais de trip/close, 86, falha de disjuntor, intertravamento, sincronismo, medição e alarmes; origem/destino de sinais; switches/fibras; HMI, gateway, SOE/tempo; tabela de funções ANSI candidatas e dependências. | Diagrama funcional; planta dos painéis reais do modelo; `3D-05`. | Cada função tem equipamento e origem/destino coerentes. IEC 61850 é somente conceito até haver arquitetura/configuração testada; não publicar settings, seletividade ou rede implementada sem evidência. |
| **E-06 — Caminhos de cabos e interfaces** | Planta por classe de rota (potência MT, auxiliares CA, proteção/controle, comunicação/fibra); canaletas, eletrocalhas, suportes, travessias e entradas; setas de origem/destino; seção transversal típica; tabela rota/circuito/origem/destino/comprimento/status; conflitos e segregação pendentes. | Planta e seção nativas; `3D-08` para interfaces e leitura espacial. | Cada rota existe no RVT e chega a ambas as extremidades. Comprimentos podem ser extraídos do modelo; seção, ampacidade, queda, ocupação e segregação permanecem pendentes até conhecer cabo, instalação, agrupamento e temperatura. |
| **E-07 — Serviços auxiliares CA/CC, UPS e baterias** | Unifilar CA e CC; fontes e transferência; QAC/QCC; carregador/retificador; UPS, bypass, bateria, distribuição por painel; cargas por regime (contínua, intermitente, emergência); tabela kW/kVA, tensão, fator de demanda, autonomia e estado de dado. | Diagrama Drafting consistente com os quadros e painéis modelados; planta da sala de auxiliares. | Cada carga é explicitada como medida, especificada ou hipótese. Sem missão de bateria e lista de cargas, capacidade, Ah, autonomia e UPS ficam `NÃO CALCULADO`; não usar valores de catálogo como demanda real. |
| **E-08 — Aterramento e proteção contra descargas** | Malha conceitual com espaçamento identificado; condutores/eletrodos; conexões equipotenciais de GIS, TR, estrutura, cercas e edifícios; pontos de inspeção; captores/descidas apenas se a avaliação de risco definir solução; detalhe típico de conexão conceitual. | Planta do modelo com linhas/símbolos; cortes/detalhes em vista Drafting quando não houver elemento 3D executivo. | Espaçamento de estudo não é resultado de dimensionamento. Sem resistividade, corrente e tempo de falta, não publicar tensão de toque/passo, seção final do condutor ou conformidade. |
| **C-01 — Bases, canaletas, drenagem e detalhes civis** | Planta de bases e apoios; dimensões e níveis de estudo; contenção e saída de óleo conceituais; canaletas/caixas; passagens sob via; pavimento e drenagem de superfície; detalhes ampliados base–equipamento, canaleta–edifício e barreira–TR; cargas e documentos de interface. | Plantas e cortes do RVT; detalhes adicionais identificados “CONCEITUAL” se não modelados com geometria final. | Bases alinham com famílias e coordenadas de equipamento. Armadura, carga, fundação, solo, volume de óleo e drenagem final ficam pendentes; não desenhar detalhe de fabricação sem cálculo/fonte. |
| **BIM-01 — Axonometria geral e vistas 3D** | Axonometria de implantação com edifícios, GIS, TRs, sala MT, comando, acessos, vias e principais rotas; tags principais, legenda de cores por sistema, norte/orientação e chave `REFERENCIADO/ESTIMADO/CONCEITUAL`. Usar enquadramento que mostre o empreendimento inteiro. | `3D-01` como vista principal; outras vistas 3D requeridas aparecem como painéis/insets com legenda, não como miniaturas sem leitura. | Captura exportada do RVT; confirmar câmera, seção, categoria e presença dos sistemas. Não retocar para inserir equipamento inexistente. |
| **Q-01 — Tabela de equipamentos e quantitativos** | Schedules exportados do modelo para GIS por bay, TR/acessórios, cubículos por seção, painéis, auxiliares, canaletas/leitos, elementos civis principais e famílias; colunas mínimas: tag, família/tipo, categoria, sistema, unidade, quantidade, dimensões, tensão/corrente/potência disponíveis, estado, fonte. | Schedules Revit ou CSV diretamente extraído do RVT, diagramados em folha A3 com subtotais por sistema. | Recalcular totais a partir de elementos do RVT; comparar com E-01 a E-07. Não preencher quantidade por desenho esquemático quando não houver elemento correspondente. |
| **BIM-02 — Cortes 3D, vistas internas e separação controlada** | Vista interna do GIS; corte 3D mostrando compartimentos e barras; corte de TR com tanque/acessórios; vista explodida controlada de um bay e interface de TR; identificação dos vetores/ordem de separação; chamada de detalhe construtivo. | `3D-02` a `3D-07`; corte/section box e displacement controlado no projeto, sem deslocar elementos na vista de coordenação principal. | Explodido é vista explicativa, não muda posição do modelo principal. Se deslocamento Revit não puder ser validado, substituir por corte seccionado e marcar vista explodida como pendente. |
| **M-01 — Cálculos e parâmetros elétricos** | Tabela central: IDs, circuito/equipamento, entradas e unidades, método/fórmula, resultado/unidade, fonte/hipótese, status e arquivo/script de reprodução. Incluir carga/corrente, placa dos TR, carregamento, N-1/shedding, curto preliminar, feeders, sensibilidade de FP, proteção candidata, auxiliares e itens pendentes. | Resultados gerados pelo script de cálculo; tabela sintética no A3 e memorial detalhado em Markdown/PDF/CSV/JSON. | Reexecutar script e comparar cada célula da prancha. Separar calculado de estimado e pendente. Curto “transformador somente”, proteção e condutor preliminar não podem ser apresentados como estudo de coordenação ou dimensionamento completo. |

## Registro obrigatório das 8 vistas tridimensionais

| ID / vista Revit | Enquadramento e conteúdo | Prancha principal | Visibilidade e validação |
|---|---|---|---|
| **3D-01 — Geral isométrica** | Todo o terreno, acessos, edifícios, pátio, transformadores e sistemas principais. | BIM-01; miniatura A-01. | Section box inclui todo o terreno e altura; sem corte que esconda um sistema principal. |
| **3D-02 — Interior GIS** | Vista próxima ao longo das cinco baias; barras A/B, compartimentos, CB/DS/ES, TCs/TPs e terminações identificados. | BIM-02; detalhe E-02. | Ocultar/remover somente a parede/teto de corte da vista; não ocultar componentes de equipamento para limpar a imagem. |
| **3D-03 — Transformadores** | TR-01 e TR-02, bases, contenção, radiadores, ventiladores, buchas, conexões e corredor de acesso. | E-03; miniatura BIM-01 se legível. | Tags de TR e componentes batem com Q-01; acesso e espaço de manutenção visíveis. |
| **3D-04 — Sala MT** | Frente de cubículos, agrupamento A/tie/B, barras, corredores, entradas de cabos e etiquetas. | E-04. | 15 cubículos e suas marcas; tie marcado normalmente aberto como hipótese. |
| **3D-05 — Proteção e controle** | Painéis em filas, áreas de operação, UPS/baterias e caminho de bandeja/fibra. | E-05. | Contagem/posição bate com planta e Q-01; painel não representa IED/configuração de fabricante inexistente. |
| **3D-06 — Explodida controlada** | Um bay GIS e uma interface do transformador com separação por sistema (invólucro, mecanismo, CB, seccionadores, medição, suporte, base). | BIM-02. | Displacement paths/offsets controlados e legendados; vista normal do modelo permanece inalterada. Sem separação validada, status `PENDENTE`. |
| **3D-07 — Corte** | Corte atravessando hall GIS/rotas ou sala MT e infraestrutura inferior, expondo o que planta/axonometria escondem. | BIM-02 e/ou C-01. | Plano de corte e elementos cortados definidos no RVT; corresponder a corte 2D usado em C-01. |
| **3D-08 — Infraestrutura** | Trecho contínuo GIS–canaleta–TR/sala MT/controle, com rotas segregadas, caixas, suporte e entrada de sala. | E-06. | Percursos são elementos modelados e têm origem/destino; rota conceitual não simula cabo conectado. |

## Matriz de complexidade por sistema e família

| Sistema | Componentes que devem ser elementos separados | Informação parametrizada mínima | Nível-alvo / limite |
|---|---|---|---|
| GIS 145 kV | Invólucro/módulos, CB, DS, ES, TC, TP, terminal, barra, suporte e mecanismo representativo por bay. | Tag, bay, sistema, tensão, corrente se selecionada, fonte, dimensão e estado. | Detalhe compatível com coordenação somente quando desenho de contorno/dados do fabricante estiver disponível; estimativa não é LOD 300 validado. |
| Transformadores | Tanque, buchas AT/MT, radiadores, ventiladores, conservador condicional, painel local, base e contenção. | Tag, TR, potência/tensões/Z com status, dimensões e fonte. | Geometria estimada permanece identificada; perda, óleo, placa e resfriamento dependem do fabricante. |
| 13,8 kV | Cubículos por incomer, medição, feeder e tie; barras e terminações. | Tag, seção, alimentador, tensão, corrente candidata, status e carga relacionada. | Distribuição física e diagrama precisam concordar; rating interruptivo pendente sem curto de rede. |
| Proteção/controle | Painéis IED, controle, SCADA/gateway, relógio/sincronismo, switches, QAC/QCC, UPS e bateria. | Tag, bay/função, sistema, fonte, status e destino funcional. | Arquitetura conceitual; sem ajustes/protocolo/testes até engenharia e evidência. |
| Infraestrutura | Canaleta, leito, caixa, suportação, travessia e ligação equipotencial. | ID da rota, origem/destino, classe de cabo, comprimento extraído e ocupação/status. | Caminho BIM verificável; seção/segregação/capacidade pendente sem lista de cabos e método de instalação. |
| Civil | Paredes, pisos, bases, acessos, contenções, barreiras e drenagem conceitual. | ID, nível, dimensões, sistema associado, fonte e status. | Conflitos geométricos verificáveis; dimensionamento estrutural/geotécnico fora do escopo enquanto faltarem dados. |
| Aterramento/SPDA | Malha, derivações, pontos de ligação, eletrodos e elementos de captação/descida se definidos. | ID, função, coordenada, seção/status e equipamento associado. | Conceitual até estudo de solo, falta e critério aplicável; sem aprovação por visualização. |

## Portões de aceite por desenho

Uma prancha passa a `EMITIDA` somente quando: (a) a vista/elementos existem no RVT; (b) tags, quantidades e topologia cruzam com a base; (c) escala, notas e legendas são legíveis em A3; (d) hipóteses, fontes e pendências estão explícitas; (e) PDF foi exportado do Revit; (f) PNG da página completa foi renderizado; (g) a página foi inspecionada visualmente e o checklist da linha foi marcado. Falha em qualquer item mantém a folha `RASCUNHO` ou `PENDENTE`.

## Conteúdo que permanece em aberto por falta de dado

Não preencher com números inventados: curto da concessionária; placa e desenho dimensional do transformador; ratings de GIS e cubículos; lista e método de instalação dos cabos; cargas reais CA/CC; missão/autonomia de bateria; resistividade de solo; corrente/tempo de falta; critérios de incêndio, drenagem, ruído, afastamentos e geotecnia. Para cada pendência, registrar responsável/dado necessário e quais desenhos/cálculos ela bloqueia.

## Estado de produção da R8

Os códigos abaixo aparecem no RVT, no PDF exportado e no índice de PNGs. `Presente` descreve a existência do artefato; todas as folhas continuam `PARCIAIS` porque ainda falham em pelo menos um critério de conteúdo ou validação. A matriz detalhada das pendências por folha está em [Matriz de pranchas](matriz-de-pranchas.md).

| Página / código | Evidência R8 | Situação de aceite |
|---|---|---|
| 01 / A-01 | Planta de implantação no RVT/PDF/PNG | Parcial: cotas, acessos, afastamentos e interfaces não plenamente verificados. |
| 02 / A-02 | Planta de arquitetura no RVT/PDF/PNG | Parcial: faltam cortes, fachadas e verificações legais/funcionais. |
| 03 / E-01 | Diagrama funcional no PDF/PNG e tags de estudo | Parcial: hipótese topológica sem dados de rede e proteção validados. |
| 04 / E-02 | Arranjo GIS no RVT/PDF/PNG | Parcial: faltam elevações/cortes dimensionados e referência de fabricante. |
| 05 / E-03 | Famílias e vistas de transformadores no RVT/PDF/PNG | Parcial: geometria estimada; dados de placa e acessórios não confirmados. |
| 06 / E-04 | Planta de cubículos e vista 3D no RVT/PDF/PNG | Parcial: curto, ratings, alimentadores e seletividade pendentes. |
| 07 / E-05 | Diagrama funcional e painéis conceituais no PDF/PNG | Parcial: não há lógica, ajustes ou rede de automação implementados/testados. |
| 08 / E-06 | Canaletas e leitos conceituais no RVT/PDF/PNG | Parcial: cabos e ligações MEP físicas ausentes; não dimensionado. |
| 09 / E-07 | Diagrama conceitual de serviços auxiliares no PDF/PNG | Parcial: um sistema lógico QAC-01–UPS-01; sem conexão física ou autonomia verificada. |
| 10 / E-08 | Grade de linhas de aterramento no RVT/PDF/PNG | Parcial: dados de solo/falta e tensões de toque/passo ausentes. |
| 11 / C-01 | Planta conceitual de bases/infraestrutura no RVT/PDF/PNG | Parcial: estrutura, geotecnia, drenagem e interfaces não dimensionadas. |
| 12 / Q-01 | Tabela extraída do RVT em PDF/PNG e CSV | Parcial: cobertura de propriedades e sistemas ainda incompleta. |
| 13 / BIM-01 | Vista geral isométrica no RVT/PDF/PNG | Parcial: partes internas ocultas por cobertura e leitura espacial limitada. |
| 14 / BIM-02 | Vista interna do GIS no RVT/PDF/PNG | Parcial: sem coordenação dimensional integral ou dados de fabricante. |
| 15 / BIM-03 | Vista 3D dos transformadores no RVT/PDF/PNG | Parcial: geometrias e interfaces estimadas. |
| 16 / BIM-04 | Vista 3D da sala de cubículos no RVT/PDF/PNG | Parcial: ratings e conectividade física pendentes. |
| 17 / BIM-05 | Vista 3D dos painéis no RVT/PDF/PNG | Parcial: arranjo conceitual sem configuração funcional. |
| 18 / BIM-06 | Vista explodida no RVT/PDF/PNG | Parcial: relações e deslocamentos ainda precisam de auditoria BIM. |
| 19 / BIM-07 | Vista de corte espacial no RVT/PDF/PNG | Parcial: rotas são conceituais e não há cabos físicos. |
| 20 / BIM-08 | Vista de infraestrutura/interfaces no RVT/PDF/PNG | Parcial: sem checagem completa de interferência ou conectividade. |
| 21 / M-01 | Quadro de resultados no PDF/PNG e fontes JSON/CSV/MD | Parcial: cálculos de rede, condutores, proteção e aterramento ainda não concluídos. |

As 21 páginas e 21 PNGs foram inspecionados visualmente em folhas de contato. A inspeção não substitui conferência em tamanho real de cada nota, dimensão ou referência cruzada. Nenhuma folha R8 foi aprovada como `EMITIDA`.
