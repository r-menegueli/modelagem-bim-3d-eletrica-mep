# Candidata R20-23 — Subestação GIS

**Estado:** candidata de estudo BIM demonstrativo; não substitui as revisões anteriores e não é projeto executivo.

## Arquivos principais

- `revit/reconstrucao-r20/19-subestacao-gis-r20.rvt` — modelo nativo Revit 2027.
- `pranchas/reconstrucao-r20/19-subestacao-gis-r20-23-A3.pdf` — 14 folhas A3 para leitura.
- `pranchas/reconstrucao-r20/19-subestacao-gis-r20-23.pdf` — PDF bruto de impressão em Carta, preservado para rastreabilidade da normalização A3.
- `pranchas/reconstrucao-r20/preview/` — prévias PNG das folhas e contato geral.
- `dados/reconstrucao-r20/auditoria-modelo-r20.json` — extração da API do documento aberto no Revit 2027.3.
- `dados/reconstrucao-r20/conectividade-equipamentos-r20.csv` e `quantitativos-modelo.csv` — inventário de conectores/elementos e quantitativos de famílias.
- `dados/reconstrucao-r20/auditoria-grafo-inventario-v1/` — grafo consolidado por equipamento, rotas, sistemas e infraestrutura.
- `dados/reconstrucao-r20/calculos-n1-model-crosswalk-v1/` — base-fonte, 32 cálculos e vínculo de 33 marcas de diagrama aos ElementIds/UniqueIds do modelo. A CLI regenerou memorial, CSV e JSON com hashes idênticos.
- `dados/reconstrucao-r20/comparativo-r8-r20-23.md` — comparação com R8.

## Verificação

- 168 FamilyInstances, 46 equipamentos elétricos, 24 DirectShapes, 8 CableTrays, 25 conectores MEP, 1 sistema elétrico e 1 Wire 2D.
- 12 geometrias de rota verificadas em quatro grupos trifásicos até envelopes de equipamento. Fases, terminais, condutores e continuidade elétrica continuam sem confirmação.
- O sistema auxiliar QAC-01→UPS-01 é lógico; o Wire 2D é dependente da vista. Não há circuito primário 138 kV/13,8 kV nem condutor primário 3D confirmado.
- Os 32 cálculos têm referências de diagrama e modelo; as 33 marcas referenciadas foram resolvidas, sem marcas ausentes/duplicadas. O cenário de transferência N-1 com acoplamento normalmente aberto não atende todas as cargas críticas; a transferência condicional ainda não foi validada operacionalmente.
- A suíte do repositório passou: 146 testes; `compileall` passou. Esses testes verificam código e cálculos, não certificam engenharia.
- A sessão Revit estava aberta na vista `3D-02`, mas o canvas ao vivo estava vazio. As folhas e PNGs foram usadas na revisão visual; a exportação PDF nativa não completou por limitação de renderização/material da instalação.

## Referências do envelope

- GIS: brochura oficial GE Vernova F35 Dual Gas, 145 kV, dimensões de baia publicadas de 800 × 3.700 × 2.500 mm: [PDF](https://www.gevernova.com/grid-solutions/sites/default/files/2026-02/F35-145-kV-Dual-Gas-GIS-EN-33282-202504-LTR-R002_0.pdf).
- Cubículos: catálogo oficial ABB UniGear ZS1; dimensões e correntes dependem da configuração: [PDF](https://library.e.abb.com/public/e63c3b0ceac143cda1f6be64e3647991/UniGear%20ZS1_Catalogue_1VCP000138_RevS_EN.pdf).
- Transformador: DAX TRP-EA40-DML-01 é apenas candidato publicado; não foi selecionado: [dados do fabricante](https://dax.energy/produtos/transformadores-de-potencia).

Consulte [RELATORIO.md](dados/reconstrucao-r20/auditoria-grafo-inventario-v1/RELATORIO.md) para hashes, resultados completos e limitações. A candidata não foi publicada no LinkedIn.
