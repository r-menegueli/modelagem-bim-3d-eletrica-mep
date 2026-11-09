from __future__ import annotations

import math
import json
from pathlib import Path
import tempfile
import unittest

from scripts.engineering.automation import (
    EquipmentRelation,
    IOPoint,
    build_control_architecture,
    control_architecture_from_mapping,
    write_control_architecture,
)
from scripts.engineering.electrical_core import Provenance, identified_calculation_record
from scripts.engineering.energy import (
    EnergyPeriod,
    energy_balance,
    energy_efficiency,
    energy_from_operating_periods,
)
from scripts.engineering.essential_services import (
    BatteryMissionEvent,
    battery_mission_capacity,
    generator_capacity_estimate,
    ups_capacity_estimate,
)
from scripts.engineering.motors import (
    MotorOperatingPeriod,
    drive_input_estimate,
    motor_energy_by_schedule,
)


class MotorModuleTests(unittest.TestCase):
    def test_schedule_energy_against_independent_power_time_reference(self):
        result = motor_energy_by_schedule(
            motor_id="M-01",
            rated_shaft_output_power_kw=15.0,
            efficiency_fraction=0.90,
            power_factor=0.85,
            line_voltage_v=400.0,
            periods=(
                MotorOperatingPeriod("P1", 2.0, 0.5),
                MotorOperatingPeriod("P2", 1.0, 1.0),
            ),
        )
        independent_input_kw = 15.0 * (0.5 * 2.0 + 1.0 * 1.0) / 0.90
        self.assertAlmostEqual(result.total_electrical_energy_kwh, independent_input_kw)
        self.assertAlmostEqual(result.total_operating_time_h, 3.0)
        self.assertEqual(result.provenance.status, "SCHEDULE_ENERGY_ESTIMATE_CONSTANT_EFFICIENCY_AND_POWER_FACTOR")
        self.assertGreater(result.periods[1].line_current_a, result.periods[0].line_current_a)

    def test_drive_loss_and_input_current(self):
        result = drive_input_estimate(
            shaft_output_power_kw=7.5,
            motor_efficiency_fraction=0.90,
            drive_efficiency_fraction=0.95,
            drive_input_power_factor=0.96,
            line_voltage_v=400.0,
        )
        expected_motor_kw = 7.5 / 0.90
        expected_drive_kw = expected_motor_kw / 0.95
        self.assertAlmostEqual(result.drive_input_power_kw, expected_drive_kw)
        self.assertAlmostEqual(result.drive_loss_power_kw, expected_drive_kw - expected_motor_kw)
        self.assertAlmostEqual(result.drive_input_line_current_a,
                               expected_drive_kw * 1000 / (math.sqrt(3) * 400 * 0.96))

    def test_motor_rejects_duplicate_schedule_and_bad_fraction(self):
        kwargs = dict(motor_id="M", rated_shaft_output_power_kw=1, efficiency_fraction=.9,
                      power_factor=.8, line_voltage_v=400)
        with self.assertRaises(ValueError):
            motor_energy_by_schedule(**kwargs, periods=(MotorOperatingPeriod("P", 1, 1), MotorOperatingPeriod("P", 1, .5)))
        with self.assertRaises(ValueError):
            motor_energy_by_schedule(**kwargs, periods=(MotorOperatingPeriod("P", 1, 1.1),))
        with self.assertRaises(ValueError):
            motor_energy_by_schedule(**kwargs, periods=(MotorOperatingPeriod("P", 1, 0),))
        with self.assertRaises(ValueError):
            motor_energy_by_schedule(
                **kwargs,
                periods=(MotorOperatingPeriod("P1", 1e308, .1), MotorOperatingPeriod("P2", 1e308, .1)),
            )


class EnergyModuleTests(unittest.TestCase):
    def test_schedule_sums_explicit_periods(self):
        result = energy_from_operating_periods(
            periods=(EnergyPeriod("day", 10, 2), EnergyPeriod("night", 5, 4)),
            source_or_assumption="metered average power schedule",
        )
        self.assertEqual(result.total_energy_kwh, 40)
        self.assertEqual(result.total_duration_h, 6)

    def test_balance_closes_without_hidden_term(self):
        result = energy_balance(
            generated_energy_kwh=50,
            imported_energy_kwh=20,
            exported_energy_kwh=5,
            requested_load_energy_kwh=60,
            served_load_energy_kwh=58,
            technical_losses_energy_kwh=3,
            auxiliary_consumption_energy_kwh=2,
        )
        self.assertEqual(result.unserved_load_energy_kwh, 2)
        self.assertAlmostEqual(result.balance_residual_kwh, 0)
        self.assertAlmostEqual(result.closure_error_percent_of_supply, 0)
        unclosed = energy_balance(
            generated_energy_kwh=50, imported_energy_kwh=20, exported_energy_kwh=5,
            requested_load_energy_kwh=60, served_load_energy_kwh=58,
            technical_losses_energy_kwh=4, auxiliary_consumption_energy_kwh=2,
        )
        self.assertAlmostEqual(unclosed.balance_residual_kwh, -1)

    def test_energy_efficiency_flags_boundary_review_without_mutating_result(self):
        result = energy_efficiency(input_energy_kwh=100, useful_output_energy_kwh=105)
        self.assertEqual(result.efficiency_fraction, 1.05)
        self.assertTrue(result.output_exceeds_input)

    def test_zero_supply_has_undefined_closure_percentage(self):
        result = energy_balance(
            generated_energy_kwh=0, imported_energy_kwh=0, exported_energy_kwh=0,
            requested_load_energy_kwh=0, served_load_energy_kwh=0,
            technical_losses_energy_kwh=0, auxiliary_consumption_energy_kwh=0,
        )
        self.assertIsNone(result.closure_error_percent_of_supply)

    def test_rejects_impossible_or_duplicate_period_input(self):
        with self.assertRaises(ValueError):
            energy_balance(
                generated_energy_kwh=1, imported_energy_kwh=0, exported_energy_kwh=0,
                requested_load_energy_kwh=1, served_load_energy_kwh=2,
                technical_losses_energy_kwh=0, auxiliary_consumption_energy_kwh=0,
            )
        with self.assertRaises(ValueError):
            energy_from_operating_periods(
                periods=(EnergyPeriod("P", 1, 1), EnergyPeriod("P", 1, 1)),
                source_or_assumption="test",
            )


class EssentialServicesTests(unittest.TestCase):
    def test_generator_and_ups_rating_references(self):
        generator = generator_capacity_estimate(
            required_active_power_kw=40, power_factor=.8,
            reserve_fraction=.25, site_derating_fraction=.8,
        )
        self.assertAlmostEqual(generator.minimum_nameplate_capacity_kva, 78.125)
        ups = ups_capacity_estimate(
            critical_load_power_kw=10, power_factor=.9,
            loading_limit_fraction=.8, reserve_fraction=.2,
        )
        self.assertAlmostEqual(ups.minimum_apparent_rating_kva, 16.6666666667)

    def test_battery_mission_includes_events_and_rounding(self):
        result = battery_mission_capacity(
            nominal_voltage_v=125,
            continuous_load_kw=.5,
            autonomy_h=8,
            events=(BatteryMissionEvent("TRIP", 1.0, 10, 3),),
            design_margin_fraction=.10,
            aging_capacity_retention_fraction=.90,
            temperature_capacity_retention_fraction=.80,
            depth_of_discharge_fraction=.50,
            discharge_efficiency_fraction=.90,
            capacity_step_ah=10,
        )
        expected_continuous_ah = 500 * 8 / 125
        expected_event_ah = 1000 * 10 * 3 / (125 * 3600)
        expected_raw_ah = expected_continuous_ah + expected_event_ah
        expected_required_ah = expected_raw_ah * 1.1 / (.90 * .80 * .50 * .90)
        self.assertAlmostEqual(result.continuous_capacity_ah, expected_continuous_ah)
        self.assertAlmostEqual(result.event_capacity_ah, expected_event_ah)
        self.assertAlmostEqual(result.raw_mission_capacity_ah, expected_raw_ah)
        self.assertAlmostEqual(result.required_capacity_ah, expected_required_ah)
        self.assertEqual(result.rounded_capacity_ah, 110)
        self.assertEqual(result.provenance.status, "PRELIMINARY_BATTERY_MISSION_AH_NO_RATE_CURVE_OR_CELL_SELECTION")

    def test_capacity_functions_reject_missing_physical_basis(self):
        with self.assertRaises(ValueError):
            generator_capacity_estimate(required_active_power_kw=1, power_factor=0)
        with self.assertRaises(ValueError):
            ups_capacity_estimate(critical_load_power_kw=1, power_factor=.9, loading_limit_fraction=0)
        with self.assertRaises(ValueError):
            battery_mission_capacity(nominal_voltage_v=0, continuous_load_kw=1, autonomy_h=1)
        with self.assertRaises(ValueError):
            battery_mission_capacity(nominal_voltage_v=125, continuous_load_kw=0, autonomy_h=1)


class AutomationModuleTests(unittest.TestCase):
    def _architecture(self):
        return build_control_architecture(
            equipment_ids=("PLC-01", "PT-01", "XV-01"),
            controller_ids=("PLC-01",),
            field_device_ids=("PT-01", "XV-01"),
            points=(
                IOPoint("PT-01.PV", "AI", "PT-01", "PLC-01", "Tank pressure", "bar", 0, 16),
                IOPoint("XV-01.CMD", "DO", "PLC-01", "XV-01", "Open valve command"),
            ),
            relations=(EquipmentRelation("CTRL-01", "PLC-01", "XV-01", "CONTROL", "PLC command to actuator"),),
        )

    def test_io_map_validates_and_labels_logical_only(self):
        result = self._architecture()
        self.assertEqual(result["summary"]["analog_input_count"], 1)
        self.assertEqual(result["summary"]["digital_output_count"], 1)
        self.assertFalse(result["summary"]["physical_wiring_confirmed"])
        self.assertEqual(result["points"][0]["protocol_status"], "NOT_SPECIFIED_OR_TESTED")
        self.assertEqual(result["relations"][0]["implementation_status"], "DOCUMENTED_LOGICAL_RELATION_NOT_PHYSICAL_CONNECTION")

    def test_io_map_rejects_bad_direction_duplicate_tag_and_bad_analog_range(self):
        self._architecture()
        with self.assertRaises(ValueError):
            build_control_architecture(
                equipment_ids=("PLC", "PT"), controller_ids=("PLC",), field_device_ids=("PT",),
                points=(IOPoint("BAD", "AI", "PLC", "PT", "wrong direction", "bar", 0, 1),),
            )
        with self.assertRaises(ValueError):
            build_control_architecture(
                equipment_ids=("PLC", "PT"), controller_ids=("PLC",), field_device_ids=("PT",),
                points=(IOPoint("P", "AI", "PT", "PLC", "pressure", "bar", 0, 1),
                        IOPoint("P", "AI", "PT", "PLC", "pressure", "bar", 0, 1)),
            )
        with self.assertRaises(ValueError):
            build_control_architecture(
                equipment_ids=("PLC", "PT"), controller_ids=("PLC",), field_device_ids=("PT",),
                points=(IOPoint("P", "AI", "PT", "PLC", "pressure"),),
            )
        with self.assertRaises(ValueError):
            control_architecture_from_mapping({
                "equipment_ids": "PLC",
                "controller_ids": ["PLC"],
                "field_device_ids": [],
                "points": [],
            })
        with self.assertRaises(ValueError):
            build_control_architecture(
                equipment_ids=("PLC", "PT"),
                controller_ids=(("PLC",),),
                field_device_ids=("PT",),
                points=(),
            )

    def test_results_support_stable_calculation_records(self):
        result = energy_efficiency(
            input_energy_kwh=80, useful_output_energy_kwh=72,
            provenance=Provenance("meter boundary test", "REFERENCE_CASE"),
        )
        record = identified_calculation_record(
            calc_id="EN-REF-01", inputs={"input_kwh": 80, "output_kwh": 72},
            method="eta=Eout/Ein", result=result, unit="fraction",
            limitations=("Boundary values are synthetic test inputs.",),
        )
        self.assertEqual(record["calc_id"], "EN-REF-01")
        self.assertEqual(record["result"]["efficiency_fraction"], .9)
        self.assertEqual(record["status"], "REFERENCE_CASE")

    def test_synthetic_json_example_validates_and_writes_report(self):
        root = Path(__file__).resolve().parents[1]
        input_path = root / "scripts" / "engineering" / "examples" / "control-architecture.example.json"
        source = json.loads(input_path.read_text(encoding="utf-8"))
        in_memory = control_architecture_from_mapping(source)
        self.assertEqual(in_memory["summary"]["point_count"], 2)
        with tempfile.TemporaryDirectory() as temp_dir:
            output_path = Path(temp_dir) / "nested" / "result.json"
            written = write_control_architecture(input_path, output_path)
            self.assertEqual(json.loads(output_path.read_text(encoding="utf-8")), written)
            self.assertFalse(written["summary"]["physical_wiring_confirmed"])


if __name__ == "__main__":
    unittest.main()
