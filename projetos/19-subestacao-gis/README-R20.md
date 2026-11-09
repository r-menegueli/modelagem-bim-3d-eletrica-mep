# Projeto 19 — Subestação GIS 138/13,8 kV

**Revisão candidata:** R20-23 · **Classificação:** estudo BIM demonstrativo, não executivo.

## Entregáveis

- [Modelo Revit 2027](attempts/r20-23-candidate/revit/reconstrucao-r20/19-subestacao-gis-r20.rvt)
- [Pranchas A3 — 14 folhas](attempts/r20-23-candidate/pranchas/reconstrucao-r20/19-subestacao-gis-r20-23-A3.pdf)
- [PDF Carta impresso pelo Revit](attempts/r20-23-candidate/pranchas/reconstrucao-r20/19-subestacao-gis-r20-23.pdf)
- [Comparativo R8 × R20-23](attempts/r20-23-candidate/dados/reconstrucao-r20/comparativo-r8-r20-23.md)
- [Auditoria de conectividade e quantitativos](attempts/r20-23-candidate/dados/reconstrucao-r20/auditoria-grafo-inventario-v1/RELATORIO.md)
- [Memorial dos cálculos N-1 e vínculo com o modelo](attempts/r20-23-candidate/dados/reconstrucao-r20/calculos-n1-model-crosswalk-v1/memorial-de-calculo.md)
- [Famílias Revit do estudo](attempts/r20-23-candidate/familias/reconstrucao-r23-f35-envelope-review/)
- [Auditoria de conectividade GIS do repositório](../../docs/auditoria-conectividade-gis.md)

## Estado verificado

A extração da API no Revit 2027.3 identificou 168 instâncias de família, 24 DirectShapes, 8 segmentos nativos de CableTray, 25 conectores MEP, um ElectricalSystem auxiliar e um Wire 2D. Foram auditados 73 nós e 70 relações. Doze geometrias de rota foram encontradas e comparadas aos envelopes de equipamento; elas não identificam terminais, condutores ou continuidade elétrica. Os 8 conectores de leito marcados como conectados correspondem a juntas de infraestrutura.

O modelo não contém circuitos primários confirmados em 138 kV ou 13,8 kV, nem condutores primários 3D confirmados. O sistema auxiliar QAC-01→UPS-01 é uma associação lógica; o Wire é específico da vista E-06 e a carga não foi confirmada pelo cálculo do Revit. O unifilar documenta a dupla barra, o acoplador e os alimentadores, mas não os transforma em sistemas elétricos conectados no RVT.

Os 32 cálculos foram vinculados a 33 marcas do modelo sem marcas ausentes ou duplicadas. O cenário com acoplamento MT normalmente aberto não atende todas as cargas críticas após perda de um transformador. As entradas permanecem hipóteses documentadas; curto-circuito completo, ampacidade, seletividade, aterramento e dimensionamentos executivos requerem dados e estudos adicionais.

## Verificação e limitações

- Os 146 testes da suíte passaram; `compileall` passou. Isso verifica software e casos de cálculo, não certifica engenharia.
- O documento foi aberto e auditado no Revit 2027.3, com zero avisos registrados pela API. A vista 3D ao vivo permaneceu visualmente vazia nesta sessão; as pranchas e prévias PNG foram usadas na revisão visual.
- O exportador PDF nativo não concluiu por indisponibilidade local de recursos de renderização/material. As folhas foram impressas em Carta pelo Revit; a versão A3 foi normalizada uniformemente e preserva o PDF Carta original.
- GIS 145 kV usa o envelope publicado de uma baia GE Vernova F35 como referência externa; a configuração e o GA de pedido não foram selecionados. Dimensões de transformador e demais geometrias estimadas estão identificadas no relatório.

**Conclusão:** revisão útil para estudo e documentação, ainda parcial. Não apresentar como projeto executivo, dimensionamento aprovado, seletividade comprovada, conformidade normativa ou pronto para construção. Não houve publicação no LinkedIn.
