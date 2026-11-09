import copy
import csv
import json
import tempfile
import unittest
from pathlib import Path

from scripts.engineering.gis_substation_study import (
    audit_model_mark_crosswalk,
    calculate,
    write_outputs,
)


ROOT = Path(__file__).resolve().parents[1]
BASE = json.loads((ROOT / "projetos/19-subestacao-gis/dados/base-de-projeto.json").read_text(encoding="utf-8"))
R20_14_CALC_DIR = ROOT / "projetos/19-subestacao-gis/attempts/r20-14-candidate/dados/reconstrucao-r20/calculos-n1"


class GisSubstationStudyTest(unittest.TestCase):
    def test_saved_r20_study_matches_the_current_calculation_engine(self):
        data_dir = R20_14_CALC_DIR
        base = json.loads((data_dir / "base-de-projeto.json").read_text(encoding="utf-8"))
        saved = json.loads((data_dir / "resultados.json").read_text(encoding="utf-8"))
        expected = calculate(base)
        expected["model_mark_crosswalk_audit"] = audit_model_mark_crosswalk(
            expected,
            ROOT / "projetos/19-subestacao-gis/attempts/r20-14-candidate/dados/reconstrucao-r20/quantitativos-modelo.csv",
        )
        self.assertEqual(expected, saved)
        self.assertEqual(len(saved["calculations"]), 32)
        self.assertEqual(saved["diagram_to_model_crosswalk"]["A-F01"], ["QMT-A-F01"])
        self.assertIn("r20-14-candidate", saved["model_mark_crosswalk_audit"]["source_file"])
        self.assertEqual(saved["model_mark_crosswalk_audit"]["status"], "MODEL_MARK_CROSSWALK_COMPLETE")
        self.assertFalse(saved["model_mark_crosswalk_audit"]["connectivity_verified"])
        with (data_dir / "resultados.csv").open(encoding="utf-8-sig", newline="") as stream:
            rows = {row["calc_id"]: row for row in csv.DictReader(stream, delimiter=";")}
        self.assertEqual(rows["BP-22"]["model_mark_identity_status"], "RESOLVED")
        self.assertEqual(rows["BP-22"]["model_mark_refs"], "TR-01|TR-02")
        self.assertEqual(rows["BP-22"]["model_element_ids"], "|".join(
            saved["model_mark_crosswalk_audit"]["mark_to_revit_elements"][mark][0]["element_id"]
            for mark in ("TR-01", "TR-02")
        ))
        self.assertEqual(rows["BP-22"]["model_unique_ids"], "|".join(
            saved["model_mark_crosswalk_audit"]["mark_to_revit_elements"][mark][0]["unique_id"]
            for mark in ("TR-01", "TR-02")
        ))

    def test_load_currents_and_transformer_ratings(self):
        result = calculate(BASE)
        rows = {item["calc_id"]: item for item in result["calculations"]}
        self.assertAlmostEqual(rows["BP-01"]["result"], 56.0 / 0.95)
        self.assertAlmostEqual(rows["BP-06"]["result"], 40.0 / (3**0.5 * 13.8))
        self.assertAlmostEqual(rows["BP-15"]["result"], 28.0 / 0.95 / (3**0.5 * 13.8))
        self.assertEqual(rows["BP-22"]["result"], "10:1")
        self.assertEqual(rows["BP-22"]["diagram_refs"], ["TR-01", "TR-02"])
        self.assertEqual(rows["BP-22"]["model_mark_refs"], ["TR-01", "TR-02"])

    def test_n_minus_one_requires_shedding_and_retains_critical_load(self):
        result = calculate(BASE)
        rows = {item["calc_id"]: item for item in result["calculations"]}
        self.assertAlmostEqual(rows["BP-08"]["result"], 32.0 / 0.95 / 40.0 * 100.0)
        self.assertAlmostEqual(rows["BP-09"]["result"], 56.0 / 0.95 / 40.0 * 100.0)
        self.assertAlmostEqual(rows["BP-10"]["result"], 24.0)
        self.assertTrue(result["quality_checks"]["critical_load_within_one_transformer_nameplate"])
        self.assertTrue(result["quality_checks"]["full_load_requires_shedding_in_n_minus_one"])

    def test_n_minus_one_distinguishes_open_tie_from_conditional_load_transfer(self):
        result = calculate(BASE)
        rows = {item["calc_id"]: item for item in result["calculations"]}
        cases = result["n_minus_one_scenarios"]["transformer_outage_with_tie_open"]
        retained = result["n_minus_one_scenarios"]["candidate_full_noncritical_feeder_retention"]
        envelope = result["n_minus_one_scenarios"]["transfer_available_aggregate_envelope"]

        self.assertEqual(len(cases), 2)
        self.assertEqual([case["critical_load_on_isolated_section_mw"] for case in cases], [16.0, 16.0])
        self.assertEqual(cases[0]["failed_transformer_tag"], "TR-01")
        self.assertEqual(cases[0]["isolated_section_incomer_tag"], "QMT-A-IN")
        self.assertIn("QMT-TIE", cases[0]["diagram_refs"])
        self.assertIn("A-F01", cases[0]["diagram_refs"])
        self.assertIn("QMT-A-F01", cases[0]["model_mark_refs"])
        self.assertAlmostEqual(cases[0]["surviving_transformer_loading_percent"], 28.0 / 0.95 / 40.0 * 100.0)
        self.assertEqual(rows["BP-16"]["result"], 38.0)
        self.assertAlmostEqual(rows["BP-17"]["result"], 40.0 - 32.0 / 0.95)
        self.assertEqual(rows["BP-18"]["result"], 6.0)
        self.assertEqual(rows["BP-19"]["result"], 18.0)
        self.assertEqual(rows["BP-20"]["result"], 16.0)
        self.assertEqual(rows["BP-21"]["result"], 100.0)
        self.assertEqual([option["retained_feeder_tag"] for option in retained], ["A-F04", "A-F05", "B-F04", "B-F05"])
        self.assertTrue(all(abs(option["surviving_transformer_loading_percent"] - 100.0) < 1e-9 for option in retained))
        self.assertEqual(envelope["planned_noncritical_shedding_mw"], 24.0)
        self.assertEqual(envelope["planned_shedding_exceeds_minimum_by_mw"], 6.0)
        self.assertFalse(result["quality_checks"]["n1_open_tie_serves_all_critical_sections"])
        self.assertTrue(result["quality_checks"]["n1_critical_load_fits_one_unit_if_transfer_path_is_available"])
        self.assertTrue(result["quality_checks"]["n1_minimum_shedding_satisfies_one_unit_nameplate"])
        self.assertTrue(result["quality_checks"]["all_calculations_have_diagram_and_model_refs"])
        feeder_rows = {item["calc_id"]: item for item in result["calculations"] if item["calc_id"].startswith("FDR-")}
        self.assertEqual(feeder_rows["FDR-A-F01"]["diagram_refs"], ["A-F01"])
        self.assertEqual(feeder_rows["FDR-A-F01"]["model_mark_refs"], ["QMT-A-F01"])

    def test_n_minus_one_study_requires_two_transformer_sections(self):
        altered = copy.deepcopy(BASE)
        altered["transformers"]["count"] = 1
        with self.assertRaisesRegex(ValueError, "exatamente dois transformadores"):
            calculate(altered)

    def test_assumed_transformer_short_circuit_is_not_marked_as_device_selection(self):
        result = calculate(BASE)
        rows = {item["calc_id"]: item for item in result["calculations"]}
        self.assertAlmostEqual(rows["BP-12"]["result"], 40.0 / (3**0.5 * 13.8) / 0.10)
        self.assertIn("Fonte ideal", rows["BP-12"]["source_or_assumption"])
        self.assertTrue(any(item["calc_id"] == "SC-HV-01" for item in result["not_calculated"]))

    def test_switchgear_current_is_only_a_candidate_not_a_verified_voltage_current_pair(self):
        result = calculate(BASE)
        bp11 = next(item for item in result["calculations"] if item["calc_id"] == "BP-11")
        self.assertAlmostEqual(bp11["result"], 40.0 / (3**0.5 * 13.8) * 1000.0 / 2000.0 * 100.0)
        self.assertIn("candidato de 2.000 A", bp11["source_or_assumption"])
        self.assertIn("não comprova essa combinação específica", bp11["source_or_assumption"])

    def test_feeder_sums_are_validated(self):
        altered = copy.deepcopy(BASE)
        altered["load_scenarios"]["feeders"][0]["active_mw"] = 7.0
        with self.assertRaises(ValueError):
            calculate(altered)

    def test_rejects_coercive_and_nonfinite_study_inputs(self):
        altered = copy.deepcopy(BASE)
        altered["system"]["frequency_hz"] = "60"
        with self.assertRaisesRegex(ValueError, "JSON number"):
            calculate(altered)

        altered = copy.deepcopy(BASE)
        altered["system"]["nominal_voltage_kv"][0] = True
        with self.assertRaisesRegex(ValueError, "JSON number"):
            calculate(altered)

        altered = copy.deepcopy(BASE)
        altered["load_scenarios"]["feeders"][0]["critical"] = "false"
        with self.assertRaisesRegex(ValueError, "must be a boolean"):
            calculate(altered)

        altered = copy.deepcopy(BASE)
        altered["load_scenarios"]["normal_active_mw_assumed"] = float("nan")
        with self.assertRaisesRegex(ValueError, "must be finite"):
            calculate(altered)

        altered = copy.deepcopy(BASE)
        altered["load_scenarios"]["normal_active_mw_assumed"] = 10**1000
        with self.assertRaisesRegex(ValueError, "must be finite"):
            calculate(altered)

        altered = copy.deepcopy(BASE)
        altered["load_scenarios"]["feeders"][0]["section"] = []
        with self.assertRaisesRegex(ValueError, "section must be A or B"):
            calculate(altered)

    def test_rejects_nonfinite_derived_results_from_finite_but_extreme_inputs(self):
        altered = copy.deepcopy(BASE)
        altered["load_scenarios"]["power_factor_assumed"] = 1e-308
        with self.assertRaisesRegex(ValueError, "non-finite numeric result"):
            calculate(altered)

    def test_outputs_include_traceability_and_pending_calculation_ids(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            result = write_outputs(BASE, Path(temp_dir))
            output = Path(temp_dir)
            self.assertTrue((output / "resultados.json").is_file())
            self.assertTrue((output / "memorial-de-calculo.md").is_file())
            with (output / "resultados.csv").open(encoding="utf-8-sig", newline="") as stream:
                rows = list(csv.DictReader(stream, delimiter=";"))
            self.assertEqual(len(rows), len(result["calculations"]))
            self.assertEqual(rows[0]["calc_id"], "BP-01")
            self.assertEqual(rows[0]["diagram_refs"], "A-F01|A-F02|A-F03|A-F04|A-F05|B-F01|B-F02|B-F03|B-F04|B-F05")
            self.assertEqual(rows[0]["model_mark_refs"], "QMT-A-F01|QMT-A-F02|QMT-A-F03|QMT-A-F04|QMT-A-F05|QMT-B-F01|QMT-B-F02|QMT-B-F03|QMT-B-F04|QMT-B-F05")
            self.assertEqual(rows[0]["model_mark_identity_status"], "NOT_VERIFIED")
            self.assertEqual(rows[0]["model_element_ids"], "")
            self.assertIn("PROT-01", {item["calc_id"] for item in result["not_calculated"]})
            memorial = (output / "memorial-de-calculo.md").read_text(encoding="utf-8")
            self.assertIn("Cenários N-1 nas barras de média tensão", memorial)
            self.assertIn("Crítica isolada (MW)", memorial)
            self.assertIn("| BP-01 | A-F01, A-F02", memorial)
            self.assertIn("QMT-A-F01", memorial)
            self.assertIn("## Crosswalk do unifilar para as marcas BIM", memorial)
            self.assertIn("| A-F01 | QMT-A-F01 |", memorial)
            self.assertIn("NOT_VERIFIED", memorial)
            self.assertFalse(result["model_mark_crosswalk_audit"]["connectivity_verified"])
            self.assertIn("não prova conectividade elétrica", memorial)
            self.assertIn("rejeição mínima agregada de 18 MW", memorial)
            self.assertIn("A-F04", memorial)
            self.assertIn("[base-de-projeto.json](base-de-projeto.json)", memorial)
            self.assertNotIn("base-de-projeto.md)", memorial)

    def test_model_mark_crosswalk_matches_the_latest_native_r20_14_export(self):
        result = calculate(BASE)
        quantitative_csv = ROOT / "projetos/19-subestacao-gis/attempts/r20-14-candidate/dados/reconstrucao-r20/quantitativos-modelo.csv"
        audit = audit_model_mark_crosswalk(result, quantitative_csv)
        self.assertEqual(audit["status"], "MODEL_MARK_CROSSWALK_COMPLETE")
        self.assertEqual(audit["referenced_mark_count"], 33)
        self.assertEqual(audit["missing_marks"], [])
        self.assertEqual(audit["duplicate_referenced_marks"], [])
        self.assertEqual(audit["incomplete_element_identity_marks"], [])
        self.assertFalse(audit["connectivity_verified"])
        self.assertEqual(len(audit["mark_to_revit_elements"]), 33)
        self.assertTrue(all(len(elements) == 1 for elements in audit["mark_to_revit_elements"].values()))
        self.assertTrue(all(elements[0]["element_id"] and elements[0]["unique_id"] for elements in audit["mark_to_revit_elements"].values()))

    def test_model_mark_crosswalk_flags_missing_marks_and_duplicate_identity(self):
        result = calculate(BASE)
        with tempfile.TemporaryDirectory() as temp_dir:
            csv_path = Path(temp_dir) / "quantitativos.csv"
            csv_path.write_text(
                "element_id;unique_id;categoria_revit;familia;tipo;marca\n"
                "101;uid-a;Electrical Equipment;GIS;Type;TR-01\n"
                "102;uid-b;Electrical Equipment;GIS;Type;TR-01\n",
                encoding="utf-8-sig",
            )
            audit = audit_model_mark_crosswalk(result, csv_path)
        self.assertEqual(audit["status"], "MODEL_MARK_CROSSWALK_INCOMPLETE")
        self.assertIn("TR-01", audit["duplicate_referenced_marks"])
        self.assertTrue(audit["missing_marks"])

    def test_model_mark_crosswalk_requires_native_identity_columns(self):
        result = calculate(BASE)
        with tempfile.TemporaryDirectory() as temp_dir:
            csv_path = Path(temp_dir) / "quantitativos.csv"
            csv_path.write_text("marca\nTR-01\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "campos de identidade"):
                audit_model_mark_crosswalk(result, csv_path)


if __name__ == "__main__":
    unittest.main()
