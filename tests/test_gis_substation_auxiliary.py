from __future__ import annotations

import json
import math
import unittest
from pathlib import Path

from scripts.engineering.gis_substation_auxiliary import calculate


ROOT = Path(__file__).resolve().parents[1]
INPUT = ROOT / "projetos" / "19-subestacao-gis" / "dados" / "servicos-auxiliares-r1.json"


class GisSubstationAuxiliaryTest(unittest.TestCase):
    def setUp(self) -> None:
        self.base = json.loads(INPUT.read_text(encoding="utf-8"))

    def test_ac_loads_and_candidate_transformer_are_reproducible(self) -> None:
        result = calculate(self.base)
        values = {item["calc_id"]: item["result"] for item in result["calculations"]}
        self.assertTrue(math.isclose(values["AUX-AC-11"], 57.9377, abs_tol=1e-9))
        self.assertTrue(math.isclose(values["AUX-AC-12"], values["AUX-AC-11"] / 0.9, rel_tol=1e-12))
        self.assertLess(values["AUX-AC-14"], 80.0)
        self.assertTrue(result["quality_checks"]["ac_candidate_below_assumed_utilization_limit"])

    def test_dc_battery_capacity_includes_mission_and_correction_factors(self) -> None:
        result = calculate(self.base)
        values = {item["calc_id"]: item["result"] for item in result["calculations"]}
        self.assertGreater(values["DC-03"], values["DC-01"] + values["DC-02"])
        self.assertEqual(values["DC-04"], 200.0)
        self.assertTrue(result["quality_checks"]["dc_candidate_capacity_covers_calculated_study_capacity"])

    def test_ups_rating_and_energy_are_separate_checks(self) -> None:
        result = calculate(self.base)
        values = {item["calc_id"]: item["result"] for item in result["calculations"]}
        self.assertTrue(math.isclose(values["UPS-01"], 1.875, rel_tol=1e-12))
        self.assertTrue(math.isclose(values["UPS-02"], 62.5, rel_tol=1e-12))
        self.assertGreater(values["UPS-03"], 1.5)
        self.assertTrue(result["quality_checks"]["ups_candidate_rating_covers_study_apparent_load"])

    def test_duplicate_auxiliary_load_id_is_rejected(self) -> None:
        self.base["ac_system"]["loads"].append(dict(self.base["ac_system"]["loads"][0]))
        with self.assertRaisesRegex(ValueError, "duplicada"):
            calculate(self.base)

    def test_invalid_demand_factor_is_rejected(self) -> None:
        self.base["ac_system"]["loads"][0]["demand_factor_assumed"] = 1.1
        with self.assertRaisesRegex(ValueError, "inválida"):
            calculate(self.base)


if __name__ == "__main__":
    unittest.main()
