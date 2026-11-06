"""Gera um modelo volumétrico simples para estudo em FreeCAD.

As formas são caixas de referência. O script não cria objetos IFC nem valida
instalações, folgas, interferências ou quantitativos de obra.
"""

from pathlib import Path
import csv

import FreeCAD
import Part


try:
    ROOT = Path(__file__).resolve().parents[1]
except NameError:
    # Também permite executar o arquivo pela console do FreeCAD com a raiz do
    # repositório definida como diretório de trabalho.
    ROOT = Path.cwd()

MODEL_DIR = ROOT / "models_3d"
SCHEDULE = ROOT / "qto_schedules" / "BIM_Electrical_QTO_LOD350.csv"
MODEL_PATH = MODEL_DIR / "Subestacao_QGBT_CCM_Gerador_BIM_MEP.FCStd"

ITEMS = (
    ("TR-01", "TR01_Transformer_500kVA_13k8_380V", 1550, 980, 1750, (0, 0, 0)),
    ("QGBT-01", "QGBT01_Main_LV_Switchboard_1000A", 2000, 800, 2200, (2800, 0, 0)),
    ("QTA-01", "QTA01_ATS_Panel_250kVA", 800, 600, 2200, (5000, 0, 0)),
    ("CCM-01", "CCM01_Motor_Control_Center_6Col", 2400, 600, 2200, (6200, 0, 0)),
    ("GMG-01", "GMG01_Diesel_Generator_250kVA", 3600, 1350, 1950, (0, 3500, 0)),
    ("EC-300", "EC300_Overhead_Cable_Tray_300x100", 9000, 300, 100, (0, -200, 3800)),
)


def write_schedule():
    SCHEDULE.parent.mkdir(parents=True, exist_ok=True)
    with SCHEDULE.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(
            ("Tag", "FreeCAD_Object", "Envelope_mm", "Quantity", "Unit", "Box_Volume_m3", "Status")
        )
        for tag, name, length, width, height, _position in ITEMS:
            volume = length * width * height / 1_000_000_000
            writer.writerow(
                (tag, name, f"{length}x{width}x{height}", 1, "ea", f"{volume:.3f}", "placeholder box")
            )


def main():
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    doc = FreeCAD.newDocument("Subestacao_MEP_Volumetric_Study")

    for _tag, name, length, width, height, position in ITEMS:
        obj = doc.addObject("Part::Feature", name)
        shape = Part.makeBox(length, width, height)
        shape.Placement.Base = FreeCAD.Vector(*position)
        obj.Shape = shape

    doc.recompute()
    doc.saveAs(str(MODEL_PATH))
    write_schedule()
    print(f"SAVED_FCSTD: {MODEL_PATH}")
    print(f"SAVED_SCHEDULE: {SCHEDULE}")


if __name__ == "__main__" or "__file__" not in globals():
    main()
