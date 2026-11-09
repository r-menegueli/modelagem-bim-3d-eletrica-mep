from __future__ import annotations

import hashlib
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

    def test_shared_core_refactor_preserves_every_legacy_r1_to_r8_output(self) -> None:
        baseline_path = ROOT / "tests" / "fixtures" / "gis_auxiliary_legacy_outputs.sha256.json"
        baseline = json.loads(baseline_path.read_text(encoding="utf-8"))
        input_paths = sorted((ROOT / "projetos" / "19-subestacao-gis" / "dados").glob("servicos-auxiliares-r*.json"))
        self.assertEqual({path.name for path in input_paths}, set(baseline["outputs"]))
        total_rows = 0
        for path in input_paths:
            result = calculate(json.loads(path.read_text(encoding="utf-8")))
            canonical = json.dumps(
                result,
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
                allow_nan=False,
            ).encode("utf-8")
            expected = baseline["outputs"][path.name]
            self.assertEqual(hashlib.sha256(canonical).hexdigest(), expected["sha256"], path.name)
            self.assertEqual(len(result["calculations"]), expected["calculation_rows"], path.name)
            total_rows += len(result["calculations"])
        self.assertEqual(total_rows, 184)

    def test_r20_r8_baseline_reproduction_matches_current_calculation(self) -> None:
        source = ROOT / "projetos" / "19-subestacao-gis" / "dados" / "servicos-auxiliares-r8.json"
        saved = (
            ROOT
            / "projetos"
            / "19-subestacao-gis"
            / "dados"
            / "reconstrucao-r20"
            / "servicos-auxiliares-resultados-r8.json"
        )
        base = json.loads(source.read_text(encoding="utf-8"))
        self.assertEqual(json.loads(saved.read_text(encoding="utf-8")), calculate(base))
        self.assertEqual(json.loads(saved.read_text(encoding="utf-8"))["revision"], "R8")

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
