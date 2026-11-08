from __future__ import annotations

import unittest

from scripts.engineering import gis_connectivity as gc
from scripts.engineering.gis_auxiliary_circuits import PANEL_MARKS


def fixture_elements() -> list[dict[str, str]]:
    marks: set[str] = set()
    for node in gc.NODES:
        if not node.external:
            for pattern in node.marks:
                marks.add(pattern[:-1] + "01" if pattern.endswith("*") else pattern)
    for edge in gc.EDGES:
        for mark in edge.equipment_marks:
            marks.add(mark[:-1] + "01" if mark.endswith("*") else mark)
        for prefix in edge.geometry_prefixes:
            marks.add(prefix + "SEG-01")
    for mark, _ in gc.INFRASTRUCTURE_SPECS:
        marks.add(mark + "-SEG-01")
    return [
        {
            "element_id": str(index + 100),
            "unique_id": f"uid-{index + 100}",
            "marca": mark,
            "categoria_revit": "Electrical Equipment" if mark.startswith(("GIS-", "TR-", "QMT-", "QAC", "UPS")) else "Generic Models",
            "mep_connectors": "1" if mark in {"QAC-01", "UPS-01"} else "0",
            "isconnected_true": "1" if mark in {"QAC-01", "UPS-01"} else "0",
            "dominios": "DomainElectrical" if mark in {"QAC-01", "UPS-01"} else "",
            "model_geometry": "FAMILY_GEOMETRY_ONLY",
        }
        for index, mark in enumerate(sorted(marks))
    ]


def fixture_audit(native_aux: bool = True) -> dict:
    systems = []
    if native_aux:
        rows = fixture_elements()
        qac = next(row for row in rows if row["marca"] == "QAC-01")
        ups = next(row for row in rows if row["marca"] == "UPS-01")
        systems = [{
            "system_id": "AUX-UPS-01",
            "selected_panel_element": {
                "element_id": int(qac["element_id"]),
                "unique_id": qac["unique_id"],
                "mark": "QAC-01",
            },
            "terminal_elements": [{"element_id": int(ups["element_id"]), "unique_id": ups["unique_id"], "mark": "UPS-01"}],
            "three_dimensional_cable_modeled": False,
        }]
    return {"electrical_system_details": systems}


def fixture_auxiliary_audit() -> tuple[list[dict[str, str]], dict]:
    elements = fixture_elements()
    by_mark = {row["marca"]: row for row in elements}
    for mark in ("QAC-01", "UPS-01", *PANEL_MARKS):
        by_mark[mark]["mep_connectors"] = "1"
        by_mark[mark]["isconnected_true"] = "1"
        by_mark[mark]["dominios"] = "DomainElectrical"
    qac = by_mark["QAC-01"]
    systems = fixture_audit()["electrical_system_details"]
    for index, mark in enumerate(PANEL_MARKS, start=1):
        terminal = by_mark[mark]
        systems.append({
            "system_id": f"AUX-AC-{mark}",
            "selected_panel_element": {
                "element_id": int(qac["element_id"]),
                "unique_id": qac["unique_id"],
                "mark": "QAC-01",
            },
            "terminal_elements": [{
                "element_id": int(terminal["element_id"]),
                "unique_id": terminal["unique_id"],
                "mark": mark,
            }],
            "three_dimensional_cable_modeled": False,
            "wire_2d_view_specific_confirmed": False,
            "circuit_number": f"{index:02d}",
        })
    return elements, {"electrical_system_details": systems}


class GISConnectivityGraphTests(unittest.TestCase):
    def test_r11_contains_five_ct_placeholders_including_bus_coupler(self) -> None:
        graph = gc.build_graph(fixture_elements(), fixture_audit())
        ct_nodes = [n for n in graph["nodes"] if n["node_id"].endswith("-CT")]
        self.assertEqual(5, len(ct_nodes))
        coupler = next(n for n in ct_nodes if n["node_id"] == "GIS-BC01-CT")
        self.assertEqual("MODEL_ELEMENT_FOUND", coupler["model_state"])

    def test_connector_counts_do_not_create_power_conductor_claim(self) -> None:
        graph = gc.build_graph(fixture_elements(), fixture_audit())
        self.assertEqual(1, graph["summary"]["native_electrical_system_relations_confirmed"])
        self.assertEqual(0, graph["summary"]["physical_power_conductors_confirmed"])
        self.assertTrue(all(not e["physical_conductor_confirmed"] for e in graph["edges"]))

    def test_auxiliary_relation_is_not_confirmed_without_audited_system_membership(self) -> None:
        graph = gc.build_graph(fixture_elements(), fixture_audit(native_aux=False))
        aux = next(e for e in graph["edges"] if e["edge_id"] == "AUX-QAC-TO-UPS")
        self.assertFalse(aux["native_electrical_system_confirmed"])
        self.assertEqual("NATIVE_SYSTEM_NOT_CONFIRMED", aux["evidence_class"])

    def test_auxiliary_relation_requires_selected_panel_instance_identity(self) -> None:
        audit = fixture_audit()
        audit["electrical_system_details"][0]["selected_panel_element"]["unique_id"] = "wrong-instance"
        graph = gc.build_graph(fixture_elements(), audit)
        self.assertEqual(0, graph["summary"]["native_electrical_system_relations_confirmed"])

    def test_geometric_routes_remain_non_electrical(self) -> None:
        graph = gc.build_graph(fixture_elements(), fixture_audit())
        route = next(e for e in graph["edges"] if e["edge_id"] == "GIS-T01-CT-TO-TR-01-HV")
        self.assertEqual("GEOMETRIC_ROUTE_ONLY", route["evidence_class"])
        self.assertTrue(route["geometry_present"])
        self.assertFalse(route["physical_conductor_confirmed"])

    def test_gis_selector_stubs_are_geometry_only_not_power_connections(self) -> None:
        graph = gc.build_graph(fixture_elements(), fixture_audit())
        for edge_id in ("GIS-L01-A-SELECTOR", "GIS-BC-A-SELECTOR", "GIS-BC-B-SELECTOR"):
            edge = next(e for e in graph["edges"] if e["edge_id"] == edge_id)
            self.assertEqual("GEOMETRIC_ROUTE_ONLY", edge["evidence_class"])
            self.assertTrue(edge["geometry_present"])
            self.assertFalse(edge["native_electrical_system_confirmed"])
            self.assertFalse(edge["physical_conductor_confirmed"])

    def test_expected_nodes_and_edge_endpoints_are_consistent(self) -> None:
        node_ids = {node.node_id for node in gc.NODES}
        self.assertEqual(len(gc.NODES), len(node_ids))
        for edge in gc.EDGES:
            self.assertIn(edge.from_node, node_ids)
            self.assertIn(edge.to_node, node_ids)

    def test_missing_ct_is_reported_as_pending(self) -> None:
        rows = [row for row in fixture_elements() if row["marca"] != "GIS-BC01-CT"]
        graph = gc.build_graph(rows, fixture_audit())
        self.assertIn("GIS-BC01-CT", graph["summary"]["missing_expected_model_nodes"])

    def test_cable_trays_are_infrastructure_only(self) -> None:
        graph = gc.build_graph(fixture_elements(), fixture_audit())
        route = next(row for row in graph["infrastructure"] if row["mark_prefix"] == "LEITO-MV-A")
        self.assertTrue(route["electrical_connection"] is False)
        self.assertEqual("NATIVE_CABLETRAY_INFRASTRUCTURE_ONLY", route["state"])

    def test_r12_auxiliary_circuits_require_audited_equipment_and_connector_identity(self) -> None:
        elements, audit = fixture_auxiliary_audit()
        graph = gc.build_graph(elements, audit, revision="R12")
        self.assertEqual(12, graph["summary"]["native_electrical_system_relations_confirmed"])
        self.assertEqual(0, graph["summary"]["physical_power_conductors_confirmed"])
        for mark in PANEL_MARKS:
            edge = next(e for e in graph["edges"] if e["edge_id"] == f"AUX-QAC-TO-{mark}")
            self.assertTrue(edge["native_electrical_system_confirmed"])
            self.assertEqual("NATIVE_ELECTRICAL_SYSTEM_LOGICAL", edge["evidence_class"])
            self.assertFalse(edge["physical_conductor_confirmed"])

    def test_r12_circuit_with_wrong_selected_panel_instance_is_not_counted(self) -> None:
        elements, audit = fixture_auxiliary_audit()
        next(s for s in audit["electrical_system_details"] if s["system_id"] == "AUX-AC-P-L01")["selected_panel_element"]["unique_id"] = "wrong-panel"
        graph = gc.build_graph(elements, audit, revision="R12")
        self.assertEqual(11, graph["summary"]["native_electrical_system_relations_confirmed"])
        edge = next(e for e in graph["edges"] if e["edge_id"] == "AUX-QAC-TO-P-L01")
        self.assertFalse(edge["native_electrical_system_confirmed"])

    def test_bus_coupler_ct_panel_relation_has_no_assumed_125v_control_voltage(self) -> None:
        edge = next(e for e in gc.EDGES if e.edge_id == "GIS-BC-CT-PROTECTION")
        self.assertIsNone(edge.voltage_kv)


if __name__ == "__main__":
    unittest.main()
