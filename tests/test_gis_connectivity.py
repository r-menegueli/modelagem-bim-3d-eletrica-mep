from __future__ import annotations

import csv
import json
import unittest
from pathlib import Path

from scripts.engineering import gis_connectivity as gc
from scripts.engineering.gis_auxiliary_circuits import PANEL_MARKS


ROOT = Path(__file__).resolve().parents[1]


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
            marks.add(prefix if prefix.endswith("-ROUTE") else prefix + "SEG-01")
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
            "revit_class": "ElectricalSystem",
            "system_type": "PowerCircuit",
            "native_membership_confirmed": True,
            "panel_assignment_confirmed": True,
            "terminal_membership_confirmed": True,
            "load_name_confirmed": True,
            "load_calculation_confirmed": False,
            "wire_2d_view_specific_confirmed": False,
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
            "revit_class": "ElectricalSystem",
            "system_type": "PowerCircuit",
            "native_membership_confirmed": True,
            "panel_assignment_confirmed": True,
            "terminal_membership_confirmed": True,
            "load_name_confirmed": True,
            "load_calculation_confirmed": True,
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


def fixture_physical_conductor_audit(phases: str = "ABC") -> tuple[list[dict[str, str]], dict]:
    elements = fixture_elements()
    rows_by_mark = {row["marca"]: row for row in elements}
    details = []
    for index, phase in enumerate(phases, start=1):
        conductor_mark = f"COND-GIS-TR01-HV-PH-{phase}-SEG-01"
        conductor = {
            "element_id": str(9000 + index),
            "unique_id": f"uid-conductor-{phase}",
            "marca": conductor_mark,
            "categoria_revit": "Modelos genéricos",
            "model_geometry": "CONDUCTOR_GEOMETRY_3D",
        }
        elements.append(conductor)
        from_terminal = rows_by_mark["GIS-T01-CT"]
        to_terminal = rows_by_mark[f"TR-01-B-HV-{index}"]
        details.append({
            "edge_id": "GIS-T01-CT-TO-TR-01-HV",
            "phase": phase,
            "evidence_source": "REVIT_API_GEOMETRY_AUDIT",
            "representation_role": "CONDUCTOR_3D_SOLID",
            "element_id": conductor["element_id"],
            "unique_id": conductor["unique_id"],
            "mark": conductor_mark,
            "solid_volume_m3": 0.0005,
            "from_terminal_element_id": from_terminal["element_id"],
            "from_terminal_unique_id": from_terminal["unique_id"],
            "from_terminal_mark": from_terminal["marca"],
            "to_terminal_element_id": to_terminal["element_id"],
            "to_terminal_unique_id": to_terminal["unique_id"],
            "to_terminal_mark": to_terminal["marca"],
            "from_endpoint_gap_mm": 0.0,
            "to_endpoint_gap_mm": 0.0,
            "endpoint_tolerance_mm": 2.0,
            "tolerance_basis": "Teste unitário sintético; não é dado extraído do Revit.",
        })
    return elements, {"physical_conductor_details": details}


def fixture_geometric_phase_route_audit() -> tuple[list[dict[str, str]], dict]:
    elements = fixture_elements()
    rows_by_mark = {row["marca"]: row for row in elements}
    details = []
    for edge_id, (from_pattern, to_pattern) in gc._GEOMETRIC_ROUTE_ENDPOINTS.items():
        edge = next(item for item in gc.EDGES if item.edge_id == edge_id)
        for phase_index, phase in enumerate("ABC", start=1):
            route_mark = next(
                prefix for prefix in edge.geometry_prefixes
                if (match := gc._PHASE_PREFIX.search(prefix)) and match.group(1) == phase
            )
            route = rows_by_mark[route_mark]
            route["categoria_revit"] = "Generic Models"
            route["model_geometry"] = "GEOMETRIC_ENVELOPE_ONLY_NO_ELECTRICAL_CONNECTOR"
            from_mark = from_pattern.format(index=phase_index)
            to_mark = to_pattern.format(index=phase_index)
            from_row, to_row = rows_by_mark[from_mark], rows_by_mark[to_mark]
            details.append({
                "audit_record_status": "API_GEOMETRY_EXTRACTED",
                "evidence_source": "REVIT_API_GEOMETRY_AUDIT",
                "representation_role": "GEOMETRIC_PHASE_ROUTE_ONLY",
                "edge_id": edge_id,
                "phase": phase,
                "phase_label_basis": "GEOMETRIC_LANE_LABEL_ONLY_PHASE_ASSIGNMENT_UNVERIFIED",
                "element_id": route["element_id"],
                "unique_id": route["unique_id"],
                "mark": route_mark,
                "revit_element_class": "DirectShape",
                "from_equipment_mark": from_mark,
                "from_equipment_match_count": 1,
                "from_equipment_element_id": int(from_row["element_id"]),
                "from_equipment_unique_id": from_row["unique_id"],
                "from_endpoint_gap_mm": 0.0,
                "to_equipment_mark": to_mark,
                "to_equipment_match_count": 1,
                "to_equipment_element_id": int(to_row["element_id"]),
                "to_equipment_unique_id": to_row["unique_id"],
                "to_endpoint_gap_mm": 0.0,
                "endpoint_gap_basis": "synthetic test distance to exact family bounding box",
                "endpoint_tolerance_mm": 25.0,
                "tolerance_basis": "synthetic geometry-only tolerance for unit testing",
                "route_radius_mm": 75.0,
                "route_vertices_m": [[0.0, 0.0, 0.0], [1.0, 0.0, 0.0]],
                "route_vertex_count": 2,
                "expected_solid_count": 1,
                "solid_count": 1,
                "solid_volume_m3": 0.0005,
                "model_bounds": {"min_m": {"x": 0.0, "y": 0.0, "z": 0.0}, "max_m": {"x": 1.0, "y": 0.0, "z": 0.0}},
                "geometry_exists": True,
                "electrical_connection_confirmed": False,
                "terminal_port_identified": False,
                "native_electrical_system_confirmed": False,
            })
    return elements, {"geometric_phase_route_details": details, "physical_conductor_details": []}


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
        self.assertEqual("NOT_EXTRACTED", graph["summary"]["physical_conductor_audit_status"])
        self.assertEqual(0, graph["summary"]["physical_conductor_records_observed"])
        self.assertTrue(all(not e["physical_conductor_confirmed"] for e in graph["edges"]))

    def test_complete_three_phase_geometry_is_reported_separately_from_native_circuit(self) -> None:
        elements, audit = fixture_physical_conductor_audit()
        graph = gc.build_graph(elements, audit, revision="TEST")
        edge = next(row for row in graph["edges"] if row["edge_id"] == "GIS-T01-CT-TO-TR-01-HV")
        self.assertTrue(edge["physical_conductor_confirmed"])
        self.assertEqual(["A", "B", "C"], edge["physical_conductor_evidence"]["confirmed_phases"])
        self.assertEqual("GEOMETRIC_ROUTE_ONLY", edge["evidence_class"])
        self.assertFalse(edge["native_electrical_system_confirmed"])
        self.assertEqual(3, graph["summary"]["physical_power_conductors_confirmed"])
        self.assertEqual(1, graph["summary"]["complete_three_phase_geometry_routes_confirmed"])
        self.assertEqual(3, graph["summary"]["physical_conductor_records_accepted"])

    def test_partial_phase_geometry_does_not_confirm_three_phase_route(self) -> None:
        elements, audit = fixture_physical_conductor_audit(phases="AB")
        graph = gc.build_graph(elements, audit, revision="TEST")
        edge = next(row for row in graph["edges"] if row["edge_id"] == "GIS-T01-CT-TO-TR-01-HV")
        self.assertFalse(edge["physical_conductor_confirmed"])
        self.assertIn("MISSING_PHASES:C", edge["physical_conductor_evidence"]["rejection_reasons"])

    def test_phase_route_must_end_at_the_matching_transformer_terminal(self) -> None:
        elements, audit = fixture_physical_conductor_audit()
        phase_b = audit["physical_conductor_details"][1]
        phase_a_terminal = next(row for row in elements if row["marca"] == "TR-01-B-HV-1")
        phase_b["to_terminal_element_id"] = phase_a_terminal["element_id"]
        phase_b["to_terminal_unique_id"] = phase_a_terminal["unique_id"]
        phase_b["to_terminal_mark"] = phase_a_terminal["marca"]
        graph = gc.build_graph(elements, audit, revision="TEST")
        edge = next(row for row in graph["edges"] if row["edge_id"] == "GIS-T01-CT-TO-TR-01-HV")
        self.assertFalse(edge["physical_conductor_confirmed"])
        self.assertEqual(1, graph["summary"]["physical_conductor_records_rejected"])
        self.assertEqual(2, graph["summary"]["physical_conductor_records_accepted"])

    def test_cable_tray_geometry_cannot_confirm_conductor(self) -> None:
        elements, audit = fixture_physical_conductor_audit()
        for row in elements:
            if row["marca"].startswith("COND-"):
                row["categoria_revit"] = "Cable Tray"
        graph = gc.build_graph(elements, audit, revision="TEST")
        edge = next(row for row in graph["edges"] if row["edge_id"] == "GIS-T01-CT-TO-TR-01-HV")
        self.assertFalse(edge["physical_conductor_confirmed"])
        self.assertEqual(0, graph["summary"]["physical_conductor_records_accepted"])
        self.assertEqual(3, graph["summary"]["physical_conductor_records_rejected"])

    def test_same_model_solid_cannot_be_reused_for_multiple_phases(self) -> None:
        elements, audit = fixture_physical_conductor_audit()
        audit["physical_conductor_details"][1]["element_id"] = audit["physical_conductor_details"][0]["element_id"]
        audit["physical_conductor_details"][1]["unique_id"] = audit["physical_conductor_details"][0]["unique_id"]
        audit["physical_conductor_details"][1]["mark"] = audit["physical_conductor_details"][0]["mark"]
        graph = gc.build_graph(elements, audit, revision="TEST")
        edge = next(row for row in graph["edges"] if row["edge_id"] == "GIS-T01-CT-TO-TR-01-HV")
        self.assertFalse(edge["physical_conductor_confirmed"])
        self.assertEqual(1, graph["summary"]["physical_conductor_records_accepted"])

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

    def test_revit_phase_route_geometry_audit_confirms_only_unique_equipment_envelope_paths(self) -> None:
        elements, audit = fixture_geometric_phase_route_audit()
        graph = gc.build_graph(elements, audit, revision="R20-15-test")
        self.assertEqual("API_GEOMETRY_VERIFIED", graph["summary"]["geometric_phase_route_audit_status"])
        self.assertEqual(12, graph["summary"]["geometric_phase_route_shapes_verified"])
        self.assertEqual(4, graph["summary"]["complete_three_phase_geometric_route_edges_verified"])
        self.assertEqual(0, graph["summary"]["physical_power_conductors_confirmed"])
        for edge_id in gc._GEOMETRIC_ROUTE_ENDPOINTS:
            edge = next(row for row in graph["edges"] if row["edge_id"] == edge_id)
            self.assertTrue(edge["geometric_phase_route_verified"])
            self.assertEqual("GEOMETRIC_ROUTE_ONLY", edge["evidence_class"])
            self.assertFalse(edge["physical_conductor_confirmed"])
            self.assertFalse(edge["native_electrical_system_confirmed"])

    def test_revit_directshape_audit_inventory_is_joined_when_equipment_csv_omits_it(self) -> None:
        elements, audit = fixture_geometric_phase_route_audit()
        route_marks = {item["mark"] for item in audit["geometric_phase_route_details"]}
        route_rows = [row for row in elements if row["marca"] in route_marks]
        elements = [row for row in elements if row["marca"] not in route_marks]
        audit["geometric_routes"] = [{
            "element_id": int(row["element_id"]),
            "unique_id": row["unique_id"],
            "mark": row["marca"],
            "comments": "DirectShape extraída em lista separada pela API do Revit.",
        } for row in route_rows]

        graph = gc.build_graph(elements, audit, revision="R20-23-split-inventory-test")

        self.assertEqual(12, graph["summary"]["geometric_phase_route_shapes_verified"])
        self.assertEqual(4, graph["summary"]["complete_three_phase_geometric_route_edges_verified"])
        self.assertEqual([], graph["summary"]["missing_geometric_route_edges"])
        self.assertEqual(0, graph["summary"]["physical_power_conductors_confirmed"])
        for edge_id in gc._GEOMETRIC_ROUTE_ENDPOINTS:
            edge = next(row for row in graph["edges"] if row["edge_id"] == edge_id)
            self.assertTrue(edge["geometric_phase_route_verified"])
            self.assertFalse(edge["physical_conductor_confirmed"])

    def test_cabletray_infrastructure_uses_its_separate_revit_api_inventory(self) -> None:
        elements = fixture_elements()
        audit = fixture_audit()
        audit["cable_tray_elements"] = [
            {"element_id": 8001, "unique_id": "tray-1", "mark": "LEITO-MV-A-SEG-01",
             "connector_count": 2, "connected_connector_count": 1,
             "comments": "Native CableTray infrastructure only."},
            {"element_id": 8002, "unique_id": "tray-2", "mark": "LEITO-MV-A-SEG-02",
             "connector_count": 2, "connected_connector_count": 2,
             "comments": "Native CableTray infrastructure only."},
        ]

        graph = gc.build_graph(elements, audit, revision="R20-23-split-tray-test")
        route = next(row for row in graph["infrastructure"] if row["mark_prefix"] == "LEITO-MV-A")

        self.assertEqual("NATIVE_CABLETRAY_INFRASTRUCTURE_ONLY", route["state"])
        self.assertEqual(2, len(route["elements"]))
        self.assertEqual(4, graph["summary"]["native_cable_tray_connectors"])
        self.assertEqual(3, graph["summary"]["native_cable_tray_connected_connectors"])
        self.assertFalse(route["electrical_connection"])

    def test_phase_route_outside_family_envelope_is_rejected_without_promoting_connection(self) -> None:
        elements, audit = fixture_geometric_phase_route_audit()
        audit["geometric_phase_route_details"][0]["to_endpoint_gap_mm"] = 26.0
        graph = gc.build_graph(elements, audit, revision="R20-15-test")
        edge = next(row for row in graph["edges"] if row["edge_id"] == audit["geometric_phase_route_details"][0]["edge_id"])
        self.assertFalse(edge["geometric_phase_route_verified"])
        self.assertIn("REJECTED_SOURCE_RECORDS_PRESENT", edge["geometric_phase_route_evidence"]["rejection_reasons"])
        self.assertEqual(11, graph["summary"]["geometric_phase_route_records_accepted"])
        self.assertEqual(0, graph["summary"]["physical_power_conductors_confirmed"])

    def test_phase_route_audit_rejects_terminal_claims_and_unmatched_equipment_identity(self) -> None:
        elements, audit = fixture_geometric_phase_route_audit()
        first = audit["geometric_phase_route_details"][0]
        first["terminal_port_identified"] = True
        second = audit["geometric_phase_route_details"][1]
        second["to_equipment_unique_id"] = "wrong-equipment"
        graph = gc.build_graph(elements, audit, revision="R20-15-test")
        self.assertEqual(10, graph["summary"]["geometric_phase_route_records_accepted"])
        self.assertEqual(2, graph["summary"]["geometric_phase_route_records_rejected"])

    def test_gis_selector_stubs_are_geometry_only_not_power_connections(self) -> None:
        graph = gc.build_graph(fixture_elements(), fixture_audit())
        for edge_id in ("GIS-L01-A-SELECTOR", "GIS-BC-A-SELECTOR", "GIS-BC-B-SELECTOR"):
            edge = next(e for e in graph["edges"] if e["edge_id"] == edge_id)
            self.assertEqual("GEOMETRIC_ROUTE_ONLY", edge["evidence_class"])
            self.assertTrue(edge["geometry_present"])
            self.assertFalse(edge["native_electrical_system_confirmed"])
            self.assertFalse(edge["physical_conductor_confirmed"])

    def test_bus_coupler_ct_is_inserted_in_series_between_breaker_and_bus_b_selector(self) -> None:
        graph = gc.build_graph(fixture_elements(), fixture_audit())
        edges = {edge["edge_id"]: edge for edge in graph["edges"]}
        self.assertNotIn("GIS-BC-52-TO-B", edges)
        self.assertEqual(("GIS-BC01-89A", "GIS-BC01-52"),
                         (edges["GIS-BC-A-TO-52"]["from_node"], edges["GIS-BC-A-TO-52"]["to_node"]))
        self.assertEqual(("GIS-BC01-52", "GIS-BC01-CT"),
                         (edges["GIS-BC-52-TO-CT"]["from_node"], edges["GIS-BC-52-TO-CT"]["to_node"]))
        self.assertEqual("ASSUMED_OPEN_NORMAL", edges["GIS-BC-52-TO-CT"]["state"])
        self.assertEqual(("GIS-BC01-CT", "GIS-BC01-89B"),
                         (edges["GIS-BC-CT-TO-89B"]["from_node"], edges["GIS-BC-CT-TO-89B"]["to_node"]))
        self.assertEqual(("GIS-BC01-89B", "BUS-138-B"),
                         (edges["GIS-BC-B-SELECTOR"]["from_node"], edges["GIS-BC-B-SELECTOR"]["to_node"]))
        self.assertEqual("DIAGRAM_ONLY", edges["GIS-BC-CT-TO-89B"]["evidence_class"])
        self.assertFalse(edges["GIS-BC-CT-TO-89B"]["native_electrical_system_confirmed"])
        self.assertTrue(all(not edge["physical_conductor_confirmed"] for edge in edges.values()
                            if edge["edge_id"].startswith("GIS-BC-")))

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

    def test_model_element_with_duplicated_element_id_is_not_identity_confirmed(self) -> None:
        rows = fixture_elements()
        ct = next(row for row in rows if row["marca"] == "GIS-BC01-CT")
        rows.append({**ct, "unique_id": "uid-unrelated", "marca": "UNRELATED-ELEMENT"})
        graph = gc.build_graph(rows, fixture_audit())
        ct_node = next(node for node in graph["nodes"] if node["node_id"] == "GIS-BC01-CT")
        self.assertEqual("MODEL_ELEMENT_IDENTITY_UNVERIFIED", ct_node["model_state"])
        self.assertEqual("ELEMENT_IDENTITY_UNVERIFIED", ct_node["model_elements"][0]["identity_status"])
        self.assertIn("GIS-BC01-CT", graph["summary"]["incomplete_or_ambiguous_model_nodes"])
        self.assertIn("GIS-BC01-CT", graph["summary"]["model_elements_without_unique_identity"])

    def test_model_element_without_unique_id_is_not_identity_confirmed(self) -> None:
        rows = fixture_elements()
        ct = next(row for row in rows if row["marca"] == "GIS-BC01-CT")
        ct["unique_id"] = ""
        graph = gc.build_graph(rows, fixture_audit())
        ct_node = next(node for node in graph["nodes"] if node["node_id"] == "GIS-BC01-CT")
        self.assertEqual("MODEL_ELEMENT_IDENTITY_UNVERIFIED", ct_node["model_state"])
        self.assertIn("GIS-BC01-CT", graph["summary"]["model_elements_without_unique_identity"])

    def test_duplicate_equipment_mark_makes_native_auxiliary_identity_ambiguous(self) -> None:
        rows = fixture_elements()
        qac = next(row for row in rows if row["marca"] == "QAC-01")
        rows.append({**qac, "element_id": "999", "unique_id": "uid-999"})
        graph = gc.build_graph(rows, fixture_audit())
        self.assertEqual(["QAC-01"], graph["summary"]["duplicate_model_marks"])
        self.assertEqual(0, graph["summary"]["native_electrical_system_relations_confirmed"])
        qac_node = next(node for node in graph["nodes"] if node["node_id"] == "QAC-01")
        self.assertEqual("AMBIGUOUS_MODEL_MARK", qac_node["model_state"])

    def test_duplicate_audit_system_id_is_not_counted_twice(self) -> None:
        audit = fixture_audit()
        audit["electrical_system_details"].append(dict(audit["electrical_system_details"][0]))
        graph = gc.build_graph(fixture_elements(), audit)
        self.assertEqual(["AUX-UPS-01"], graph["summary"]["duplicate_audited_system_ids"])
        self.assertEqual(0, graph["summary"]["native_electrical_system_relations_confirmed"])

    def test_partial_phase_terminal_group_is_reported(self) -> None:
        rows = [row for row in fixture_elements() if row["marca"] != "TR-01-B-HV-2"]
        graph = gc.build_graph(rows, fixture_audit())
        terminals = next(node for node in graph["nodes"] if node["node_id"] == "TR-01-HV-TERMINALS")
        self.assertEqual("PARTIAL_MODEL_ELEMENTS", terminals["model_state"])
        self.assertIn("TR-01-B-HV-2", terminals["expected_marks_missing"])
        self.assertIn("TR-01-HV-TERMINALS", graph["summary"]["incomplete_or_ambiguous_model_nodes"])

    def test_r19_snapshot_stays_historical_and_current_spec_updates_ct_series(self) -> None:
        audit_dir = ROOT / "projetos" / "19-subestacao-gis" / "dados" / "reconstrucao-r19"
        enhanced_dir = audit_dir / "auditoria-grafo-identidade-v4"
        elements = gc._load_rows(audit_dir / "conectividade-equipamentos-r19.csv")
        audit = json.loads((audit_dir / "auditoria-modelo-r19.json").read_text(encoding="utf-8"))
        actual = gc.build_graph(elements, audit, revision="R20-21-SPEC-ON-R19-EXTRACTION")
        saved = json.loads((enhanced_dir / "grafo-conectividade-r19.json").read_text(encoding="utf-8"))
        saved_edges = {row["edge_id"]: row for row in saved["edges"]}
        actual_edges = {row["edge_id"]: row for row in actual["edges"]}
        self.assertEqual("R19", saved["revision"])
        self.assertEqual(69, saved["summary"]["edges_expected"])
        self.assertIn("GIS-BC-52-TO-B", saved_edges)
        self.assertNotIn("GIS-BC-52-TO-CT", saved_edges)
        self.assertNotIn("GIS-BC-CT-TO-89B", saved_edges)
        self.assertEqual(73, actual["summary"]["nodes_expected"])
        self.assertEqual(70, actual["summary"]["edges_expected"])
        self.assertNotIn("GIS-BC-52-TO-B", actual_edges)
        self.assertEqual(("GIS-BC01-52", "GIS-BC01-CT"),
                         (actual_edges["GIS-BC-52-TO-CT"]["from_node"], actual_edges["GIS-BC-52-TO-CT"]["to_node"]))
        self.assertEqual(("GIS-BC01-CT", "GIS-BC01-89B"),
                         (actual_edges["GIS-BC-CT-TO-89B"]["from_node"], actual_edges["GIS-BC-CT-TO-89B"]["to_node"]))
        self.assertEqual([], actual["summary"]["duplicate_model_marks"])
        self.assertEqual([], actual["summary"]["duplicate_element_ids"])
        self.assertEqual([], actual["summary"]["duplicate_unique_ids"])
        self.assertEqual([], actual["summary"]["duplicate_audited_system_ids"])
        self.assertEqual([], actual["summary"]["incomplete_or_ambiguous_model_nodes"])
        self.assertEqual(0, actual["summary"]["physical_power_conductors_confirmed"])
        self.assertEqual("GEOMETRIC_ROUTE_CANDIDATES_ONLY", actual["summary"]["physical_conductor_audit_status"])
        self.assertTrue(actual["physical_conductor_audit"]["route_candidates_fallback_used"])
        self.assertGreater(actual["summary"]["physical_conductor_records_observed"], 0)
        self.assertEqual(0, actual["summary"]["physical_conductor_records_accepted"])
        self.assertEqual(
            actual["summary"]["physical_conductor_records_observed"],
            actual["summary"]["physical_conductor_records_rejected"],
        )
        self.assertEqual(1, actual["summary"]["native_electrical_system_relations_confirmed"])
        self.assertEqual(0, actual["summary"]["native_electrical_system_load_calculations_confirmed"])
        self.assertEqual(1, actual["summary"]["view_specific_wires_confirmed"])
        self.assertEqual("NOT_EXTRACTED", actual["summary"]["geometric_phase_route_audit_status"])
        self.assertEqual(0, actual["summary"]["geometric_phase_route_shapes_verified"])

    def test_cable_trays_are_infrastructure_only(self) -> None:
        audit = fixture_audit()
        audit["cable_tray_elements"] = [{
            "element_id": 8001, "unique_id": "tray-1", "mark": "LEITO-MV-A-SEG-01",
            "connector_count": 2, "connected_connector_count": 1,
        }]
        graph = gc.build_graph(fixture_elements(), audit)
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
