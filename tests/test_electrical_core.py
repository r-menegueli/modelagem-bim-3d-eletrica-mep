import math
import unittest

from scripts.engineering.electrical_core import (
    DemandLoad,
    Provenance,
    adiabatic_conductor_withstand,
    aggregate_demand_schedule,
    ampacity_after_corrections,
    balanced_three_phase_cable_performance,
    battery_autonomy,
    compare_interrupting_rating,
    energy_from_average_power,
    hydro_generation,
    identified_calculation_record,
    motor_electrical_input,
    power_triangle_from_active_and_reactive_power,
    power_triangle_from_active_power_and_power_factor,
    pv_energy_yield,
    result_record,
    required_reactive_compensation,
    short_circuit_from_series_impedances,
    single_phase_current,
    three_phase_line_current,
    transformer_loading,
)


class ElectricalCoreTests(unittest.TestCase):
    def test_power_triangle_and_lagging_or_leading_q(self):
        lagging = power_triangle_from_active_power_and_power_factor(
            active_power_kw=100.0, power_factor=0.8
        )
        leading = power_triangle_from_active_power_and_power_factor(
            active_power_kw=100.0,
            power_factor=0.8,
            reactive_direction="leading",
        )
        self.assertAlmostEqual(lagging.apparent_power_kva, 125.0)
        self.assertAlmostEqual(lagging.reactive_power_kvar, 75.0)
        self.assertAlmostEqual(leading.reactive_power_kvar, -75.0)
        self.assertAlmostEqual(
            power_triangle_from_active_and_reactive_power(
                active_power_kw=100.0, reactive_power_kvar=-75.0
            ).power_factor,
            0.8,
        )

    def test_three_phase_current_uses_line_voltage_and_kva(self):
        result = three_phase_line_current(apparent_power_kva=1000.0, line_voltage_v=400.0)
        self.assertAlmostEqual(result.line_current_a, 1000.0 * 1000.0 / (math.sqrt(3.0) * 400.0))

    def test_single_phase_current_uses_line_to_neutral_voltage_and_va(self):
        result = single_phase_current(apparent_power_va=150.0 / 0.9, voltage_v=220.0)
        self.assertAlmostEqual(result.current_a, (150.0 / 0.9) / 220.0)
        self.assertAlmostEqual(result.current_a, 0.7575757575757576)

    def test_balanced_cable_voltage_drop_and_resistive_loss_reference_case(self):
        result = balanced_three_phase_cable_performance(
            active_power_kw=100.0,
            reactive_power_kvar=0.0,
            line_voltage_v=400.0,
            resistance_ohm_per_km=0.1,
            reactance_ohm_per_km=0.05,
            one_way_length_km=0.2,
        )
        self.assertAlmostEqual(result.line_current_a, 100_000.0 / (math.sqrt(3.0) * 400.0))
        self.assertAlmostEqual(result.approximate_voltage_drop_v, 5.0)
        self.assertAlmostEqual(result.approximate_voltage_drop_percent, 1.25)
        self.assertAlmostEqual(result.three_phase_resistive_loss_kw, 1.25)
        self.assertIn("NO_AMPACITY_CHECK", result.provenance.status)

    def test_leading_power_factor_can_produce_negative_approximate_drop(self):
        result = balanced_three_phase_cable_performance(
            active_power_kw=100.0,
            reactive_power_kvar=-75.0,
            line_voltage_v=400.0,
            resistance_ohm_per_km=0.1,
            reactance_ohm_per_km=0.2,
            one_way_length_km=0.2,
        )
        self.assertLess(result.approximate_voltage_drop_v, 0.0)
        self.assertGreater(result.approximate_load_voltage_v, 400.0)

    def test_transformer_parallel_loading_assumes_equal_share(self):
        result = transformer_loading(
            load_apparent_power_kva=35_000.0,
            unit_rating_kva=20_000.0,
            parallel_unit_count=2,
        )
        self.assertAlmostEqual(result.installed_capacity_kva, 40_000.0)
        self.assertAlmostEqual(result.equal_share_load_per_unit_kva, 17_500.0)
        self.assertAlmostEqual(result.equal_share_loading_percent, 87.5)
        self.assertFalse(result.one_unit_exceeds_nameplate_under_equal_share)
        self.assertEqual(result.provenance.status, "EQUAL_LOAD_SHARING_ASSUMED")

    def test_energy_integrates_average_power_over_duration(self):
        result = energy_from_average_power(average_power_kw=100.0, duration_h=10.0)
        self.assertEqual(result.energy_kwh, 1000.0)

    def test_motor_input_power_and_line_current(self):
        result = motor_electrical_input(
            shaft_output_power_kw=37.0,
            efficiency_fraction=0.9,
            power_factor=0.85,
            line_voltage_v=400.0,
        )
        self.assertAlmostEqual(result.electrical_input_power_kw, 37.0 / 0.9)
        self.assertAlmostEqual(result.electrical_input_apparent_power_kva, 37.0 / 0.9 / 0.85)
        self.assertAlmostEqual(
            result.line_current_a,
            result.electrical_input_apparent_power_kva * 1000.0 / (math.sqrt(3.0) * 400.0),
        )

    def test_pv_yield_uses_kwp_peak_sun_hours_and_performance_ratio(self):
        result = pv_energy_yield(
            installed_peak_power_kwp=100.0,
            peak_sun_hours_h_per_day=5.0,
            performance_ratio_fraction=0.8,
            duration_days=30.0,
        )
        self.assertEqual(result.daily_energy_kwh, 400.0)
        self.assertEqual(result.total_energy_kwh, 12_000.0)
        self.assertIn("NOT_A_SITE_FORECAST", result.provenance.status)

    def test_hydro_generation_power_and_energy_reference_case(self):
        result = hydro_generation(
            flow_m3_per_s=2.0,
            net_head_m=10.0,
            overall_efficiency_fraction=0.8,
            duration_h=24.0,
        )
        expected_kw = 1000.0 * 9.80665 * 2.0 * 10.0 * 0.8 / 1000.0
        self.assertAlmostEqual(result.electrical_output_power_kw, expected_kw)
        self.assertAlmostEqual(result.electrical_energy_kwh, expected_kw * 24.0)

    def test_battery_autonomy_includes_declared_usable_capacity_factors(self):
        result = battery_autonomy(
            rated_capacity_ah=200.0,
            nominal_voltage_v=125.0,
            dc_load_power_kw=2.0,
            depth_of_discharge_fraction=0.8,
            capacity_retention_fraction=0.9,
            discharge_efficiency_fraction=0.85,
        )
        self.assertEqual(result.nominal_energy_kwh, 25.0)
        self.assertAlmostEqual(result.usable_energy_kwh, 15.3)
        self.assertAlmostEqual(result.autonomy_h, 7.65)
        self.assertAlmostEqual(result.constant_power_load_current_a, 16.0)
        self.assertIn("NO_DISCHARGE_CURVE", result.provenance.status)

    def test_short_circuit_uses_explicit_per_phase_series_impedance(self):
        result = short_circuit_from_series_impedances(
            line_voltage_v=13_800.0,
            source_resistance_ohm_per_phase=0.2,
            source_reactance_ohm_per_phase=0.8,
            transformer_resistance_ohm_per_phase=0.1,
            transformer_reactance_ohm_per_phase=1.5,
            feeder_resistance_ohm_per_phase=0.5,
            feeder_reactance_ohm_per_phase=0.7,
            provenance=Provenance("Explicit equivalent impedances for test", "TEST_CASE"),
        )
        expected_ka = 13_800.0 / (math.sqrt(3.0) * math.hypot(0.8, 3.0)) / 1000.0
        self.assertAlmostEqual(result.total_resistance_ohm_per_phase, 0.8)
        self.assertAlmostEqual(result.total_reactance_ohm_per_phase, 3.0)
        self.assertAlmostEqual(result.symmetrical_rms_short_circuit_current_ka, expected_ka)
        self.assertEqual(result.provenance.status, "TEST_CASE")

    def test_result_record_flattens_source_status(self):
        result = energy_from_average_power(
            average_power_kw=5.0,
            duration_h=2.0,
            provenance=Provenance("metered example", "MEASURED_INPUT_CALCULATION"),
        )
        record = result_record(result)
        self.assertEqual(record["energy_kwh"], 10.0)
        self.assertEqual(record["source_or_assumption"], "metered example")
        self.assertEqual(record["status"], "MEASURED_INPUT_CALCULATION")

    def test_identified_calculation_record_contains_inputs_method_units_and_limits(self):
        result = energy_from_average_power(
            average_power_kw=5.0,
            duration_h=2.0,
            provenance=Provenance("explicit example", "REFERENCE_CASE"),
        )
        record = identified_calculation_record(
            calc_id="TEST-ENERGY-01",
            inputs={"average_power_kw": 5.0, "duration_h": 2.0},
            method="E=P*t",
            result=result,
            unit="kWh",
            limitations=("average power is assumed constant",),
        )
        self.assertEqual(record["calc_id"], "TEST-ENERGY-01")
        self.assertEqual(record["inputs"]["duration_h"], 2.0)
        self.assertEqual(record["method"], "E=P*t")
        self.assertEqual(record["result"]["energy_kwh"], 10.0)
        self.assertEqual(record["unit"], "kWh")
        self.assertEqual(record["limitations"], ["average power is assumed constant"])
        self.assertEqual(record["status"], "REFERENCE_CASE")

    def test_demand_schedule_requires_and_applies_explicit_factors_and_q_direction(self):
        result = aggregate_demand_schedule(
            loads=(
                DemandLoad("MCC-01", 100.0, 0.8, 0.5, "lagging"),
                DemandLoad("LGT-01", 40.0, 1.0, 0.5, "leading"),
            ),
            provenance=Provenance("declared example schedule", "REFERENCE_CASE"),
        )
        self.assertEqual(result.connected_active_power_kw, 140.0)
        self.assertEqual(result.diversified_active_power_kw, 70.0)
        self.assertAlmostEqual(result.diversified_reactive_power_kvar, 37.5)
        self.assertAlmostEqual(result.diversified_apparent_power_kva, math.hypot(70.0, 37.5))
        self.assertAlmostEqual(result.aggregate_power_factor, 70.0 / math.hypot(70.0, 37.5))
        self.assertEqual([line.equipment_id for line in result.lines], ["MCC-01", "LGT-01"])
        self.assertEqual(result.provenance.status, "REFERENCE_CASE")

    def test_demand_schedule_allows_active_power_only_without_inventing_q_or_s(self):
        result = aggregate_demand_schedule(
            loads=(
                DemandLoad("AUX-01", 10.0, None, 0.5, None),
                DemandLoad("AUX-02", 4.0, None, 1.0, None),
            )
        )
        self.assertAlmostEqual(result.connected_active_power_kw, 14.0)
        self.assertAlmostEqual(result.diversified_active_power_kw, 9.0)
        self.assertIsNone(result.diversified_reactive_power_kvar)
        self.assertIsNone(result.diversified_apparent_power_kva)
        self.assertIsNone(result.lines[0].demand_apparent_power_kva)

    def test_demand_schedule_rejects_only_one_of_power_factor_and_direction(self):
        with self.assertRaisesRegex(ValueError, "both power_factor and reactive_direction"):
            aggregate_demand_schedule(
                loads=(DemandLoad("AUX-01", 1.0, 0.9, 1.0, None),)
            )

    def test_ampacity_multipliers_are_named_and_based_on_supplied_rating(self):
        result = ampacity_after_corrections(
            reference_ampacity_a=300.0,
            correction_factors=(
                {"name": "ambient", "multiplier": 0.8, "source_or_assumption": "declared table row"},
                {"name": "grouping", "multiplier": 0.9, "source_or_assumption": "declared study input"},
            ),
        )
        self.assertAlmostEqual(result.combined_factor, 0.72)
        self.assertAlmostEqual(result.corrected_ampacity_a, 216.0)
        self.assertEqual(len(result.applied_correction_factors), 2)
        self.assertIn("USER_SUPPLIED_REFERENCE", result.provenance.status)

    def test_adiabatic_withstand_uses_explicit_k_area_and_duration(self):
        result = adiabatic_conductor_withstand(
            k_a_sqrt_s_per_mm2=143.0,
            conductor_area_mm2=95.0,
            fault_duration_s=0.25,
        )
        expected_i2t = (143.0 * 95.0) ** 2
        self.assertAlmostEqual(result.withstand_current_a, 143.0 * 95.0 / math.sqrt(0.25))
        self.assertAlmostEqual(result.withstand_i2t_a2s, expected_i2t)
        self.assertIn("EXPLICIT_K", result.provenance.status)

    def test_interrupting_rating_screen_requires_matching_bases_and_voltage(self):
        result = compare_interrupting_rating(
            calculated_fault_current_ka=24.0,
            device_interrupting_rating_ka=25.0,
            fault_current_basis="symmetrical_rms_3_phase",
            device_rating_basis="symmetrical_rms_3_phase",
            bus_nominal_voltage_kv=13.8,
            device_rated_voltage_kv=17.5,
        )
        self.assertEqual(result.rating_margin_ka, 1.0)
        self.assertTrue(result.current_rating_sufficient)
        self.assertTrue(result.voltage_rating_sufficient)
        self.assertEqual(result.status, "NUMERIC_RATINGS_MEET_DECLARED_SCREEN_ONLY")

        incomparable = compare_interrupting_rating(
            calculated_fault_current_ka=24.0,
            device_interrupting_rating_ka=25.0,
            fault_current_basis="symmetrical_rms_3_phase",
            device_rating_basis="making_peak",
            bus_nominal_voltage_kv=13.8,
            device_rated_voltage_kv=12.0,
        )
        self.assertIsNone(incomparable.rating_margin_ka)
        self.assertIsNone(incomparable.current_rating_sufficient)
        self.assertFalse(incomparable.voltage_rating_sufficient)
        self.assertEqual(incomparable.status, "CURRENT_BASES_NOT_COMPARABLE")

    def test_power_factor_correction_matches_independent_triangle_reference(self):
        result = required_reactive_compensation(
            active_power_kw=1000.0,
            initial_power_factor=0.8,
            target_power_factor=0.95,
        )
        expected = 1000.0 * (math.tan(math.acos(0.8)) - math.tan(math.acos(0.95)))
        self.assertAlmostEqual(result.required_reactive_compensation_kvar, expected)
        self.assertGreater(result.required_reactive_compensation_kvar, 0.0)

    def test_rejects_invalid_or_ambiguous_inputs(self):
        with self.assertRaises(ValueError):
            power_triangle_from_active_power_and_power_factor(active_power_kw=1.0, power_factor=0.0)
        with self.assertRaises(ValueError):
            power_triangle_from_active_power_and_power_factor(active_power_kw=0.0, power_factor=0.9)
        with self.assertRaises(ValueError):
            three_phase_line_current(apparent_power_kva=1.0, line_voltage_v=float("nan"))
        with self.assertRaises(ValueError):
            single_phase_current(apparent_power_va=1.0, voltage_v=0.0)
        with self.assertRaises(ValueError):
            single_phase_current(apparent_power_va=-1.0, voltage_v=220.0)
        with self.assertRaises(ValueError):
            three_phase_line_current(apparent_power_kva="1000", line_voltage_v=400.0)
        with self.assertRaises(ValueError):
            three_phase_line_current(apparent_power_kva=1e308, line_voltage_v=1e-300)
        with self.assertRaises(ValueError):
            balanced_three_phase_cable_performance(
                active_power_kw=1.0,
                reactive_power_kvar=0.0,
                line_voltage_v=400.0,
                resistance_ohm_per_km=-0.1,
                reactance_ohm_per_km=0.1,
                one_way_length_km=0.1,
            )
        with self.assertRaises(ValueError):
            transformer_loading(load_apparent_power_kva=100.0, unit_rating_kva=50.0, parallel_unit_count=0)
        with self.assertRaises(ValueError):
            motor_electrical_input(
                shaft_output_power_kw=1.0,
                efficiency_fraction=1.01,
                power_factor=0.9,
                line_voltage_v=400.0,
            )
        with self.assertRaises(ValueError):
            short_circuit_from_series_impedances(
                line_voltage_v=13_800.0,
                source_resistance_ohm_per_phase=0.0,
                source_reactance_ohm_per_phase=0.0,
                transformer_resistance_ohm_per_phase=0.0,
                transformer_reactance_ohm_per_phase=0.0,
                feeder_resistance_ohm_per_phase=0.0,
                feeder_reactance_ohm_per_phase=0.0,
            )
        with self.assertRaises(ValueError):
            energy_from_average_power(
                average_power_kw=1.0,
                duration_h=1.0,
                provenance=Provenance(source_or_assumption=None),  # type: ignore[arg-type]
            )
        with self.assertRaises(ValueError):
            result_record({"provenance": Provenance()})
        with self.assertRaises(ValueError):
            aggregate_demand_schedule(loads=(DemandLoad("L1", 10.0, 0.9, 1.1, "lagging"),))
        with self.assertRaises(ValueError):
            aggregate_demand_schedule(
                loads=(DemandLoad("L1", 10.0, 0.9, 1.0, "lagging"), DemandLoad("L1", 2.0, 1.0, 1.0, "lagging"))
            )
        with self.assertRaises(ValueError):
            ampacity_after_corrections(reference_ampacity_a=100.0, correction_factors=())
        with self.assertRaises(ValueError):
            adiabatic_conductor_withstand(k_a_sqrt_s_per_mm2=143.0, conductor_area_mm2=0.0, fault_duration_s=1.0)
        with self.assertRaises(ValueError):
            compare_interrupting_rating(
                calculated_fault_current_ka=10.0,
                device_interrupting_rating_ka=12.0,
                fault_current_basis="",
                device_rating_basis="symmetrical_rms_3_phase",
                bus_nominal_voltage_kv=13.8,
                device_rated_voltage_kv=17.5,
            )


if __name__ == "__main__":
    unittest.main()
