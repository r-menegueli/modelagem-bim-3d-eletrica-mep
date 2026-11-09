"""Traceable equipment references and transformer-impedance sensitivity for GIS R21."""
from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
from typing import Any


REFERENCES: dict[str, Any] = {
    "project": "19 - Subestacao GIS 138/13,8 kV",
    "revision": "R21",
    "checked_on": "2026-11-08",
    "equipment": [
        {
            "equipment_id": "GIS-REF-145",
            "function": "GIS 145 kV reference",
            "manufacturer_reference": "GE Vernova F35-145 Dual Gas GIS — Fully Digital SF6 Bay brochure configuration; no project order GA selected",
            "reference_configuration": "FULLY_DIGITAL_SF6_BAY",
            "verified_catalog_facts": {
                "rated_voltage_kv": 145,
                "rated_frequency_hz": [50, 60],
                "rated_busbar_feeder_current_a": 3150,
                "rated_short_circuit_breaking_current_ka": 40,
                "rated_short_time_withstand_current_ka_3s": 40,
                "bay_width_mm": 800,
                "bay_dimensions_mm": {"width": 800, "depth": 3700, "height": 2500},
                "typical_layout_figure_callouts_mm": {
                    "line_bay_plan_callouts": [1400, 3700, 600, 5700],
                    "vertical_or_clearance_callouts": [2500, 3600],
                },
                "rated_lightning_impulse_withstand_voltage_kv_phase_to_earth": 650,
                "rated_lightning_impulse_withstand_voltage_kv_across_open_gap": 750,
            },
            "model_dimensions_m": {"width": 0.8, "depth": 3.7, "height": 2.5},
            "unverified_order_specific_details": {
                "order_ga": "NOT_AVAILABLE",
                "module_positions_and_interfaces": "NOT_AVAILABLE",
                "installation_and_maintenance_clearances": "NOT_AVAILABLE",
            },
            "dimension_status": {
                "width": "PUBLISHED_BAY_DIMENSION_REFERENCE",
                "depth": "PUBLISHED_BAY_DIMENSION_REFERENCE",
                "height": "PUBLISHED_BAY_DIMENSION_REFERENCE",
                "selected_order_ga": "NOT_SELECTED",
            },
            "layout_clearance_note": "The 2025 F35-145 brochure page 6 explicitly lists Bay Dimensions of 800 x 3700 x 2500 mm for the published bay reference. The 3700 mm and 2500 mm also appear as separate callouts in typical layout/clearance drawings; those figures alone are not order-specific GA. Module positions, terminal interfaces, and installation clearances remain unverified without the selected order GA.",
            "scope_note": "The model uses the published 800 x 3700 x 2500 mm F35 bay envelope as a reference configuration, not as a project-specific order GA. No internal OEM parts, module positions, terminal coordinates or installation/maintenance clearances are inferred.",
            "source_url": "https://www.gevernova.com/grid-solutions/sites/default/files/2026-02/F35-145-kV-Dual-Gas-GIS-EN-33282-202504-LTR-R002_0.pdf",
            "layout_reference_source_url": "https://www.gevernova.com/grid-solutions/sites/default/files/resources/products/brochures/grid-gis-l3-f35_145_kv-0169-2016_07-en.pdf",
            "product_page_url": "https://www.gevernova.com/electrification/equipment/gas-insulated-switchgear/gas-insulated-substations/f35-145-kv-dual-gas-gis",
        },
        {
            "equipment_id": "GIS-REF-145-LAYOUT",
            "function": "GIS double-busbar room-planning reference",
            "manufacturer_reference": "Hitachi Energy ELK-04, 145 kV, double busbar L layout (separate planning reference)",
            "verified_catalog_facts": {
                "overall_length_mm": 13000,
                "overall_width_mm": 3000,
                "overall_height_mm": 3500,
                "approximate_mass_t": 28,
                "rated_current_a": 3150,
                "short_time_current_ka_3s": 40,
            },
            "model_dimensions_m": None,
            "dimension_status": "REFERENCE_LAYOUT_ONLY_NOT_USED_AS_GE_F35_BAY_GEOMETRY",
            "scope_note": "This Hitachi ELK-04 L installation layout is retained only as a separate planning comparison and is not used for the GE Vernova F35-145 model dimensions or as the modeled five-bay GA.",
            "source_url": "https://www.hitachienergy.com/content/dam/documents/external/application_guide_iga_1hc0151157_ad_en.pdf",
        },
        {
            "equipment_id": "TR-40MVA-REF",
            "function": "138/13.8 kV power transformer reference",
            "manufacturer_reference": "DAX Energy TRP-EA40-DML-01",
            "verified_catalog_facts": {
                "primary_voltage_kv": 138,
                "secondary_voltage_kv": 13.8,
                "rated_power_mva": 40,
                "catalog_power_stages_mva": [30, 40],
                "vector_group": "Dyn1",
                "on_load_tap_changer": True,
                "catalog_impedance_pu": 0.11,
                "insulating_fluid": "mineral oil",
            },
            "model_dimensions_m": {"length": 6.2, "width": 3.5, "height": 3.8},
            "dimension_status": "ESTIMATED_NO_PUBLIC_OUTLINE_DRAWING_FOUND",
            "scope_note": "Product-line candidate only, not selected or approved. The Revit envelope dimensions are estimates. R8's Z=10% study assumption is preserved as a separate baseline; 11% is a catalog-candidate sensitivity, not a final transformer input.",
            "source_url": "https://dax.energy/produtos/transformadores-de-potencia",
        },
        {
            "equipment_id": "QMT-REF-17K5-2KA",
            "function": "13.8 kV switchgear panel reference",
            "manufacturer_reference": "ABB UniGear ZS1 12/17.5 kV feeder panel",
            "verified_catalog_facts": {
                "rated_voltage_class_kv": 17.5,
                "feeder_current_range_a": [1600, 2000],
                "panel_width_mm": 800,
                "panel_depth_mm": 1340,
                "panel_height_mm": 2200,
                "alternative_panel_height_mm": 2595,
                "height_note": "depends on low-voltage compartment configuration",
            },
            "model_dimensions_m": {"width": 0.8, "depth": 1.34, "height": 2.2},
            "dimension_status": "CATALOG_ENVELOPE_REFERENCE_APPROXIMATE_ASSEMBLY",
            "scope_note": "The selected dimensional row is the 1600-2000 A feeder range. The 1340 mm depth is the manual's up-to-2500 A / 31.5 kA option; no short-circuit rating or ordered switchgear lineup is selected. Internal compartments and device arrangement are not represented as a vendor GA.",
            "source_url": "https://library.e.abb.com/public/71a2961523474bf3b94276232a3f7d29/UniGear%20ZS1_IOM%20instruction%20manual_1VLM000363_RevN_EN.pdf",
        },
    ],
}


DEFAULT_INPUTS: dict[str, Any] = {
    "study_id": "SC-Z-TR-R21",
    "transformer_rating_mva": 40.0,
    "hv_voltage_kv": 138.0,
    "mv_voltage_kv": 13.8,
    "r8_impedance_assumption_pu": 0.10,
    "catalog_candidate_impedance_pu": 0.11,
    "source_model": "ideal infinite bus for a transformer-only sensitivity; source impedance is omitted",
    "limits": [
        "This is not a complete network fault study: upstream source impedance and impedance angle/X-R are missing.",
        "Motor contribution, cable/busduct impedance, grounding method, and protection clearing time are omitted.",
        "The 11% value is a product-line catalog candidate, not a selected nameplate or guaranteed test value.",
    ],
    "source_url": "https://dax.energy/produtos/transformadores-de-potencia",
}


def _finite_number(inputs: dict[str, Any], field: str) -> float:
    if field not in inputs:
        raise ValueError(f"Campo obrigatório ausente: {field}.")
    value = inputs[field]
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{field} deve ser um número JSON finito, não booleano ou texto.")
    number = float(value)
    if not math.isfinite(number):
        raise ValueError(f"{field} deve ser um número JSON finito.")
    return number


def calculate_impedance_sensitivity(inputs: dict[str, Any]) -> dict[str, Any]:
    if not isinstance(inputs, dict):
        raise ValueError("As entradas devem ser um objeto JSON.")
    rating = _finite_number(inputs, "transformer_rating_mva")
    hv_kv = _finite_number(inputs, "hv_voltage_kv")
    mv_kv = _finite_number(inputs, "mv_voltage_kv")
    z_values = {
        "R8_assumption": _finite_number(inputs, "r8_impedance_assumption_pu"),
        "catalog_candidate": _finite_number(inputs, "catalog_candidate_impedance_pu"),
    }
    if any(value <= 0 for value in (rating, hv_kv, mv_kv, *z_values.values())):
        raise ValueError("Potencia, tensões e impedâncias devem ser positivas.")
    if z_values["R8_assumption"] == z_values["catalog_candidate"]:
        raise ValueError("Os cenários de impedância precisam ser distintos.")
    if not isinstance(inputs.get("study_id"), str) or not inputs["study_id"].strip():
        raise ValueError("study_id deve ser um texto não vazio.")
    if not isinstance(inputs.get("source_url"), str) or not inputs["source_url"].strip():
        raise ValueError("source_url deve ser um texto não vazio.")

    current_base_hv_a = rating * 1000.0 / (math.sqrt(3.0) * hv_kv)
    current_base_mv_a = rating * 1000.0 / (math.sqrt(3.0) * mv_kv)
    scenarios: dict[str, Any] = {}
    for scenario_id, impedance_pu in z_values.items():
        scenarios[scenario_id] = {
            "impedance_pu": impedance_pu,
            "short_circuit_power_mva_transformer_only": rating / impedance_pu,
            "three_phase_fault_current_hv_ka_transformer_only": current_base_hv_a / impedance_pu / 1000.0,
            "three_phase_fault_current_mv_ka_transformer_only": current_base_mv_a / impedance_pu / 1000.0,
            "status": "PRELIMINARY_INFINITE_SOURCE_TRANSFORMER_ONLY",
        }
    baseline = scenarios["R8_assumption"]["three_phase_fault_current_mv_ka_transformer_only"]
    candidate = scenarios["catalog_candidate"]["three_phase_fault_current_mv_ka_transformer_only"]
    return {
        "calculation_id": inputs["study_id"],
        "inputs": inputs,
        "nominal_currents_a": {"hv": current_base_hv_a, "mv": current_base_mv_a},
        "scenarios": scenarios,
        "candidate_vs_R8_mv_fault_current_change_percent": (candidate / baseline - 1.0) * 100.0,
        "method": "I_base = S_n/(sqrt(3)*V_LL); S_sc = S_n/Z_pu; I_sc = I_base/Z_pu",
        "validation_status": "REPRODUCIBLE_PRELIMINARY_SENSITIVITY_NOT_A_COMPLETE_SHORT_CIRCUIT_STUDY",
        "source_url": inputs["source_url"],
        "not_calculated": ["complete 138 kV source + transformer + MV bus/cable fault", "protection/selectivity", "peak/asymmetrical current"],
    }


def write_outputs(output_dir: Path) -> tuple[Path, Path, Path]:
    output_dir.mkdir(parents=True, exist_ok=True)
    references_path = output_dir / "referencias-equipamentos-r21.json"
    inputs_path = output_dir / "sensibilidade-impedancia-transformador-r21-input.json"
    results_path = output_dir / "sensibilidade-impedancia-transformador-r21.json"
    report_path = output_dir / "sensibilidade-impedancia-transformador-r21.md"
    references_path.write_text(json.dumps(REFERENCES, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    inputs_path.write_text(json.dumps(DEFAULT_INPUTS, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    result = calculate_impedance_sensitivity(DEFAULT_INPUTS)
    results_path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    r8 = result["scenarios"]["R8_assumption"]
    product = result["scenarios"]["catalog_candidate"]
    report = "\n".join([
        "# Sensibilidade da impedância do transformador — R21", "",
        "## Entradas e método", "",
        f"- Transformador de referência: 40 MVA, {DEFAULT_INPUTS['hv_voltage_kv']} / {DEFAULT_INPUTS['mv_voltage_kv']} kV.",
        f"- Caso-base preservado da R8: Z = {DEFAULT_INPUTS['r8_impedance_assumption_pu'] * 100:.1f}% (hipótese de estudo).",
        f"- Sensibilidade candidata de catálogo DAX TRP-EA40-DML-01: Z = {DEFAULT_INPUTS['catalog_candidate_impedance_pu'] * 100:.1f}%.",
        "- Fonte equivalente ideal; corrente calculada somente com a impedância do transformador.",
        f"- Método: `{result['method']}`.", "",
        "## Resultados", "",
        "| Caso | Z (pu) | Icc no lado 13,8 kV (kA RMS simétrica) | Scc (MVA) |",
        "|---|---:|---:|---:|",
        f"| Base R8 | {r8['impedance_pu']:.2f} | {r8['three_phase_fault_current_mv_ka_transformer_only']:.3f} | {r8['short_circuit_power_mva_transformer_only']:.1f} |",
        f"| Candidato catálogo | {product['impedance_pu']:.2f} | {product['three_phase_fault_current_mv_ka_transformer_only']:.3f} | {product['short_circuit_power_mva_transformer_only']:.1f} |",
        f"| Variação do candidato vs base |  | {result['candidate_vs_R8_mv_fault_current_change_percent']:.2f}% |  |", "",
        "## Limites", "",
        *[f"- {item}" for item in DEFAULT_INPUTS["limits"]],
        "- Não usar estes resultados para escolher disjuntores ou declarar seletividade.",
        "- O estudo completo depende da impedância de curto da rede, relações R/X, cabos/barramentos, motores e sequência de proteção.",
        "",
        f"Fonte do valor candidato: [DAX Energy — transformadores de potência]({DEFAULT_INPUTS['source_url']}).",
        "",
    ])
    report_path.write_text(report, encoding="utf-8")
    return references_path, results_path, report_path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True, help="Pasta dos entregáveis R21")
    args = parser.parse_args()
    paths = write_outputs(args.output)
    result = calculate_impedance_sensitivity(DEFAULT_INPUTS)
    print(json.dumps({"files": [str(path) for path in paths], "summary": result}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
