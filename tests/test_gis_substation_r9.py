import json
import unittest
from pathlib import Path

from scripts.engineering.gis_substation_r9 import calculate


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "projetos" / "19-subestacao-gis"


class GISSubstationR9Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.inputs = json.loads((PROJECT / "dados" / "reconstrucao-r9" / "estudo-r9.json").read_text(encoding="utf-8"))
        cls.r8 = json.loads((PROJECT / "dados" / "reconstrucao-r8" / "resultados.json").read_text(encoding="utf-8"))
        cls.result = calculate(cls.inputs, cls.r8)

    def test_r8_base_results_are_preserved(self):
        self.assertEqual(self.result["preserved_r8_calculation_ids"], [x["calc_id"] for x in self.r8["calculations"]])
        self.assertAlmostEqual(self.r8["calculations"][0]["result"], 56 / 0.95)

    def test_cable_drop_and_losses_scale_with_route_length(self):
        rows = [x for x in self.result["cable_sensitivities"] if x["active_power_mw"] == 6.0]
        self.assertEqual([x["length_km"] for x in rows], [0.1, 0.25, 0.5])
        self.assertTrue(all(a["voltage_drop_percent"] < b["voltage_drop_percent"] for a, b in zip(rows, rows[1:])))
        self.assertTrue(all(a["three_phase_copper_loss_kw"] < b["three_phase_copper_loss_kw"] for a, b in zip(rows, rows[1:])))

    def test_fault_sensitivity_includes_source_transformer_and_feeder(self):
        row = next(x for x in self.result["short_circuit_sensitivities"] if x["source_short_circuit_mva_assumed"] == 1000 and x["transformers_in_parallel_assumed"] == 1 and x["cable_length_km_assumed"] == 0.25)
        self.assertGreater(row["bus_fault_current_ka_symmetric_rms"], row["remote_fault_current_ka_symmetric_rms"])
        two = next(x for x in self.result["short_circuit_sensitivities"] if x["source_short_circuit_mva_assumed"] == 1000 and x["transformers_in_parallel_assumed"] == 2 and x["cable_length_km_assumed"] == 0.25)
        self.assertGreater(two["bus_fault_current_ka_symmetric_rms"], row["bus_fault_current_ka_symmetric_rms"])

    def test_every_quality_check_passes_and_ampacity_is_not_asserted(self):
        self.assertTrue(all(self.result["quality_checks"].values()))
        self.assertTrue(all("AMPACITY" in x["status"] for x in self.result["cable_sensitivities"]))


if __name__ == "__main__":
    unittest.main()
