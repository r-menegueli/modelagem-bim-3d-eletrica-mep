import copy
import csv
import json
import tempfile
import unittest
from pathlib import Path

from scripts.engineering.gis_substation_study import calculate, write_outputs


ROOT = Path(__file__).resolve().parents[1]
BASE = json.loads((ROOT / "projetos/19-subestacao-gis/dados/base-de-projeto.json").read_text(encoding="utf-8"))


class GisSubstationStudyTest(unittest.TestCase):
    def test_load_currents_and_transformer_ratings(self):
        result = calculate(BASE)
        rows = {item["calc_id"]: item for item in result["calculations"]}
        self.assertAlmostEqual(rows["BP-01"]["result"], 56.0 / 0.95)
        self.assertAlmostEqual(rows["BP-06"]["result"], 40.0 / (3**0.5 * 13.8))
        self.assertAlmostEqual(rows["BP-15"]["result"], 28.0 / 0.95 / (3**0.5 * 13.8))

    def test_n_minus_one_requires_shedding_and_retains_critical_load(self):
        result = calculate(BASE)
        rows = {item["calc_id"]: item for item in result["calculations"]}
        self.assertAlmostEqual(rows["BP-08"]["result"], 32.0 / 0.95 / 40.0 * 100.0)
        self.assertAlmostEqual(rows["BP-09"]["result"], 56.0 / 0.95 / 40.0 * 100.0)
        self.assertAlmostEqual(rows["BP-10"]["result"], 24.0)
        self.assertTrue(result["quality_checks"]["critical_load_within_one_transformer_nameplate"])
        self.assertTrue(result["quality_checks"]["full_load_requires_shedding_in_n_minus_one"])

    def test_assumed_transformer_short_circuit_is_not_marked_as_device_selection(self):
        result = calculate(BASE)
        rows = {item["calc_id"]: item for item in result["calculations"]}
        self.assertAlmostEqual(rows["BP-12"]["result"], 40.0 / (3**0.5 * 13.8) / 0.10)
        self.assertIn("Fonte ideal", rows["BP-12"]["source_or_assumption"])
        self.assertTrue(any(item["calc_id"] == "SC-HV-01" for item in result["not_calculated"]))

    def test_feeder_sums_are_validated(self):
        altered = copy.deepcopy(BASE)
        altered["load_scenarios"]["feeders"][0]["active_mw"] = 7.0
        with self.assertRaises(ValueError):
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
            self.assertIn("PROT-01", {item["calc_id"] for item in result["not_calculated"]})


if __name__ == "__main__":
    unittest.main()
