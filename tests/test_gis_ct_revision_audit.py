import csv
import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "projetos" / "19-subestacao-gis" / "dados"
EXPECTED_MARKS = {
    "GIS-L01-CT",
    "GIS-T01-CT",
    "GIS-BC01-CT",
    "GIS-T02-CT",
    "GIS-L02-CT",
}


def revision_ct_marks(revision: str) -> set[str]:
    path = DATA / f"reconstrucao-{revision}" / "quantitativos-modelo.csv"
    with path.open(encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream, delimiter=";"))
    return {
        row["marca"]
        for row in rows
        if row.get("familia") == "GIS_CurrentTransformer"
        and row.get("tipo") == "GIS_CT_145KV_STUDY"
    }


class GISTransformerCurrentAuditTests(unittest.TestCase):
    def test_r8_to_r9_missing_coupler_placeholder_and_r19_restoration_are_evidenced(self):
        expected_r9 = EXPECTED_MARKS - {"GIS-BC01-CT"}
        self.assertEqual(revision_ct_marks("r8"), EXPECTED_MARKS)
        self.assertEqual(revision_ct_marks("r9"), expected_r9)
        self.assertEqual(revision_ct_marks("r19"), EXPECTED_MARKS)

        for revision, expected_count in (("r8", 5), ("r9", 4), ("r19", 5)):
            audit_path = DATA / f"reconstrucao-{revision}" / f"auditoria-modelo-{revision}.json"
            audit = json.loads(audit_path.read_text(encoding="utf-8"))
            family_counts = audit["family_type_counts"]
            self.assertEqual(family_counts["GIS_CurrentTransformer | GIS_CT_145KV_STUDY"], expected_count)

    def test_r20_ct_audit_states_placeholder_and_protection_limits(self):
        report = (
            DATA
            / "reconstrucao-r20"
            / "auditoria-transformadores-corrente-r8-r9-r19.md"
        ).read_text(encoding="utf-8")
        self.assertIn("a ausência do placeholder `GIS-BC01-CT`", report)
        self.assertIn("não comprova cinco conjuntos de TCs adequados", report)
        self.assertIn("zero `AllRefs` externos", report)
        self.assertIn("a contagem de cinco continua sendo uma conclusão sobre os arquivos históricos R19", report)


if __name__ == "__main__":
    unittest.main()
