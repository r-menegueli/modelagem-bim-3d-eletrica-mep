# Auditoria de conectividade por equipamento — R20

Fonte: extração do RVT pelo add-in Revit 2027 e grafo topológico independente. ElementId e UniqueId são extraídos do modelo; IDs sem correspondência são listados como pendência.

## Resultado

- Nós esperados: 73; relações: 70.
- Classes de relação: `DIAGRAM_ONLY`: 44; `GEOMETRIC_ROUTE_ONLY`: 14; `NATIVE_ELECTRICAL_SYSTEM_LOGICAL`: 1; `NATIVE_SYSTEM_NOT_CONFIRMED`: 11.
- ElectricalSystem nativo confirmado: 1 relações auxiliares auditadas.
- Cálculo de carga confirmado nessas relações: 0; Wire 2D view-specific confirmado: 1.
- Geometrias 3D de condutor verificadas: 0; estes dados não confirmam circuito elétrico.
- Rotas geométricas por faixa A/B/C verificadas contra envelopes de equipamentos: 12; grupos trifásicos completos: 4. A atribuição da fase elétrica não foi verificada.
- Auditoria de rotas de fase: API_GEOMETRY_VERIFIED; elementos observados/aceitos/rejeitados: 12/12/0.
- Auditoria geométrica de condutores 3D: NO_3D_CONDUCTOR_RECORDS; registros observados/aceitos/rejeitados: 0/0/0.
- Placeholders de TC: 5/5.
- Nós BIM esperados ausentes: 0; nós parciais/ambíguos: 0; rotas geométricas faltantes: 0.
- Marcas/IDs/SystemIds duplicados: 0/0/0/0.

## Classificação

`NATIVE_ELECTRICAL_SYSTEM_LOGICAL` registra somente associação elétrica nativa confirmada pela lista de terminais e por identidades únicas. `physical_conductor_confirmed` exige elementos de condutor 3D identificados e terminais BIM inequívocos; mesmo isso não cria circuito. `geometric_phase_route_verified` confirma somente uma DirectShape por faixa geométrica declarada, seus sólidos, percurso e distância aos envelopes de famílias exatas; A/B/C são rótulos geométricos, sem atribuição da fase elétrica, porta ou terminal. Isso não comprova cabo, barramento ou conexão MEP. `GEOMETRIC_ROUTE_ONLY` indica uma carcaça/percurso sem ligação elétrica. `DIAGRAM_ONLY` indica topologia documentada sem conexão MEP. Leitos CableTray permanecem `INFRASTRUCTURE_ONLY`.

## Pendências

- A topologia primária GIS/13,8 kV está no E-01 e no grafo; condutor geométrico e ElectricalSystem nativo são evidências separadas. Esta auditoria não comprova continuidade elétrica, contatos internos ou PowerCircuits para a rede primária.
- Envelopes DirectShape e leitos CableTray não são condutores; a continuidade entre conectores de leito comprova apenas juntas de infraestrutura.
- Estados de disjuntores e seccionadoras são premissas; não há simulação de manobra nem lógica de intertravamento.
- Cada família GIS_CT é um placeholder genérico; não comprova conjunto trifásico, relação, núcleo, burden ou zona de proteção.
- Sistemas auxiliares QAC-01→UPS/painéis só são contados quando sistema, painel, terminais e conectores são confirmados por ElementId/UniqueId; não provam cabo 3D. O Wire 2D existente é específico da vista E-06.
- As conexões com as duas redes externas e os dez destinos remotos dos alimentadores ficam fora do limite modelado.

O CSV `relacoes-rede-r20.csv` contém os endpoints e a classe de evidência por relação. O JSON preserva os vínculos de cada nó/aresta aos ElementIds e UniqueIds extraídos e identifica marcas ausentes, duplicadas ou parciais.
