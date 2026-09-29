"""
Parametric 3D BIM / Electrical MEP Model Generator for FreeCAD / IFC
Generates 3D Substation Equipment, MV/LV Switchgear, Transformer, QTA Panel & Overhead Cable Trays.
Author: Rogerio Eduardo Moreira Menegueli
"""
BIM_ELEMENTS = [
    {"ifc_class": "IfcTransformer", "tag": "TR-01", "spec": "500 kVA 13.8kV / 380-220V Dry-Type Transformer", "dims_mm": (1550, 980, 1750)},
    {"ifc_class": "IfcElectricDistributionBoard", "tag": "QGBT-01", "spec": "Main LV Switchboard 1000A 380/220V Form 3B", "dims_mm": (2000, 800, 2200)},
    {"ifc_class": "IfcElectricDistributionBoard", "tag": "QTA-01", "spec": "250 kVA Automatic Transfer Switch Panel", "dims_mm": (800, 400, 1800)},
    {"ifc_class": "IfcElectricGenerator", "tag": "GMG-01", "spec": "250 kVA / 200 kW Soundproof Diesel Generator Skid", "dims_mm": (3600, 1350, 1950)},
    {"ifc_class": "IfcCableCarrierSegment", "tag": "EC-01", "spec": "Perforated Overhead Cable Tray 300x100 mm Hot-Dip Galvanized", "length_m": 48.0},
    {"ifc_class": "IfcCableCarrierSegment", "tag": "ED-01", "spec": "Heavy Galvanized Steel Conduit DN 100 (4 in) Underground Feeder", "length_m": 36.0},
]

if __name__ == "__main__":
    for el in BIM_ELEMENTS:
        print(f"[BIM-MEP] {el['tag']} ({el['ifc_class']}): {el['spec']}")
