# Auditoria de conectividade por equipamento — R11

Fonte: extração do RVT pelo add-in Revit 2027 e grafo topológico independente. ElementId e UniqueId são extraídos do modelo; IDs sem correspondência são listados como pendência.

## Resultado

- Nós esperados: 62; relações: 58.
- Classes de relação: `DIAGRAM_ONLY`: 43; `GEOMETRIC_ROUTE_ONLY`: 14; `NATIVE_ELECTRICAL_SYSTEM_LOGICAL`: 1.
- ElectricalSystem nativo confirmado: 1 relação auxiliar.
- Condutores primários de potência confirmados: 0.
- Placeholders de TC: 5/5.
- Nós BIM esperados ausentes: 0; rotas geométricas faltantes: 0.

## Classificação

`NATIVE_ELECTRICAL_SYSTEM_LOGICAL` registra somente associação elétrica nativa confirmada pela lista de terminais. `GEOMETRIC_ROUTE_ONLY` indica carcaça ou percurso geométrico sem condutor. `DIAGRAM_ONLY` indica topologia documentada sem conexão MEP. Leitos CableTray permanecem `INFRASTRUCTURE_ONLY`.

## Pendências

- A topologia primária GIS/13,8 kV está no E-01 e no grafo; não existe uma rede Revit PowerCircuit conectada para potência.
- Envelopes DirectShape e leitos CableTray não são condutores; a continuidade entre conectores de leito comprova apenas juntas de infraestrutura.
- Estados de disjuntores e seccionadoras são premissas; não há simulação de manobra nem lógica de intertravamento.
- Cada família GIS_CT é um placeholder genérico; não comprova conjunto trifásico, relação, núcleo, burden ou zona de proteção.
- O ElectricalSystem QAC-01–UPS-01 comprova apenas a associação auditada; o Wire 2D é específico da vista e não há cabo 3D, fonte a montante, saída da UPS ou circuito CC da bateria.
- As conexões com as duas redes externas e os dez destinos remotos dos alimentadores ficam fora do limite modelado.

O CSV `relacoes-rede-r11.csv` contém os endpoints e a classe de evidência por relação. O JSON preserva os vínculos de cada nó/aresta aos ElementIds e UniqueIds extraídos.
