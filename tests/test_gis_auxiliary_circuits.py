from __future__ import annotations

import unittest

from scripts.engineering.gis_auxiliary_circuits import PANEL_MARKS, build_schedule, validate_revit_audit


def auxiliary_inputs() -> dict:
    return {
        "ac_system": {
            "voltage_v_line_to_line_assumed": 380.0,
            "power_factor_assumed": 0.9,
            "loads": [{
                "id": "AUX-AC-06",
                "quantity": 11,
                "unit_power_kw_assumed": 0.15,
                "demand_factor_assumed": 1.0,
                "basis": "Eleven protection/control panels; load estimate pending datasheets",
            }],
        }
    }


def fixture_model() -> tuple[list[dict[str, str]], dict]:
    equipment = [{
        "element_id": "100",
        "unique_id": "uid-100",
        "marca": "QAC-01",
        "mep_connectors": "1",
        "dominios": "DomainElectrical",
        "isconnected_true": "0",
    }]
    systems = []
    for index, mark in enumerate(PANEL_MARKS, start=101):
        row = {
            "element_id": str(index),
            "unique_id": f"uid-{index}",
            "marca": mark,
            "mep_connectors": "1",
            "dominios": "DomainElectrical",
            "isconnected_true": "0",
        }
        equipment.append(row)
        systems.append({
            "system_id": f"AUX-AC-{mark}",
            "revit_load_name": f"AUX-AC-{mark}",
            "revit_element_id": index + 1000,
            "revit_unique_id": f"sys-{index}",
            "circuit_number": str(index - 100),
            "revit_class": "ElectricalSystem",
            "system_type": "PowerCircuit",
            "native_membership_confirmed": True,
            "panel_assignment_confirmed": True,
            "terminal_membership_confirmed": True,
            "load_name_confirmed": True,
            "load_calculation_confirmed": True,
            "model_voltage_V": 220.0,
            "model_true_load_W": 150.0,
            "model_apparent_power_VA": 166.6666667,
            "model_power_factor": 0.9,
            "model_apparent_current_A": 0.7575758,
            "physical_endpoint_connectors_isconnected_true": 0,
            "selected_panel_element": {
                "element_id": 100,
                "unique_id": "uid-100",
                "mark": "QAC-01",
            },
            "terminal_elements": [{
                "element_id": index,
                "unique_id": f"uid-{index}",
                "mark": mark,
            }],
            "wire_2d_view_specific_confirmed": False,
            "three_dimensional_cable_modeled": False,
        })
    return equipment, {"electrical_system_details": systems, "primary_power_conductors_confirmed": 0}


class GISAuxiliaryCircuitTests(unittest.TestCase):
    def test_schedule_derives_eleven_explicit_single_phase_panel_loads(self):
        schedule = build_schedule(auxiliary_inputs())
        self.assertEqual(11, len(schedule["circuits"]))
        self.assertEqual(1.65, schedule["aggregate"]["active_power_kw_demand"])
        self.assertEqual({"A": 4, "B": 4, "C": 3}, schedule["aggregate"]["phase_assignment_counts"])
        self.assertAlmostEqual(0.7575757576, schedule["circuits"][0]["current_a"])
        self.assertEqual(set(PANEL_MARKS), {c["terminal_equipment_mark"] for c in schedule["circuits"]})
        self.assertTrue(all(not c["physical_power_conductor_confirmed"] for c in schedule["circuits"]))

    def test_schedule_labels_requested_revision_and_rejects_path_like_values(self):
        schedule = build_schedule(auxiliary_inputs(), revision="r13")
        self.assertEqual("R13", schedule["revision"])
        self.assertEqual("NOT_RUN_NO_REVIT_R13_AUDIT", schedule["model_validation"]["status"])
        with self.assertRaises(ValueError):
            build_schedule(auxiliary_inputs(), revision="../../R13")

    def test_revit_validation_confirms_logical_loads_without_claiming_physical_feeders(self):
        schedule = build_schedule(auxiliary_inputs())
        equipment, audit = fixture_model()
        verified = validate_revit_audit(schedule, equipment, audit)
        self.assertEqual("ALL_EXPECTED_LOGICAL_SYSTEMS_AND_REVIT_LOADS_CONFIRMED_NO_PHYSICAL_FEEDERS", verified["model_validation"]["status"])
        self.assertEqual(11, verified["model_validation"]["native_systems_confirmed"])
        self.assertEqual(11, verified["model_validation"]["calculated_loads_confirmed"])
        self.assertEqual(0, verified["model_validation"]["physical_power_conductors_confirmed"])
        self.assertTrue(all(c["native_circuit_status"].startswith("CONFIRMED") for c in verified["circuits"]))
        self.assertTrue(all(c["logical_endpoint_connectors_isconnected_true"] == 0 for c in verified["circuits"]))
        self.assertTrue(all(not c["physical_power_conductor_confirmed"] for c in verified["circuits"]))

    def test_mismatched_terminal_identity_is_not_counted_as_a_circuit(self):
        schedule = build_schedule(auxiliary_inputs())
        equipment, audit = fixture_model()
        audit["electrical_system_details"][0]["terminal_elements"][0]["unique_id"] = "wrong-terminal"
        verified = validate_revit_audit(schedule, equipment, audit)
        self.assertEqual(10, verified["model_validation"]["native_systems_confirmed"])
        self.assertEqual("PARTIAL_OR_FAILED_LOGICAL_SYSTEM_OR_LOAD_VALIDATION", verified["model_validation"]["status"])

    def test_disconnected_endpoints_do_not_masquerade_as_physical_conductors(self):
        schedule = build_schedule(auxiliary_inputs())
        equipment, audit = fixture_model()
        equipment[1]["isconnected_true"] = "0"
        verified = validate_revit_audit(schedule, equipment, audit)
        self.assertEqual(11, verified["model_validation"]["native_systems_confirmed"])
        self.assertEqual(0, verified["model_validation"]["physical_power_conductors_confirmed"])

    def test_zero_or_inconsistent_revit_load_is_not_confirmed(self):
        schedule = build_schedule(auxiliary_inputs())
        equipment, audit = fixture_model()
        audit["electrical_system_details"][0]["model_true_load_W"] = 0.0
        audit["electrical_system_details"][0]["load_calculation_confirmed"] = False
        verified = validate_revit_audit(schedule, equipment, audit)
        self.assertEqual(10, verified["model_validation"]["native_systems_confirmed"])
        self.assertIn("load-calculation", next(iter(verified["model_validation"]["unconfirmed_systems"].values())))

    def test_wrong_connector_domain_is_not_confirmed(self):
        schedule = build_schedule(auxiliary_inputs())
        equipment, audit = fixture_model()
        equipment[1]["dominios"] = "DomainUndefined"
        verified = validate_revit_audit(schedule, equipment, audit)
        self.assertEqual(10, verified["model_validation"]["native_systems_confirmed"])
        self.assertIn("Electrical-domain connectors", next(iter(verified["model_validation"]["unconfirmed_systems"].values())))

    def test_schedule_rejects_text_that_looks_numeric(self):
        inputs = auxiliary_inputs()
        inputs["ac_system"]["loads"][0]["unit_power_kw_assumed"] = "0.15"
        with self.assertRaises(ValueError):
            build_schedule(inputs)


if __name__ == "__main__":
    unittest.main()
