# Biblioteca de cálculos elétricos

O pacote `scripts.engineering` reúne primitivas reutilizáveis com unidades explícitas, validação de entradas e proveniência. Os módulos por disciplina são a interface recomendada; `electrical_core.py` mantém as funções históricas usadas nos estudos existentes.

## Módulos

- `power.py`: triângulo de potência, correntes, motor e compensação de reativo.
- `distribution.py`: agregação de cargas com fator de demanda explícito e carregamento de transformador.
- `conductors.py`: queda de tensão/perdas aproximadas, correções sobre ampacidade de referência fornecida e suportabilidade adiabática `I²t` com `k` explícito.
- `protection.py`: curto trifásico simétrico por impedâncias R/X em ohms por fase e comparação numérica preliminar de capacidade de interrupção.
- `motors.py`: consumo por agenda de operação e estimativa em regime permanente da entrada de acionamentos.
- `energy.py`: soma de energia por períodos, fechamento de balanço e eficiência na fronteira declarada.
- `generation.py`: cenários de energia fotovoltaica e hidráulica.
- `storage.py`: energia e autonomia idealizada de baterias.
- `essential_services.py`: estimativas preliminares de gerador, UPS e capacidade Ah para uma missão de bateria.
- `automation.py`: mapa lógico validado de sinais I/O e relações entre equipamentos.
- `gis_substation_study.py`: estudo N-1 específico da subestação GIS 138/13,8 kV, com entradas versionadas e itens não calculados explicitados.

## Exemplo de levantamento de cargas

```python
from scripts.engineering.distribution import DemandLoad, aggregate_demand_schedule

schedule = aggregate_demand_schedule(loads=(
    DemandLoad("MCC-01", 100.0, 0.80, 0.50, "lagging"),
    DemandLoad("LGT-01", 40.0, 1.00, 0.50, "leading"),
))
```

Os argumentos de cada carga estão em kW; o fator de demanda deve ser informado em `[0, 1]`. Fator de potência (fração entre zero e um) e sentido reativo (`lagging` ou `leading`) são um par opcional. Se ambos forem omitidos, a biblioteca calcula somente P e deixa Q/S desconhecidos; ela nunca assume FP ou sentido reativo. A agregação supõe que os fatores fornecidos já representam a simultaneidade pertinente. Ela não deduz fase, topologia ou regime.

## Condutores e proteção

`ampacity_after_corrections` apenas multiplica uma ampacidade de referência fornecida pelos fatores nomeados, cada qual com fonte ou hipótese. A biblioteca não seleciona cabo nem determina a ampacidade de referência. `adiabatic_conductor_withstand` exige `k`, seção e duração explícitos e não escolhe valores normativos de `k`.

`compare_interrupting_rating` só compara correntes quando os rótulos de base coincidem, e também compara tensão nominal do barramento com a tensão nominal informada do dispositivo. Um resultado favorável é apenas uma triagem numérica; não demonstra conformidade normativa, capacidade de fechamento, assimetria, duty, seletividade ou coordenação. A biblioteca não contém curvas de fabricantes ou parâmetros de proteção validados.

## Motores, energia, serviços essenciais e automação

`motor_energy_by_schedule` calcula kWh a partir de frações carregadas e horas declaradas. Mantém eficiência e fator de potência constantes em cada período; períodos desligados devem ser omitidos, e operação sem carga deve vir de potência medida porque o método não modela perdas em vazio. Partidas e mudanças de eficiência em carga parcial ficam fora do método. `drive_input_estimate` usa eficiências e FP total de entrada informados, sem modelar harmônicos ou selecionar um inversor.

`energy_balance` recebe cada parcela de energia e calcula o resíduo sem inventar um termo de fechamento. Um resíduo diferente de zero aponta fronteira incompleta ou inconsistência. `energy_efficiency` conserva e sinaliza resultados acima de 100% para revisão da fronteira, sem corrigi-los silenciosamente.

`generator_capacity_estimate` e `ups_capacity_estimate` calculam kVA de regime permanente com FP, reserva, derating e limite de carregamento informados. Não verificam partida de motores, degraus de carga, altitude, temperatura do fabricante ou autonomia. `battery_mission_capacity` soma carga contínua e eventos de potência constante à tensão nominal; fatores de retenção por envelhecimento/temperatura, DoD e eficiência reduzem a fração utilizável e ficam no denominador. Não substitui curvas de descarga, queda de tensão, projeto de strings, carregador ou especificação do fabricante.

`build_control_architecture` valida IDs, direção AI/AO/DI/DO e limites de sinais analógicos. O resultado marca cada ponto como relação lógica documentada; não comprova fiação, lógica de PLC, intertravamentos, rede ou protocolo.

O mapa de automação aceita entrada JSON e grava um relatório validado:

```powershell
python -m scripts.engineering.automation --inputs scripts/engineering/examples/control-architecture.example.json --output <diretorio-de-saida>/control-architecture-results.json
```

## Estudo N-1 da subestação GIS

O estudo de projeto usa `base-de-projeto.json` e gera 32 resultados em JSON/CSV e um memorial. Cada linha associa `diagram_refs` (tag do unifilar/circuito) a `model_mark_refs` (marcas Revit), por exemplo `A-F01` → `QMT-A-F01`. Para um modelo extraído, `--model-quantities` acrescenta uma auditoria reprodutível `Mark` → `ElementId`/`UniqueId`, detecta marcas ausentes ou duplicadas e grava o hash do CSV fonte. O `resultados.csv` passa a trazer `model_element_ids`, `model_unique_ids` e o estado de resolução em cada cálculo. Essa checagem confirma identidade do elemento, não conectividade elétrica.

```powershell
python -m scripts.engineering.gis_substation_study --inputs projetos/19-subestacao-gis/attempts/r20-23-candidate/dados/reconstrucao-r20/calculos-n1-model-crosswalk-v1/base-de-projeto-fonte.json --output <diretorio-de-saida> --model-quantities projetos/19-subestacao-gis/attempts/r20-23-candidate/dados/reconstrucao-r20/quantitativos-modelo.csv
```

O leitor rejeita booleanos, textos, valores não finitos, campos numéricos ausentes e identificadores de estudo/fonte vazios. Sem `--model-quantities`, o resultado marca a auditoria de identidade como `NOT_VERIFIED` em vez de presumir correspondência.

`calculate()` também valida tipos e consistência da entrada de projeto antes dos cálculos: números JSON não aceitam coerção de texto/booleano, fatores de potência precisam estar em `(0, 1]`, marcas/tag de alimentador devem ser únicas, os estados `critical` devem ser booleanos e as posições declaradas devem corresponder aos alimentadores listados. Resultados derivados não finitos são rejeitados antes da exportação.

## Inventário Revit usado pela auditoria de conectividade

O arquivo `quantitativos-modelo.csv` contém a tabela de famílias/quantitativos e não deve ser tratado sozinho como inventário completo de geometrias. `conectividade-equipamentos-<revisao>.csv` registra famílias e, quando extraídos, DirectShape, CableTray e Wire. A auditoria também reconcilia as identidades das listas específicas no JSON do Revit para rotas geométricas e infraestrutura. A correspondência exige ElementId/UniqueId; uma rota aceita continua sendo apenas geometria que toca envelopes de famílias, sem comprovar porta, cabo, contato, fase elétrica ou `ElectricalSystem`.

CableTray é contado como infraestrutura pela classe/categoria do elemento, não por sua marca ou quantidade de conectores. Conectores conectados de um leito indicam somente juntas entre infraestrutura; não significam conexão de potência com equipamento. `quantitativos-modelo.csv` pode ser usado separadamente por `gis_substation_study` para vincular cálculos a marcas de equipamentos.

## Rastreabilidade

Cada resultado numérico carrega `source_or_assumption` e `status` via `Provenance`. Use `identified_calculation_record` para anexar ID, entradas estruturadas, método, unidade e limitações ao registro exportado.

Os cálculos existentes continuam sendo estudos demonstrativos. Valores faltantes não devem ser substituídos por padrões silenciosos. As funções não constituem dimensionamento executivo, verificação normativa nem aprovação de instalação.

## CLI e formato JSON

O arquivo `examples/demand-schedule.example.json` demonstra campos obrigatórios, unidades e fontes por carga. Ele é explicitamente sintético. Execute a partir da raiz do repositório:

```powershell
python -m scripts.engineering.distribution --inputs scripts/engineering/examples/demand-schedule.example.json --output <diretorio-de-saida>
```

O comando grava `demand-schedule-results.json` com `calc_id`, entradas, método, resultado, unidade, fonte, status e limitações, além de `demand-schedule-lines.csv` com uma linha por equipamento.
