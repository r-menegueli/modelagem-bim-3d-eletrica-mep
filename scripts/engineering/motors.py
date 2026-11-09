"""Motor operating schedules and drive input estimates with explicit assumptions.

    Partial-load motor efficiency and power factor are not inferred from nameplate
    data. Schedule calculations keep the supplied values constant across each
    loaded operating period. Stopped and no-load periods must be modeled from
    measured input power separately.
"""

from __future__ import annotations

from dataclasses import dataclass
import math
from typing import Any, Iterable

from .electrical_core import MotorInput, Provenance, motor_electrical_input


@dataclass(frozen=True)
class MotorOperatingPeriod:
    period_id: str
    duration_h: float
    shaft_load_fraction: float


@dataclass(frozen=True)
class MotorPeriodResult:
    period_id: str
    duration_h: float
    shaft_load_fraction: float
    shaft_output_power_kw: float
    electrical_input_power_kw: float
    electrical_input_apparent_power_kva: float
    line_current_a: float
    energy_kwh: float


@dataclass(frozen=True)
class MotorScheduleResult:
    rated_shaft_output_power_kw: float
    total_operating_time_h: float
    total_electrical_energy_kwh: float
    periods: tuple[MotorPeriodResult, ...]
    provenance: Provenance


@dataclass(frozen=True)
class DriveInputEstimate:
    motor_electrical_input_power_kw: float
    drive_efficiency_fraction: float
    drive_input_power_kw: float
    drive_loss_power_kw: float
    drive_input_apparent_power_kva: float
    drive_input_power_factor: float
    line_voltage_v: float
    drive_input_line_current_a: float
    provenance: Provenance


def _number(name: str, value: float, *, positive: bool = False) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError(f"{name} must be a finite real number")
    converted = float(value)
    if converted < 0 or (positive and converted <= 0):
        qualifier = "positive" if positive else "nonnegative"
        raise ValueError(f"{name} must be {qualifier}")
    return converted


def _fraction(name: str, value: float, *, allow_zero: bool = False) -> float:
    number = _number(name, value)
    if number > 1 or (number == 0 and not allow_zero):
        raise ValueError(f"{name} must be in {'[0, 1]' if allow_zero else '(0, 1]'}")
    return number


def motor_energy_by_schedule(
    *,
    motor_id: str,
    rated_shaft_output_power_kw: float,
    efficiency_fraction: float,
    power_factor: float,
    line_voltage_v: float,
    periods: Iterable[MotorOperatingPeriod],
) -> MotorScheduleResult:
    """Estimate motor kWh for periods using constant efficiency and PF.

    The caller supplies shaft load fractions and durations. It does not model
    starts, acceleration, harmonics, drive standby, or part-load changes in
    efficiency/power factor.
    """
    if not isinstance(motor_id, str) or not motor_id.strip():
        raise ValueError("motor_id cannot be blank")
    rated_kw = _number("rated_shaft_output_power_kw", rated_shaft_output_power_kw, positive=True)
    efficiency = _fraction("efficiency_fraction", efficiency_fraction)
    pf = _fraction("power_factor", power_factor)
    voltage = _number("line_voltage_v", line_voltage_v, positive=True)
    selected = tuple(periods)
    if not selected:
        raise ValueError("periods must contain at least one operating period")
    ids: set[str] = set()
    results: list[MotorPeriodResult] = []
    for period in selected:
        if not isinstance(period, MotorOperatingPeriod):
            raise ValueError("each period must be a MotorOperatingPeriod")
        if not isinstance(period.period_id, str) or not period.period_id.strip() or period.period_id in ids:
            raise ValueError("period_id values must be nonblank and unique")
        ids.add(period.period_id)
        duration = _number(f"{period.period_id}.duration_h", period.duration_h, positive=True)
        fraction = _fraction(f"{period.period_id}.shaft_load_fraction", period.shaft_load_fraction)
        shaft_kw = rated_kw * fraction
        operating = motor_electrical_input(
            shaft_output_power_kw=shaft_kw,
            efficiency_fraction=efficiency,
            power_factor=pf,
            line_voltage_v=voltage,
            provenance=Provenance(
                source_or_assumption=f"Motor {motor_id.strip()}: declared rated output, efficiency, PF, voltage and schedule",
                status="CONSTANT_EFFICIENCY_AND_POWER_FACTOR_ASSUMED_FOR_EACH_LOAD_PERIOD",
            ),
        )
        results.append(MotorPeriodResult(
            period_id=period.period_id,
            duration_h=duration,
            shaft_load_fraction=fraction,
            shaft_output_power_kw=shaft_kw,
            electrical_input_power_kw=operating.electrical_input_power_kw,
            electrical_input_apparent_power_kva=operating.electrical_input_apparent_power_kva,
            line_current_a=operating.line_current_a,
            energy_kwh=operating.electrical_input_power_kw * duration,
        ))
    try:
        total_hours = math.fsum(row.duration_h for row in results)
        total_energy = math.fsum(row.energy_kwh for row in results)
    except OverflowError as error:
        raise ValueError("motor schedule totals are outside the finite numeric range") from error
    if not math.isfinite(total_hours) or not math.isfinite(total_energy):
        raise ValueError("motor schedule totals are outside the finite numeric range")
    return MotorScheduleResult(
        rated_shaft_output_power_kw=rated_kw,
        total_operating_time_h=total_hours,
        total_electrical_energy_kwh=total_energy,
        periods=tuple(results),
        provenance=Provenance(
            source_or_assumption=f"Motor {motor_id.strip()}: explicit operating schedule",
            status="SCHEDULE_ENERGY_ESTIMATE_CONSTANT_EFFICIENCY_AND_POWER_FACTOR",
        ),
    )


def drive_input_estimate(
    *,
    shaft_output_power_kw: float,
    motor_efficiency_fraction: float,
    drive_efficiency_fraction: float,
    drive_input_power_factor: float,
    line_voltage_v: float,
    provenance: Provenance | None = None,
) -> DriveInputEstimate:
    """Estimate steady balanced 3-phase drive input from explicit efficiencies.

    This does not predict harmonics, displacement/true PF differences, thermal
    derating, overload duty, or manufacturer drive selection.
    """
    shaft_kw = _number("shaft_output_power_kw", shaft_output_power_kw)
    motor_efficiency = _fraction("motor_efficiency_fraction", motor_efficiency_fraction)
    drive_efficiency = _fraction("drive_efficiency_fraction", drive_efficiency_fraction)
    pf = _fraction("drive_input_power_factor", drive_input_power_factor)
    voltage = _number("line_voltage_v", line_voltage_v, positive=True)
    motor_input_kw = shaft_kw / motor_efficiency
    drive_input_kw = motor_input_kw / drive_efficiency
    drive_kva = drive_input_kw / pf
    drive_current = drive_kva * 1000.0 / (math.sqrt(3.0) * voltage)
    loss_kw = drive_input_kw - motor_input_kw
    values = (motor_input_kw, drive_input_kw, drive_kva, drive_current, loss_kw)
    if any(not math.isfinite(value) for value in values):
        raise ValueError("drive estimate is outside the finite numeric range")
    return DriveInputEstimate(
        motor_electrical_input_power_kw=motor_input_kw,
        drive_efficiency_fraction=drive_efficiency,
        drive_input_power_kw=drive_input_kw,
        drive_loss_power_kw=loss_kw,
        drive_input_apparent_power_kva=drive_kva,
        drive_input_power_factor=pf,
        line_voltage_v=voltage,
        drive_input_line_current_a=drive_current,
        provenance=provenance or Provenance(
            source_or_assumption="Motor and drive efficiencies and drive input PF explicitly supplied",
            status="STEADY_STATE_DRIVE_INPUT_ESTIMATE_NO_HARMONIC_OR_DUTY_CHECK",
        ),
    )


__all__ = [
    "DriveInputEstimate", "MotorInput", "MotorOperatingPeriod", "MotorPeriodResult",
    "MotorScheduleResult", "drive_input_estimate", "motor_electrical_input",
    "motor_energy_by_schedule",
]
