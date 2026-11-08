# Auditoria comparativa da subestação GIS — R8 a R12

**Data:** 07/11/2026  
**Resultado:** R12 parcialmente implementada e auditada; não atende ao critério de conclusão de engenharia.  
**Escopo:** comparar dados extraídos pelo add-in Revit, conferir o RVT no Revit 2027.3, revisar visualmente as 14 páginas do PDF R12 e executar a suíte Python.

## Síntese

A R12 acrescenta conectores de carga às famílias dos painéis auxiliares e instrumenta a validação para exigir valores calculados pelo Revit antes de contabilizar um `ElectricalSystem`. O Revit calculou zero watt, zero VA e zero ampere para todas as 11 saídas auxiliares tentadas; cada tentativa foi revertida. Portanto, o modelo final continua com apenas o sistema lógico QAC-01 → UPS-01 e um `Wire` 2D na vista E-06. Não há cabo 3D nem rede de potência nativa entre os equipamentos de 138 kV, os transformadores, as barras de 13,8 kV e os alimentadores.

O R12 foi aberto no Revit 2027.3 a partir do caminho final salvo; a janela exibiu `19-subestacao-gis-r12.rvt` e a planta `00 - Implantacao - ±0,00 m`. A extração da API salva no JSON registra o documento de origem `19-subestacao-gis-r12`, 170 instâncias de família, zero avisos do documento, 14 folhas e quatro vistas 3D. A abertura manual confirma que o RVT existe e carrega; não valida por si só especificação executiva ou conectividade ausente.

## Quantitativos comparáveis

| Revisão | Instâncias de família | Equipamentos elétricos | Famílias genéricas | Conectores MEP | `IsConnected` elétrico | `ElectricalSystem` | `Wire` 2D (conectores ligados) | CableTray | Folhas | Vistas 3D | Avisos Revit |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| R8 | 191 | 46 | 145 | 93 | não extraído no mesmo esquema | 1 | não extraído no mesmo esquema | — | 21 | 8 | 0 |
| R9 | 169 | 45 | 124 | 3 | 2 | 1 | 1 (2/2) | 8 | 14 | 4 | 0 |
| R11 | 170 | 46 | 124 | 3 | 2 | 1 | 1 (2/2) | 8 | 14 | 4 | 0 |
| R12 | 170 | 46 | 124 | 14 | 2 | 1 | 1 (2/2) | 8 | 14 | 4 | 0 |

As contagens provêm dos JSONs de auditoria de cada revisão; campos vazios na R8 significam que aquele campo não foi extraído com o mesmo esquema, e não que o valor seja zero. Na R12, oito conectores de leitos estão `IsConnected` de um total de 16; são juntas de infraestrutura entre segmentos de CableTray, não condutores elétricos.

## Mudanças implementadas na R12

- Foram adicionados 11 endpoints de carga aos painéis auxiliares, elevando a contagem de conectores MEP de 3 para 14.
- O gerador passou a confirmar painel, terminal, associação do sistema e valores elétricos calculados antes de contar uma saída como circuito válido. As tentativas sem carga calculada são revertidas em subtransações.
- A família do painel de proteção auxiliar fica no modelo com parâmetros de estudo explícitos. A hipótese de 150 W, FP 0,90 e 220 V não é apresentada como carga calculada; nenhuma das 11 saídas foi contada como `ElectricalSystem`.
- Foram publicados relatórios por equipamento, relações de rede, quantitativos e validação das saídas auxiliares com estados separados para conectores, infraestrutura, geometria, relação de diagrama e pendências.
- As cinco posições de TC permanecem como placeholders de família genérica, incluindo o acoplador. A contagem corrige a omissão da R9, mas não especifica quantidade de fases, núcleos, relação, burden ou zona de proteção.

## Comparação da conectividade

| Item | R8 | R12 | Interpretação |
|---|---:|---:|---|
| Conectores MEP | 93 | 14 | A contagem menor não é um indicador isolado de pior conectividade: a R8 tinha endpoints sem uma rede demonstrada. A R12 registra as portas de painel, mas não inventa circuitos para fazê-las parecer conectadas. |
| Conectores elétricos `IsConnected=true` | Não extraído pelo esquema R8 | 2 | Os dois conectores pertencem ao sistema auxiliar existente QAC-01/UPS-01. |
| Sistemas elétricos | 1 | 1 | O único sistema nativo observado é auxiliar e tem carga modelada zero; não representa a rede primária ou secundária de potência. |
| Fios 2D | Sem campo equivalente | 1, 2/2 | O `Wire` é específico de E-06 e não constitui cabo 3D. |
| Condutores físicos de potência confirmados | 0 | 0 | Não existem circuitos ou cabos físicos auditados para GIS, transformadores, barras MT ou dez saídas. |

Os oito trechos CableTray, 84 DirectShapes de rota e 175 ModelCurves não são contados como condutores. Os endpoints das duas entradas de rede e os destinos remotos dos alimentadores permanecem fora do limite físico do modelo.

## Diagrama e pranchas

O E-01 R12 representa barras duplas de 138 kV, cinco bays, seletores 89A/89B, 52-BC normalmente aberto como hipótese, transformadores TR-01/TR-02, barras MT A/B e 52-TIE normalmente aberto. A coerência de topologia e estados é compatível com a premissa declarada. Na inspeção visual do PDF, os quatro símbolos de TC de linha/transformador continuam identificados somente como “TC”; o acoplador mostra “TC placeholder”. As marcas BIM individuais não aparecem ao lado dos símbolos, então a rastreabilidade visual continua incompleta.

O PDF tem 14 folhas A3. A inspeção das 14 páginas encontrou texto e desenhos legíveis no E-01, tabelas de quantitativos e premissas, mas a vista BIM-02 (bay) e BIM-04 (cubículos MT) estão pequenas para a área útil das folhas. A BIM-01 ocupa melhor a folha e a BIM-03 apresenta o transformador ampliado. A existência das 14 folhas não significa que os sistemas elétricos estejam concluídos.

## Geometria e referências

Os envelopes de GIS 145 kV, transformador 40 MVA e cubículos 13,8 kV estão diferenciados por função e acompanhados por referências de fabricante no registro técnico R12. São geometrias externas aproximadas, não um modelo de fabricação escolhido. Não há desenho GA do equipamento selecionado nem representação de contatos internos proprietários.

- GE Vernova F35: classe GIS até 145 kV e bay de referência com 800 mm de largura; o arranjo específico não foi selecionado.
- DAX TRP-EA40-DML-01: referência pública de 138/13,8 kV e 30/40 MVA; o valor de 11% de impedância da referência não foi adotado como placa do projeto.
- ABB UniGear ZS1: referência de cubículos 17,5 kV; envelopes de variantes dependem da configuração.

## Cálculos e testes

As bases BP-01 a BP-15 e os identificadores dos dez alimentadores foram preservados. As sensibilidades de queda de tensão, perdas e curto-circuito com fonte e cabo candidato foram carregadas da R11 sem novo recálculo na R12. Ampacidade, fatores de instalação, X/R da fonte, impedância real e tolerância dos transformadores, contribuição de motores/conversores, capacidade de interrupção e seletividade continuam pendentes. Os 150 W, 220 V e FP 0,90 dos auxiliares são hipóteses; a API Revit retornou carga nativa zero para as tentativas.

Execução de `python -m unittest discover -s tests -v`: **52 testes aprovados**. `git diff --check`: código de saída 0; somente avisos de conversão LF/CRLF do Git. O comando padrão sem `-s tests` encontrou zero testes e não foi usado como evidência de sucesso. Pytest não está instalado neste ambiente.

## Pendências que impedem aprovação

1. Criar e auditar uma representação complementar de conectividade da rede principal, sem tratar barramentos encapsulados, DirectShapes, leitos ou conectores soltos como cabos.
2. Obter parâmetros nativos de carga válidos e, só então, criar circuitos auxiliares que a API Revit realmente calcule.
3. Projetar a geometria, as terminações, as interfaces e os sistemas da seção GIS, transformador e cubículos a partir de desenhos gerais ou manter explicitamente os envelopes como aproximações.
4. Imprimir identificadores de TC junto aos símbolos do E-01 e ampliar as vistas BIM-02 e BIM-04.
5. Completar cálculos com dados de concessionária, placa dos transformadores, método de instalação, agrupamento, ambiente, cargas, proteção e requisitos de desempenho.

**Status:** R12 é uma revisão parcialmente validada, não um projeto executivo nem um modelo de conectividade elétrica concluído.

## Evidências locais

- RVT R12: [arquivo de modelo](../projetos/19-subestacao-gis/revit/reconstrucao-r12/19-subestacao-gis-r12.rvt)
- PDF de 14 folhas: [pranchas R12](../projetos/19-subestacao-gis/pranchas/reconstrucao-r12/19-subestacao-gis-r12.pdf)
- Extração Revit: [auditoria de modelo R12](../projetos/19-subestacao-gis/dados/reconstrucao-r12/auditoria-modelo-r12.json)
- Conectividade por instância e relações: [CSV de conectividade](../projetos/19-subestacao-gis/dados/reconstrucao-r12/conectividade-equipamentos-r12.csv), [CSV de relações](../projetos/19-subestacao-gis/dados/reconstrucao-r12/relacoes-rede-r12.csv) e [relatório](../projetos/19-subestacao-gis/dados/reconstrucao-r12/conectividade-r12.md)
- Validação de circuitos auxiliares: [JSON](../projetos/19-subestacao-gis/dados/reconstrucao-r12/circuitos-auxiliares-r12.json) e [CSV](../projetos/19-subestacao-gis/dados/reconstrucao-r12/circuitos-auxiliares-r12.csv)
- Referências geométricas: [fontes e limites](referencias-tecnicas-gis-r12.md)
