"""Keep names in the existing FreeCAD archive aligned with the source model.

This only rewrites object identifiers and BREP entry names; it does not modify
geometry. Reopen the result in FreeCAD when that application is available.
"""

from __future__ import annotations

from pathlib import Path
import os
from zipfile import ZipFile, ZipInfo


ROOT = Path(__file__).resolve().parents[1]
MODEL = ROOT / "models_3d" / "Subestacao_QGBT_CCM_Gerador_BIM_MEP.FCStd"
REPLACEMENTS = {
    "TR01_Transformer_500kVA_13k8_380V": "TR01_Transformer_500kVA_13k8_380V_Oil",
    "QGBT01_Main_LV_Switchboard_1000A": "QGBT01_Main_LV_Switchboard_800A_380V",
}


def main() -> None:
    temporary = MODEL.with_name(MODEL.name + ".tmp")
    with ZipFile(MODEL, "r") as source, ZipFile(temporary, "w") as target:
        for original in source.infolist():
            name = original.filename
            data = source.read(original.filename)
            for old, new in REPLACEMENTS.items():
                name = name.replace(old, new)
                if original.filename == "Document.xml":
                    data = data.replace(old.encode("ascii"), new.encode("ascii"))
            info = ZipInfo(name, original.date_time)
            info.compress_type = original.compress_type
            info.external_attr = original.external_attr
            target.writestr(info, data)
    os.replace(temporary, MODEL)
    print(f"Updated native labels in {MODEL.relative_to(ROOT)}; geometry unchanged.")


if __name__ == "__main__":
    main()
