import copy
import json
import unittest
from pathlib import Path

from scripts.engineering.gis_equipment_reference import (
    DEFAULT_INPUTS,
    REFERENCES,
    calculate_impedance_sensitivity,
)


class GisEquipmentReferenceTest(unittest.TestCase):
    def test_saved_r21_references_and_sensitivity_match_the_current_source(self):
        root = Path(__file__).resolve().parents[1]
        data_dir = root / "projetos/19-subestacao-gis/dados/reconstrucao-r21"
        saved_references = json.loads((data_dir / "referencias-equipamentos-r21.json").read_text(encoding="utf-8"))
        self.assertEqual(REFERENCES, saved_references)
        inputs = json.loads((data_dir / "sensibilidade-impedancia-transformador-r21-input.json").read_text(encoding="utf-8"))
        saved_results = json.loads((data_dir / "sensibilidade-impedancia-transformador-r21.json").read_text(encoding="utf-8"))
        self.assertEqual(calculate_impedance_sensitivity(inputs), saved_results)

    def test_gis_145kv_published_bay_dimensions_are_kept_separate_from_order_ga(self):
        gis = next(item for item in REFERENCES["equipment"] if item["equipment_id"] == "GIS-REF-145")
        self.assertEqual(gis["verified_catalog_facts"]["bay_width_mm"], 800)
        self.assertEqual(gis["reference_configuration"], "FULLY_DIGITAL_SF6_BAY")
        self.assertEqual(gis["verified_catalog_facts"]["bay_dimensions_mm"], {"width": 800, "depth": 3700, "height": 2500})
        self.assertEqual(gis["verified_catalog_facts"]["typical_layout_figure_callouts_mm"]["line_bay_plan_callouts"], [1400, 3700, 600, 5700])
        self.assertEqual(gis["model_dimensions_m"], {"width": 0.8, "depth": 3.7, "height": 2.5})
        self.assertEqual(gis["unverified_order_specific_details"]["order_ga"], "NOT_AVAILABLE")
        self.assertEqual(gis["dimension_status"]["width"], "PUBLISHED_BAY_DIMENSION_REFERENCE")
        self.assertEqual(gis["dimension_status"]["depth"], "PUBLISHED_BAY_DIMENSION_REFERENCE")
        self.assertEqual(gis["dimension_status"]["height"], "PUBLISHED_BAY_DIMENSION_REFERENCE")
        self.assertEqual(gis["dimension_status"]["selected_order_ga"], "NOT_SELECTED")
        self.assertIn("page 6 explicitly lists Bay Dimensions", gis["layout_clearance_note"])
        self.assertIn("not as a project-specific order GA", gis["scope_note"])

    def test_hitachi_double_busbar_layout_reference_is_not_misapplied_to_ge_f35(self):
        layout = next(item for item in REFERENCES["equipment"] if item["equipment_id"] == "GIS-REF-145-LAYOUT")
        self.assertIsNone(layout["model_dimensions_m"])
        self.assertIn("NOT_USED_AS_GE_F35_BAY_GEOMETRY", layout["dimension_status"])
        dims = layout["verified_catalog_facts"]
        self.assertEqual((dims["overall_length_mm"], dims["overall_width_mm"], dims["overall_height_mm"]), (13000, 3000, 3500))

    def test_mv_dimensions_match_the_2000a_reference_line(self):
        mv = next(item for item in REFERENCES["equipment"] if item["equipment_id"] == "QMT-REF-17K5-2KA")
        self.assertEqual(mv["verified_catalog_facts"]["feeder_current_range_a"], [1600, 2000])
        self.assertEqual(mv["model_dimensions_m"], {"width": 0.8, "depth": 1.34, "height": 2.2})
        self.assertIn("no short-circuit rating", mv["scope_note"])

    def test_catalog_impedance_is_a_sensitivity_not_a_replacement_for_r8_baseline(self):
        result = calculate_impedance_sensitivity(DEFAULT_INPUTS)
        self.assertAlmostEqual(result["nominal_currents_a"]["hv"], 40_000_000 / (3**0.5 * 138_000))
        self.assertAlmostEqual(result["nominal_currents_a"]["mv"], 40_000_000 / (3**0.5 * 13_800))
        r8 = result["scenarios"]["R8_assumption"]
        candidate = result["scenarios"]["catalog_candidate"]
        self.assertAlmostEqual(r8["three_phase_fault_current_mv_ka_transformer_only"], 16.734790, places=5)
        self.assertAlmostEqual(candidate["three_phase_fault_current_mv_ka_transformer_only"], 15.213446, places=5)
        self.assertAlmostEqual(result["candidate_vs_R8_mv_fault_current_change_percent"], -9.090909, places=5)
        self.assertIn("NOT_A_COMPLETE_SHORT_CIRCUIT_STUDY", result["validation_status"])

    def test_invalid_impedance_or_voltage_is_rejected(self):
        altered = copy.deepcopy(DEFAULT_INPUTS)
        altered["catalog_candidate_impedance_pu"] = 0
        with self.assertRaisesRegex(ValueError, "devem ser positivas"):
            calculate_impedance_sensitivity(altered)

    def test_non_finite_or_non_numeric_engineering_inputs_are_rejected(self):
        numeric_fields = (
            "transformer_rating_mva",
            "hv_voltage_kv",
            "mv_voltage_kv",
            "r8_impedance_assumption_pu",
            "catalog_candidate_impedance_pu",
        )
        invalid_values = (float("nan"), float("inf"), float("-inf"), True, "NaN")
        for field in numeric_fields:
            for invalid in invalid_values:
                with self.subTest(field=field, invalid=repr(invalid)):
                    altered = copy.deepcopy(DEFAULT_INPUTS)
                    altered[field] = invalid
                    with self.assertRaises(ValueError):
                        calculate_impedance_sensitivity(altered)

    def test_missing_required_input_is_reported_explicitly(self):
        altered = copy.deepcopy(DEFAULT_INPUTS)
        del altered["transformer_rating_mva"]
        with self.assertRaisesRegex(ValueError, "Campo obrigatório ausente"):
            calculate_impedance_sensitivity(altered)

    def test_non_object_input_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "objeto JSON"):
            calculate_impedance_sensitivity(None)  # type: ignore[arg-type]


if __name__ == "__main__":
    unittest.main()
