from __future__ import annotations

import unittest

from scripts.engineering import gis_connectivity as gc


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
            "isconnected_true": "0",
            "model_geometry": "FAMILY_GEOMETRY_ONLY",
        }
        for index, mark in enumerate(sorted(marks))
    ]


def fixture_audit(native_aux: bool = True) -> dict:
    systems = []
    if native_aux:
        qac = next(row for row in fixture_elements() if row["marca"] == "QAC-01")
        systems = [{
            "system_id": "AUX-UPS-01",
            "selected_panel_element": {
                "element_id": int(qac["element_id"]),
                "unique_id": qac["unique_id"],
                "mark": "QAC-01",
            },
            "terminal_elements": [{"mark": "UPS-01"}],
            "three_dimensional_cable_modeled": False,
        }]
    return {"electrical_system_details": systems}


class GISConnectivityGraphTests(unittest.TestCase):
    def test_r10_contains_five_ct_placeholders_including_bus_coupler(self) -> None:
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


if __name__ == "__main__":
    unittest.main()
