import math
import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from engineering.gis_substation import StudyInput, calculate


class GisSubstationCalculationsTest(unittest.TestCase):
    def setUp(self):
        self.data = StudyInput(
            project="GIS reference",
            date="2026-11-07",
            primary_voltage_kv=138.0,
            secondary_voltage_kv=13.8,
            active_load_mw=32.0,
            load_power_factor=0.95,
            critical_active_load_mw=18.0,
            transformer_count=2,
            transformer_rating_mva=25.0,
            transformer_impedance_pu=0.08,
            target_power_factor=0.98,
            frequency_hz=60.0,
            basis="test inputs",
        )

    def test_three_phase_currents_and_capacity(self):
        result = calculate(self.data)["results"]
        expected_s = 32.0 / 0.95
        self.assertAlmostEqual(result["normal_load"]["apparent_mva"], expected_s, places=10)
        self.assertAlmostEqual(result["normal_load"]["current_secondary_ka"], expected_s / (math.sqrt(3) * 13.8), places=10)
        self.assertAlmostEqual(result["transformer_ratings"]["secondary_current_each_ka"], 25.0 / (math.sqrt(3) * 13.8), places=10)
        self.assertAlmostEqual(result["normal_load"]["transformer_loading_pct"], expected_s / 50.0 * 100.0, places=10)

    def test_n_minus_one_separates_critical_and_full_load(self):
        result = calculate(self.data)["results"]["n_minus_one"]
        self.assertTrue(result["critical_load_within_nameplate"])
        self.assertFalse(result["full_load_within_nameplate"])
        self.assertAlmostEqual(result["one_transformer_critical_loading_pct"], 18.0 / 0.95 / 25.0 * 100.0, places=10)

    def test_short_circuit_uses_declared_transformer_impedance(self):
        result = calculate(self.data)["results"]["secondary_fault_estimate"]
        i_rated = 25.0 / (math.sqrt(3) * 13.8)
        self.assertAlmostEqual(result["transformer_only_one_unit_ka"], i_rated / 0.08, places=10)
        self.assertAlmostEqual(result["transformer_only_parallel_units_ka"], 2.0 * i_rated / 0.08, places=10)

    def test_power_factor_compensation_is_nonnegative_and_reduces_reactive_power(self):
        result = calculate(self.data)["results"]["power_factor_correction"]
        expected = 32.0 * (math.tan(math.acos(0.95)) - math.tan(math.acos(0.98)))
        self.assertAlmostEqual(result["estimated_compensation_mvar"], expected, places=10)
        self.assertGreater(result["estimated_compensation_mvar"], 0.0)

    def test_invalid_voltage_and_power_factor_are_rejected(self):
        with self.assertRaises(ValueError):
            calculate(StudyInput(**{**self.data.__dict__, "secondary_voltage_kv": 0.0}))
        with self.assertRaises(ValueError):
            calculate(StudyInput(**{**self.data.__dict__, "load_power_factor": 1.01}))


if __name__ == "__main__":
    unittest.main()
