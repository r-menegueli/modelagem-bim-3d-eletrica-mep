# Base de projeto — Subestação GIS

Revisão 0 — 2026-11-07. Base conceitual para modelagem e estudos de portfólio; valores sem confirmação do usuário, concessionária, campo ou placa são premissas.

## Topologia de estudo

- Sistema nominal: 138 kV / 13,8 kV, trifásico, 60 Hz (frequência a confirmar com os dados de conexão).
- GIS de classe de equipamento até 145 kV, arranjo de barras duplas com um bay de acoplamento. Balanço de bays: duas linhas de entrada, duas saídas para transformadores e um acoplamento. Cada bay e cada polo/compartimento que seja distinguível no arranjo terá identificação própria.
- Dois transformadores 40 MVA, um por seção de 13,8 kV. Não se assume paralelo permanente.
- Barramento de 13,8 kV em seções A/B, cada uma alimentada por um transformador. Acoplador normalmente aberto; a transferência de cargas críticas requer isolar a unidade indisponível e confirmar os intertravamentos. Não fechar o acoplador com as duas fontes em paralelo antes de conferir sequência de fases, impedância, curto-circuito, proteção e procedimento da concessionária.
- Cenário de carga demonstrativo: 56 MW totais, FP 0,95; 32 MW críticos e 24 MW não críticos. A distribuição será simétrica no estudo, com cinco alimentadores por seção: 16 MW críticos e 12 MW não críticos em cada seção. São grupos de carga assumidos, não clientes ou cargas medidas.
- N-1: indisponibilidade de um transformador requer rejeição dos 24 MW não críticos; os 32 MW críticos ficam abaixo dos 40 MVA da unidade remanescente na premissa adotada.

## Cálculos de enquadramento

Base equilibrada trifásica e tensões linha-linha. MVA/kV resulta em kA.

| ID | Grandeza | Entrada e método | Resultado de estudo | Estado |
|---|---|---|---:|---|
| BP-01 | Potência aparente total | 56 MW / 0,95 | 58,947 MVA | Calculável com premissas |
| BP-02 | Reativo total | `sqrt(S² - P²)` | 18,406 MVAr | Calculável com premissas |
| BP-03 | Corrente total em 138 kV | `S / (sqrt(3) × 138 kV)` | 0,247 kA | Calculável com premissas |
| BP-04 | Corrente total em 13,8 kV | `S / (sqrt(3) × 13,8 kV)` | 2,466 kA | Calculável com premissas |
| BP-05 | Corrente nominal de cada transformador, lado 138 kV | `40 MVA / (sqrt(3) × 138 kV)` | 0,167 kA | Calculável com premissas |
| BP-06 | Corrente nominal de cada transformador, lado 13,8 kV | `40 MVA / (sqrt(3) × 13,8 kV)` | 1,673 kA | Calculável com premissas |
| BP-07 | Carregamento agregado normal | `58,947 / (2 × 40) × 100` | 73,7% | Pressupõe partilha equilibrada |
| BP-08 | Carga crítica em N-1 | `32 MW / 0,95` e comparação com 40 MVA | 33,684 MVA; 84,2% | Cabe na potência nominal teórica |
| BP-09 | Carga total em N-1 | `58,947 / 40 × 100` | 147,4% | Não cabe; rejeitar os 24 MW não críticos |
| BP-10 | Compensação até FP 0,98 | Fórmula de tangentes aplicada a 56 MW | 7,035 MVAr | Estimativa; não seleciona banco |
| BP-11 | Curto simplificado por transformador | In de 40 MVA / Z=10% | 16,735 kA em 13,8 kV | Cenário; fonte ideal, Z presumida |
| BP-12 | Dois transformadores em paralelo | Soma das contribuições BP-11 | 33,470 kA em 13,8 kV | Cenário de sensibilidade, não regime normal |

A impedância de 10% é apenas um valor provisório para sensibilidade; substituir pela impedância de placa, tolerância, tap e relação real. BP-11/12 não incluem rede, cabos, motores, razão X/R, assimetria nem decaimento. Não usar para especificar capacidade interruptiva ou ajustes.

## Referências de faixa e dimensões

As fontes abaixo demonstram que há equipamentos e arranjos nessa classe; não escolhem fabricante nem comprovam que uma unidade específica atende ao projeto.

- [GE Vernova — GIS de 145 kV, 40 kA e 3.150 A](https://www.gevernova.com/electrification/node/24000), folheto técnico disponibilizado na página do produto. A corrente de curto admissível precisa ser confrontada com o estudo da concessionária antes de qualquer seleção.
- [GE Vernova — brochura GIS 72,5–800 kV](https://www.gevernova.com/grid-solutions/sites/default/files/resources/products/brochures/primaryequip/gis_72_800kv_xdge_en_web.pdf), inclui exemplos de barramento simples, duplo e bay de acoplamento. É referência de arquitetura; desenhos e dimensões específicos não são adotados como projeto.
- [Siemens Energy — Blue GIS 8VN1 145 kV](https://assets.siemens-energy.com/dam/88bb59e4-f5d2-4d57-aefa-b0840170f33f/2020-08--Siemens-Energy-Blue-portfolio-customer-presentation-EN-DAM-version-pdf_Original%20file.pdf), exemplo de solução 145 kV com ratings próprios. Demonstra que os ratings dependem do produto; não é especificação escolhida.
- [WEG — transformador 138/13,8 kV](https://www.weg.net/catalog/weg/BR/en/Generation%2CTransmission-and-Distribution/Oil-type-Reactors-and-Transformers/Oil-Type-Power-Transformer/3-up-to-50-MVA/Oil-Transformer-10000-12500-kVA-138-0-13-8kV-CDC-ONAF/p/14149426), página oficial informa que as unidades são customizadas, documenta classes de tensão e lista ratings usuais incluindo 40/50 MVA. Não fornece dimensões de uma unidade 40 MVA específica.
- [GE Vernova — cubículos M-Tier 3,6–17,5 kV](https://www.gevernova.com/power-conversion/sites/default/files/2025-05/GEA35559_M-Tier-MV-Switchgear-3.6kV-17.5kV-air-insulated-system_EN_20250905.pdf), exemplo público de opções de corrente, incluindo 2.000 A. A corrente nominal de cada transformador é 1,673 kA em 13,8 kV; 2.000 A é somente candidato inicial de continuidade, sujeito a regime, margem, temperatura, agrupamento, curto e fabricante.
- [IEC 62271-200:2021+AMD1:2024](https://webstore.iec.ch/en/publication/63466), referência para conjuntos CA metal-enclosed acima de 1 kV até 52 kV, até 60 Hz. Confirmar requisitos ABNT, concessionária e revisão aplicável antes de alegar conformidade.

Geometria BIM: famílias genéricas separadas por equipamento, com parâmetros `FonteGeometria`, `EstadoGeometria` e `RevisaoDados`. Até haver desenhos gerais/catálogos de um modelo selecionado, cada dimensão será marcada `ESTIMATIVA`; não usar uma imagem de produto como se fosse desenho dimensional. Tanques, buchas, radiadores, ventiladores, conservador, terminais, bases, bandejas e acessos serão componentes genéricos configuráveis, e acessórios que dependam da especificação ficarão condicionais.

## Pendências que impedem seleção final

- Dados de curto-circuito e equivalente da rede de 138 kV, X/R, proteção e critérios de operação da concessionária.
- Perfil e simultaneidade das cargas, tensões mínimas/máximas e critério de FP.
- Placas/datasheets de transformadores: impedância, grupo vetorial, taps, perdas, níveis de isolamento, acessórios e dimensões gerais.
- Correntes de falta, classe interna de arco, CT/VT, tempos, intertravamentos e procedimentos de manobra.
- Lista de cargas por alimentador, demanda, comprimentos e método de instalação para cabos.
- Cargas auxiliares, missão de bateria/UPS e autonomia requerida.
- Resistividade do solo, corrente/tempo de falta à terra, geometria final e estudo de tensão de toque/passo.
- Levantamento civil/geotécnico, ambiente, drenagem, óleo, incêndio, ventilação, acesso e afastamentos regulamentares.

As pranchas permanecem estudo conceitual de portfólio até essas informações serem obtidas e verificadas por responsável técnico. Não usar para obra, compra, aprovação ou instalação.
