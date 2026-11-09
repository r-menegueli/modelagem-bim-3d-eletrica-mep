import csv
import json
import tempfile
import unittest
from pathlib import Path

from scripts.engineering.distribution import calculate_demand_schedule, write_demand_schedule


EXAMPLE = Path(__file__).resolve().parents[1] / "scripts" / "engineering" / "examples" / "demand-schedule.example.json"


class ElectricalDistributionTests(unittest.TestCase):
    def setUp(self):
        self.data = json.loads(EXAMPLE.read_text(encoding="utf-8"))

    def test_example_is_traceable_and_calculates_aggregate_demand(self):
        result = calculate_demand_schedule(self.data)
        calculation = result["calculation"]
        self.assertEqual(result["project"], "LIBRARY-EXAMPLE-NOT-A-REAL-FACILITY")
        self.assertEqual(calculation["calc_id"], "EX-DIST-001")
        self.assertEqual(calculation["inputs"]["active_power_unit"], "kW")
        self.assertEqual(calculation["unit"], "kW, kvar, kVA, PF")
        self.assertEqual(calculation["status"], "ILLUSTRATIVE_EXPLICIT_INPUTS")
        self.assertAlmostEqual(calculation["result"]["diversified_active_power_kw"], 70.0)
        self.assertAlmostEqual(calculation["result"]["diversified_reactive_power_kvar"], 37.5)
        self.assertEqual(len(calculation["result"]["lines"]), 2)

    def test_writer_exports_json_and_line_csv(self):
        with tempfile.TemporaryDirectory() as directory:
            result = write_demand_schedule(self.data, Path(directory))
            json_path = Path(directory) / "demand-schedule-results.json"
            csv_path = Path(directory) / "demand-schedule-lines.csv"
            self.assertTrue(json_path.exists())
            self.assertTrue(csv_path.exists())
            saved = json.loads(json_path.read_text(encoding="utf-8"))
            self.assertEqual(saved["calculation"]["calc_id"], result["calculation"]["calc_id"])
            with csv_path.open(encoding="utf-8-sig", newline="") as stream:
                rows = list(csv.DictReader(stream, delimiter=";"))
            self.assertEqual([row["equipment_id"] for row in rows], ["MCC-01", "LGT-01"])

    def test_unknown_units_or_missing_load_source_are_rejected(self):
        altered = dict(self.data)
        altered["active_power_unit"] = "MW"
        with self.assertRaisesRegex(ValueError, "units must explicitly"):
            calculate_demand_schedule(altered)

        altered = json.loads(json.dumps(self.data))
        del altered["loads"][0]["source_or_assumption"]
        with self.assertRaisesRegex(ValueError, "source_or_assumption"):
            calculate_demand_schedule(altered)

    def test_schedule_can_leave_reactive_inputs_unknown_without_defaulting_them(self):
        data = json.loads(json.dumps(self.data))
        for row in data["loads"]:
            row.pop("power_factor")
            row.pop("reactive_direction")
        result = calculate_demand_schedule(data)["calculation"]
        self.assertAlmostEqual(result["result"]["diversified_active_power_kw"], 70.0)
        self.assertIsNone(result["result"]["diversified_reactive_power_kvar"])
        self.assertIsNone(result["result"]["diversified_apparent_power_kva"])
        self.assertIn("desconhecidos", result["method"])
        self.assertIn("não calculados", result["unit"])

    def test_schedule_rejects_incomplete_reactive_input_pair(self):
        data = json.loads(json.dumps(self.data))
        data["loads"][0].pop("reactive_direction")
        with self.assertRaisesRegex(ValueError, "power_factor and reactive_direction together"):
            calculate_demand_schedule(data)


if __name__ == "__main__":
    unittest.main()
