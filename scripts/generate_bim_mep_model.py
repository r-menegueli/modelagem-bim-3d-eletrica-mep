import os
import FreeCAD
import Part

out_dir = r'C:\Users\dakra\Documents\Codex\modelagem-bim-3d-eletrica-mep\models_3d'
os.makedirs(out_dir, exist_ok=True)
doc = FreeCAD.newDocument('Subestacao_BIM_MEP_LOD350')

items = [
    ('TR01_Transformer_500kVA_13k8_380V', 1550, 980, 1750, (0, 0, 0)),
    ('QGBT01_Main_LV_Switchboard_1000A', 2000, 800, 2200, (2800, 0, 0)),
    ('QTA01_ATS_Panel_250kVA', 800, 600, 2200, (5000, 0, 0)),
    ('CCM01_Motor_Control_Center_6Col', 2400, 600, 2200, (6200, 0, 0)),
    ('GMG01_Diesel_Generator_250kVA', 3600, 1350, 1950, (0, 3500, 0)),
    ('EC300_Overhead_Cable_Tray_300x100', 9000, 300, 100, (0, -200, 3800)),
]

for name, L, W, H, pos in items:
    obj = doc.addObject('Part::Feature', name)
    box = Part.makeBox(L, W, H)
    box.Placement.Base = FreeCAD.Vector(*pos)
    obj.Shape = box

doc.recompute()
fcstd_path = os.path.join(out_dir, 'Subestacao_QGBT_CCM_Gerador_BIM_MEP.FCStd')
doc.saveAs(fcstd_path)
print('SAVED_FCSTD:', fcstd_path, os.path.getsize(fcstd_path))
