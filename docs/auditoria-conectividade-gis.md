# Auditoria R11 — conectividade, CTs e coerência do unifilar

**Data:** 07/11/2026  
**Escopo:** leitura estática dos CSVs/JSONs R8, R9 e R11, do gerador C# e do construtor do grafo; inspeção visual do preview E-01.  
**Limite:** não abri o RVT nesta auditoria e não alterei o RVT nem o add-in. O JSON R11 registra `GENERATED_FROM_OPEN_REVIT_DOCUMENT` e zero avisos, mas isso é evidência do arquivo de auditoria existente, não uma verificação Revit repetida aqui.

## Resultado executivo

R11 restaura cinco **placeholders** de TC, um por posição de bay GIS. O quinto, `GIS-BC01-CT`, está presente no inventário, no grafo e no modelo descrito pelos arquivos de extração. Isso corrige a diferença de contagem introduzida na R9. A quantidade de cinco não é uma contagem de projeto de proteção: cada posição é somente uma instância genérica, sem definição de conjunto trifásico, posição, relação, núcleos ou burden.

O unifilar E-01 representa coerentemente a topologia operacional declarada: L01 e TR-01 selecionam a Barra A; TR-02 e L02 selecionam a Barra B; os seletores do acoplador ficam fechados e o disjuntor 52-BC aberto; o tie MT fica normalmente aberto. O grafo e a tabela CSV concordam nos 58 IDs e endpoints, e os nós internos têm correspondência de marca/ElementId no inventário. Há, contudo, uma lacuna de rastreio visual: os símbolos de TC são genéricos e a prancha não imprime as marcas individuais `...-CT`.

R11 continua sem circuito de potência HV/MV nem condutor primário físico confirmado. Os leitos e DirectShapes representam infraestrutura e geometria, não conexão elétrica. O único relacionamento elétrico nativo auditado é auxiliar, entre QAC-01 e UPS-01; o fio correspondente é 2D e específico da vista E-06.

## Contagem e origem da diferença de CT

| Revisão | CT no JSON de auditoria | Linhas de CT no quantitativo | Marca do acoplador | Evidência do gerador |
|---|---:|---:|---|---|
| R8 | 5 | 5 | `GIS-BC01-CT` presente | Commit `94606b3`: inclusão incondicional de um CT dentro do loop de cinco bays. |
| R9 | 4 | 4 | ausente | Commit `b65e441`: `if(i!=2)` antes da inclusão; o índice 2 é o terceiro bay, `GIS-BC01`. |
| R11 | 5 | 5 | `GIS-BC01-CT` presente | O código atual percorre os cinco bays sem excluir o índice 2 e atribui ao acoplador um comentário explícito de placeholder. |

A R9 também cai de 46 para 45 elementos de equipamento elétrico e de 170 para 169 elementos em relação à R11. A comparação dos `family_type_counts` R9/R11 encontrou apenas uma diferença de família: `GIS_CurrentTransformer | GIS_CT_145KV_STUDY`, de 4 para 5. Isso isola a discrepância ao CT omitido do acoplador, sem confundi-la com mudanças de geometria ou documentação.

Em R11, os cinco marks encontrados são `GIS-L01-CT`, `GIS-T01-CT`, `GIS-BC01-CT`, `GIS-T02-CT` e `GIS-L02-CT`. Cada linha usa a família/tipo genérico `GIS_CurrentTransformer / GIS_CT_145KV_STUDY`, registra zero conectores MEP e classifica a geometria como envelope sem conector de potência. O comentário do CT do acoplador deixa explícito que quantidade por fase, posição, relação, núcleo e burden dependem do esquema do fabricante.

**Leitura correta da contagem:** cinco é o número de placeholders adotado para cobrir os cinco bays (duas entradas, dois bays de transformador e acoplador). Não determina o número de conjuntos de TC necessários. A contagem e a posição finais dependem do arranjo GIS, das zonas de proteção e dos núcleos destinados às funções de linha, transformador, barras e falha de disjuntor. Uma FamilyInstance por bay também não representa três TCs físicos por fase nem um núcleo por função.

## Unifilar E-01 versus IDs e topologia

O preview de E-01 mostra cinco bays GIS e as duas barras principais de 138 kV. Conferi no desenho os estados seguintes com os endpoints/estados de `relacoes-rede-r11.csv` e `grafo-conectividade-r11.json`:

- `GIS-L01` e `GIS-T01`: 89A fechado e 89B aberto, selecionando Barra A;
- `GIS-T02` e `GIS-L02`: 89A aberto e 89B fechado, selecionando Barra B;
- `GIS-BC01`: 89A e 89B fechados e 52-BC aberto, conforme hipótese indicada na folha;
- `TR-01` segue para QMT-A/Barra MT A; `TR-02` para QMT-B/Barra MT B;
- 52-TIE MT aparece normalmente aberto; os dez alimentadores estão identificados por seção no diagrama.

Verificações de correspondência realizadas nos artefatos: 62 nós esperados no grafo; 58 relações no CSV e JSON, com IDs/endpoints iguais; zero endpoints sem nó; zero divergências entre ElementId/marca dos elementos usados pelos nós e as linhas do inventário. O resumo do grafo também registra zero nós BIM internos e zero rotas geométricas esperadas ausentes. Os dois pontos de rede externos e os destinos remotos dos alimentadores continuam fora do limite do modelo.

O cruzamento é coerente por bay e por função, mas não é uma identificação individual completa na prancha: no E-01, os quatro TCs de linha/transformador aparecem como símbolos genéricos “TC”; o acoplador recebe a chamada “TC placeholder”, próxima ao rótulo `GIS-BC01`. Não aparece a marca exata `GIS-BC01-CT` nem as demais marcas `...-CT` junto aos símbolos. Assim, a associação é recuperável pelo bay e pelo grafo, mas não diretamente conferível por tag no desenho. Para fechar a rastreabilidade, imprimir marcas curtas junto aos símbolos e conservar uma legenda que as relacione às marcas BIM.

Há uma inconsistência de metadado na relação de proteção do acoplador: a aresta `GIS-BC-CT-PROTECTION` liga `GIS-BC01-CT` a `P-BUS`, com `voltage_kv=0.125`. O nó `P-BUS` representa painel funcional de proteção 125 Vcc, mas o endpoint do TC para o painel é uma relação candidata de proteção, não uma conexão de potência a 125 V. O secundário do TC deve ser especificado por corrente e núcleo, separado da alimentação CC do relé. A aresta está classificada corretamente como `DIAGRAM_ONLY`; o valor de tensão deve ser removido/corrigido no esquema de dados para evitar leitura como circuito físico. O grafo não registra relações explícitas equivalentes dos outros quatro TCs para os painéis de proteção de linha e transformador.

## O que existe no modelo e o que ainda não está conectado

### Ligações elétricas nativas

- A auditoria registra 3 conectores MEP em equipamentos: dois `DomainElectrical`/`End` conectados em QAC-01 e UPS-01, e um conector lógico `DomainUndefined` em QAC-01.
- Há um `ElectricalSystem` auxiliar e um `Wire` nativo com 2/2 conectores ligados, específico do plano E-06. O próprio relatório classifica o Wire como não sendo cabo 3D.
- A auditoria registra 0 condutores primários físicos e 44 de 46 equipamentos elétricos sem conectores MEP. Todos os cinco CTs têm zero conectores.
- Não há relação de potência conectada no Revit para barras, seccionadoras, disjuntores, transformadores, cubículos MT ou alimentadores. As relações aparecem no grafo e em E-01, classificadas como `DIAGRAM_ONLY` ou `GEOMETRIC_ROUTE_ONLY`.

### Geometria e infraestrutura

- A extração R11 registra 84 DirectShapes geométricos, 175 ModelCurves e 14 relações de rota `GEOMETRIC_ROUTE_ONLY`. Carcaças de barra, stubs GIS e envelopes de transição por fase não têm contato interno nem conector elétrico.
- Há 8 segmentos nativos CableTray com 16 conectores, dos quais 8 estão `IsConnected`: quatro juntas internas confirmadas nas duas rotas MT. Os leitos `LEITO-CTRL` e `LEITO-GIS` permanecem sem juntas. As conexões confirmadas são entre segmentos de infraestrutura; não conectam cabos nem equipamentos.
- Os dez alimentadores têm posições no unifilar, mas não têm cabos de saída, terminação remota, carga ou ampacidade modelados. As transições TR–cubículo têm envelopes geométricos, sem circuito de potência.

Não se deve somar leitos, DirectShapes, ModelCurves, Wire 2D, conectores sem ligação e relações de grafo como se fossem circuitos ou condutores de potência.

## Conclusões e correções propostas para a próxima revisão

1. Manter o estado de `GIS-BC01-CT` como placeholder explicitamente pendente; não reportar cinco como quantidade final de TCs.
2. Fechar o esquema de proteção e a quantidade/posição dos TCs após definir fabricante/GA e zonas 87B, 50BF, proteção de linha e de transformador; depois criar tags por fase/núcleo conforme o projeto aprovado.
3. Colocar identificadores dos CTs e, idealmente, tags dos equipamentos de manobra junto aos símbolos do E-01, mantendo as marcas BIM existentes.
4. Corrigir o campo `voltage_kv=0.125` da relação CT–P-BUS. Registrar separadamente a alimentação 125 Vcc do painel e o circuito secundário de corrente do TC, sem convertê-los em circuito Revit fictício.
5. Preservar a separação de evidências: `ElectricalSystem`/Wire auxiliar, juntas de leito, envelopes geométricos e topologia diagramática devem continuar sendo contabilizados em categorias distintas.

## Evidências locais

- [Auditoria R8](../projetos/19-subestacao-gis/dados/reconstrucao-r8/auditoria-modelo-r8.json) e [quantitativos R8](../projetos/19-subestacao-gis/dados/reconstrucao-r8/quantitativos-modelo.csv)
- [Auditoria R9](../projetos/19-subestacao-gis/dados/reconstrucao-r9/auditoria-modelo-r9.json), [quantitativos R9](../projetos/19-subestacao-gis/dados/reconstrucao-r9/quantitativos-modelo.csv) e [comparativo R8/R9](../projetos/19-subestacao-gis/dados/reconstrucao-r9/comparativo-r8-r9.md)
- [Auditoria R11](../projetos/19-subestacao-gis/dados/reconstrucao-r11/auditoria-modelo-r11.json), [quantitativos R11](../projetos/19-subestacao-gis/dados/reconstrucao-r11/quantitativos-modelo.csv), [conectividade por equipamento](../projetos/19-subestacao-gis/dados/reconstrucao-r11/conectividade-equipamentos-r11.csv), [relações de rede](../projetos/19-subestacao-gis/dados/reconstrucao-r11/relacoes-rede-r11.csv) e [grafo R11](../projetos/19-subestacao-gis/dados/reconstrucao-r11/grafo-conectividade-r11.json)
- [Gerador de modelo e E-01](../scripts/revit-addin/RevitPortfolioGenerator.cs#L1441) e [especificação do grafo](../scripts/engineering/gis_connectivity.py#L95)
- [Preview PNG E-01](../projetos/19-subestacao-gis/pranchas/reconstrucao-r11/preview/folhas/folha-03.png)

