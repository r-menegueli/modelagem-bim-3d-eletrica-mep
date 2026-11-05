"""Create a FreeCAD volumetric massing mockup and its box-volume schedule.

Run with FreeCAD's Python environment. The exported Part::Feature boxes are
conceptual placeholders; this script does not create IFC objects or run clashes.
"""

from __future__ import annotations

import csv
from pathlib import Path

import FreeCAD
import Part


ROOT = Path(__file__).resolve().parents[1]
MODEL_DIR = ROOT / "models_3d"
SCHEDULE = ROOT / "qto_schedules" / "volumetric_placeholders.csv"
MODEL_PATH = MODEL_DIR / "Subestacao_QGBT_CCM_Gerador_BIM_MEP.FCStd"

ITEMS = (
    ("TR-01", "TR01_Transformer_500kVA_13k8_380V_Oil", 1550, 980, 1750, (0, 0, 0), "Envelope placeholder; type and foundation are not detailed."),
    ("QGBT-01", "QGBT01_Main_LV_Switchboard_800A_380V", 2000, 800, 2200, (2800, 0, 0), "Envelope placeholder; compartment geometry is not modeled."),
    ("QTA-01", "QTA01_ATS_Panel_250kVA", 800, 600, 2200, (5000, 0, 0), "Envelope placeholder; internal equipment is not modeled."),
    ("CCM-01", "CCM01_Motor_Control_Center_6Col", 2400, 600, 2200, (6200, 0, 0), "Envelope placeholder; six columns are not separated."),
    ("GMG-01", "GMG01_Diesel_Generator_250kVA", 3600, 1350, 1950, (0, 3500, 0), "Envelope placeholder; access and service clearances are not checked."),
    ("EC-300", "EC300_Overhead_Cable_Tray_300x100", 9000, 300, 100, (0, -200, 3800), "One straight box; no fittings, supports, routing, or cut-length QTO."),
)


def write_schedule() -> None:
    SCHEDULE.parent.mkdir(parents=True, exist_ok=True)
    with SCHEDULE.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(
            ("Tag", "FreeCAD_Object", "Envelope_mm", "Quantity", "Unit", "Box_Volume_m3", "Status", "Notes")
        )
        for tag, name, length, width, height, _position, note in ITEMS:
            volume = length * width * height / 1_000_000_000
            writer.writerow(
                (tag, name, f"{length}x{width}x{height}", 1, "ea", f"{volume:.3f}", "placeholder box", note)
            )


def main() -> None:
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    doc = FreeCAD.newDocument("Subestacao_MEP_Volumetric_Mockup")
    for tag, name, length, width, height, position, _note in ITEMS:
        obj = doc.addObject("Part::Feature", name)
        shape = Part.makeBox(length, width, height)
        shape.Placement.Base = FreeCAD.Vector(*position)
        obj.Shape = shape
        obj.addProperty("App::PropertyString", "BIMTag", "Conceptual")
        obj.BIMTag = tag
        obj.addProperty("App::PropertyString", "ModelStatus", "Conceptual")
        obj.ModelStatus = "Volumetric placeholder; not an IFC element"

    doc.recompute()
    doc.saveAs(str(MODEL_PATH))
    write_schedule()
    print(f"SAVED_FCSTD: {MODEL_PATH} ({MODEL_PATH.stat().st_size} bytes)")
    print(f"SAVED_SCHEDULE: {SCHEDULE}")


if __name__ == "__main__":
    main()
