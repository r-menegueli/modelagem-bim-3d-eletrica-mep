# Relatório de conectividade por equipamento — R20

A extração lê o RVT gerado no Revit. `IsConnected` e `AllRefs` são registrados separadamente; os elementos DirectShape, as famílias de equipamento e os sistemas elétricos não são tratados como equivalentes.

## Estado de execução

- A rede principal 138/13,8 kV não é uma rede elétrica conectada pela API MEP do Revit. Os equipamentos HV/MV não recebem conectores `PowerCircuit`.
- `AUX-UPS-01` tem um Wire nativo Revit com os dois conectores confirmados no plano E-06 e representação view-specific. Em ambos os casos não existe cabo 3D, dimensionamento do condutor ou prova de alimentação completa.
- Foram tentados 11 circuitos lógicos QAC-01 → aquecedores auxiliares associados aos painéis; 0 tiveram tipo `PowerCircuit`, painel, terminal, `LoadName` e valores de tensão/carga/potência aparente/FP/corrente confirmados pela API. `LoadName` identifica a finalidade no sistema lógico; `CircuitNumber` permanece o número nativo atribuído pelo Revit. Cargas CA de aquecedores são hipóteses de estudo derivadas da R8 AUX-AC-06 e do sistema 380Y/220 V. Cada painel também expõe interface `Controls` de 125 Vcc, ainda sem circuito CC. Os sistemas CA confirmados são lógicos, sem cabo 3D e sem Wire 2D.
- As rotas de potência HV/MV são DirectShape com centros geométricos separados, não condutores, cabos, barramentos selecionados ou ligações MEP. O espaçamento e a atribuição de fases são ilustrativos. As aberturas de parede são estimadas e a auditoria lista os elementos/caixas geométricas encontrados; 4 aberturas são esperadas, mas isso não comprova vedação ou rating corta-fogo.
- Foram extraídos 8 segmentos nativos CableTray, com 8 de 16 conectores marcados IsConnected. Essas juntas ligam infraestrutura a infraestrutura; não conectam os leitos aos equipamentos/circuitos.

## Relações da topologia

O CSV `relacoes-rede-r20.csv` lista as relações de cada etapa, separando conexão elétrica no RVT, geometria/infrastructure, relação mostrada em E-01 e pendências.

O CSV `conectividade-equipamentos-r20.csv` tem uma linha para cada FamilyInstance, DirectShape de rota, trecho CableTray e Wire, com ElementId, marca, contagem de conectores, `IsConnected`, referências externas e relação declarada.

## Pendências principais

1. Condutores primários, contatos internos GIS, ligação física aos terminais dos transformadores, condutores/barramento MT e saídas dos dez alimentadores não estão implementados como circuito ou cabo no RVT.
2. As juntas internas de leito só são consideradas conectadas quando `ConnectTo`/`IsConnected` foram confirmados; terminais externos continuam abertos, e não há ocupação de cabos nem conexão dos leitos aos equipamentos.
3. A relação lógica auxiliar não inclui circuitos QCC/bateria, saída CC da UPS ou cargas finais.
