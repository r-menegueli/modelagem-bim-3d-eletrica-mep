# Registro de validação — R8

Data: 2026-11-07. Este registro distingue geração e inspeções efetivamente observadas de critérios técnicos ainda pendentes.

## Revit e modelo

- Autodesk Revit 2027.3 abriu o arquivo `19-subestacao-gis-r8.rvt` na vista `00 - Implantacao - ±0,00 m` após a geração.
- O log de geração identifica o documento Revit aberto como origem, registra 30 arquivos RFA, 21 folhas e 8 vistas 3D, e confirma a gravação do RVT de 8.327.168 bytes e a exportação do PDF de 21 folhas.
- A auditoria extraída do RVT registra 191 FamilyInstances: 46 em Electrical Equipment e 145 em Generic Models; 21 paredes; 7 pisos; 180 ModelCurves; zero DirectShapes; 93 conectores MEP; zero conectores fisicamente conectados; 1 sistema elétrico lógico; 1 elemento terminal; zero equipamentos elétricos sem conectores e zero instâncias sem os campos de identidade requeridos.
- O único sistema é o circuito lógico auxiliar QAC-01–UPS-01. Não há cabo físico nem conexão MEP física validada. As 30 famílias em disco não significam que todas tenham instâncias no modelo.

## Pranchas e imagens

- PDF A3 paisagem: 21 páginas, 1190,52 × 841,8 pontos por página, 2.313.451 bytes.
- Foram geradas 21 imagens PNG a 120 dpi e 8 prévias de vistas 3D exportadas do RVT.
- As 21 páginas foram revisadas em folhas de contato. A Q-01 foi inspecionada em alta resolução após reposicionar a linha inferior. A conferência confirma a presença e o enquadramento geral, mas não atesta a legibilidade de cada nota em tamanho de impressão nem o atendimento completo aos critérios da matriz.
- A revisão visual identificou espaços livres grandes e áreas internas ocultadas por coberturas nas vistas gerais. As folhas e vistas seguem classificadas como parciais.

## Cálculos e compilação

- O comando `python -m unittest -v tests.test_gis_substation_study tests.test_gis_substation_auxiliary` executou 10 testes; todos passaram. Os testes cobrem regras dos scripts e premissas declaradas, não dados de campo, fabricantes ou requisitos da concessionária.
- O add-in `PortfolioExpansion.csproj` foi compilado com .NET SDK 10.0.402. O resumo local do build mostra zero erros e quatro avisos, incluindo conflitos de referência MSB3277 e uma tentativa de cópia MSB3026 enquanto a DLL estava em uso. O modelo foi gerado no Revit e reaberto posteriormente.
- A arquitetura e os valores elétricos são hipóteses explícitas. Curto da rede, ratings confirmados, seletividade, dimensionamento de cabos, autonomia com lista real de cargas e aterramento com dados de solo permanecem pendentes.

## Arquivos grandes e Git LFS

- RVT: 8,33 MB; PDF: 2,31 MB; maior RFA observado: 491.520 bytes.
- Nenhum arquivo R8 ultrapassa 50 MiB, limiar em que o GitHub emite aviso para Git comum, ou 100 MiB, limite que o GitHub bloqueia. Foi mantido no Git comum; LFS acrescentaria cotas de armazenamento e banda sem benefício necessário neste pacote. Consulte [limites de arquivos do GitHub](https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github) e [cotas de Git LFS](https://docs.github.com/en/billing/concepts/product-billing/git-lfs).

## Conclusão de aceite

A R8 foi gerada e aberta no Revit; seus dados de auditoria, cálculos reproduzíveis, PDF e PNGs existem e foram inspecionados conforme descrito. A revisão não aprova o estudo como projeto executivo, não atesta LOD 300 e não valida conexões físicas. As pendências por prancha constam em [matriz de pranchas](../matriz-de-pranchas.md) e [especificação de complexidade](../especificacao-de-complexidade-dos-desenhos.md).
