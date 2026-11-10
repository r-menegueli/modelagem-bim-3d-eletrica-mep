# Projeto 19 — Subestação GIS 138/13,8 kV

## Estado nesta branch — 10/11/2026

R20-23 é a candidata mais recente gerada e extraída de um documento aberto no Revit 2027.3. Ela está preservada nesta branch de desenvolvimento; a branch `main` continua com os 14 projetos originais, sem integração desta revisão.

| Artefato | Evidência |
| --- | --- |
| Modelo Revit | [RVT R20](attempts/r20-23-candidate/revit/reconstrucao-r20/19-subestacao-gis-r20.rvt) |
| Pranchas | [PDF A3 com 14 páginas](attempts/r20-23-candidate/pranchas/reconstrucao-r20/19-subestacao-gis-r20-23-A3.pdf) · [prévia das folhas](attempts/r20-23-candidate/pranchas/reconstrucao-r20/preview/contact-r20-23.png) |
| Auditoria extraída | [JSON](attempts/r20-23-candidate/dados/reconstrucao-r20/auditoria-modelo-r20.json) |
| Conectividade e relações | [conectores por equipamento](attempts/r20-23-candidate/dados/reconstrucao-r20/conectividade-equipamentos-r20.csv) · [grafo de relações](attempts/r20-23-candidate/dados/reconstrucao-r20/relacoes-rede-r20.csv) |
| Comparação e pendências | [R8 × R20-23](attempts/r20-23-candidate/dados/reconstrucao-r20/comparativo-r8-r20-23.md) |

![Prévia das 14 folhas A3 R20-23](attempts/r20-23-candidate/pranchas/reconstrucao-r20/preview/contact-r20-23.png)

## Auditoria do modelo

A extração da API do Revit registrou 168 instâncias, 46 equipamentos elétricos, 24 DirectShapes, 8 elementos CableTray nativos, 25 conectores MEP, 1 ElectricalSystem auxiliar, 1 fio 2D, 14 folhas, 4 vistas 3D e zero avisos do documento. Dos 25 conectores MEP, dois aparecem ligados ao sistema auxiliar QAC-01–UPS-01. As juntas de CableTray representam conectividade de infraestrutura.

Os 12 trajetos faseados, as barras GIS e os demais DirectShapes são geometria de estudo. Não há condutores ou circuitos nativos conectando a rede primária de 138 kV e a distribuição de 13,8 kV. O sistema auxiliar e o fio 2D não comprovam cabo 3D nem alimentação completa da UPS.

## Pranchas e verificação visual

O PDF foi renderizado e as 14 páginas foram inspecionadas. A exportação PDF nativa do Revit não funcionou nessa execução por indisponibilidade do renderer e de bibliotecas de material; foi usada impressão para PDF e normalização uniforme para A3. O arquivo RVT abriu e foi auditado pela API, mas o canvas da sessão permaneceu visualmente em branco. Portanto, não há confirmação de inspeção direta das vistas dentro do canvas do Revit.

Os testes de contrato do gerador passaram na revisão R20-23. Isso verifica o código e os arquivos previstos pelos testes, não conectividade elétrica de potência, dimensionamento ou segurança.

## Limites do estudo

R20-23 é demonstrativa e acadêmica. Não é projeto executivo, aprovação normativa, prova de dimensionamento nem autorização para obra, compra ou instalação. Permanecem pendentes dados de fabricante e concessionária, cabos e terminais, aterramento, coordenação de proteção, intertravamentos, interfaces civis e validação profissional independente.

## Revisões anteriores preservadas

- [R11 — modelo e documentação](revit/reconstrucao-r11/19-subestacao-gis-r11.rvt) · [PDF de 14 folhas](pranchas/reconstrucao-r11/19-subestacao-gis-r11.pdf) · [comparativo R8 × R11](dados/reconstrucao-r11/comparativo-r8-r11.md)
- [Baseline R8 — auditoria](dados/reconstrucao-r8/auditoria-modelo-r8.json) · [modelo](revit/reconstrucao-r8/19-subestacao-gis-r8.rvt) · [PDF](pranchas/reconstrucao-r8/19-subestacao-gis-r8.pdf)
