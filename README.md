# Estudo volumétrico de equipamentos elétricos em FreeCAD

Este repositório contém um estudo acadêmico de arranjo espacial. O modelo nativo representa cinco equipamentos e um trecho de eletrocalha por caixas simples, para visualizar posições e envelopes aproximados.

## Arquivos

- [Modelo FreeCAD](models_3d/Subestacao_QGBT_CCM_Gerador_BIM_MEP.FCStd)
- [Script de geração](scripts/generate_bim_mep_model.py)
- [Tabela de volumes das caixas](qto_schedules/BIM_Electrical_QTO_LOD350.csv)

O nome da tabela é legado. Seu conteúdo registra apenas volumes geométricos dos blocos; não é um quantitativo de instalações, um modelo IFC ou uma entrega LOD 350.

## Gerar o modelo

Abra a pasta do repositório como diretório de trabalho no FreeCAD e execute no console Python:

```python
exec(open("scripts/generate_bim_mep_model.py", encoding="utf-8").read())
```

O script salva o modelo em `models_3d/` e atualiza a tabela em `qto_schedules/`.

## Limites do estudo

As caixas não representam famílias ou componentes detalhados. O repositório não contém um modelo coordenado de edifício, sistemas MEP conectados, exportação IFC, detecção de interferências, cálculo elétrico ou quantitativo executivo. Não use os arquivos para construção, compra ou instalação.
