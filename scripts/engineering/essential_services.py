"""Preliminary generator, UPS and DC battery mission estimates.

These functions calculate requirements from explicit user inputs. They do not
select a commercial unit, verify code requirements, or replace manufacturer
discharge curves and site/load studies.
"""

from __future__ import annotations

from dataclasses import dataclass
import math
from typing import Iterable

from .electrical_core import Provenance


@dataclass(frozen=True)
class GeneratorCapacityEstimate:
    required_active_power_kw: float
    power_factor: float
    reserve_fraction: float
    site_derating_fraction: float
    minimum_nameplate_capacity_kva: float
    provenance: Provenance


@dataclass(frozen=True)
class UPSCapacityEstimate:
    critical_load_power_kw: float
    power_factor: float
    loading_limit_fraction: float
    reserve_fraction: float
    minimum_apparent_rating_kva: float
    provenance: Provenance


@dataclass(frozen=True)
class BatteryMissionEvent:
    event_id: str
    constant_power_kw: float
    duration_s: float
    count: int


@dataclass(frozen=True)
class BatteryMissionEstimate:
    nominal_voltage_v: float
    continuous_load_kw: float
    autonomy_h: float
    continuous_capacity_ah: float
    event_capacity_ah: float
    raw_mission_capacity_ah: float
    design_margin_fraction: float
    aging_capacity_retention_fraction: float
    temperature_capacity_retention_fraction: float
    depth_of_discharge_fraction: float
    discharge_efficiency_fraction: float
    required_capacity_ah: float
    selected_capacity_step_ah: float | None
    rounded_capacity_ah: float | None
    provenance: Provenance


def _number(name: str, value: float, *, positive: bool = False) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError(f"{name} must be a finite real number")
    number = float(value)
    if number < 0 or (positive and number <= 0):
        raise ValueError(f"{name} must be {'positive' if positive else 'nonnegative'}")
    return number


def _fraction(name: str, value: float, *, allow_zero: bool = False) -> float:
    number = _number(name, value)
    if number > 1 or (number == 0 and not allow_zero):
        raise ValueError(f"{name} must be in {'[0, 1]' if allow_zero else '(0, 1]'}")
    return number


def generator_capacity_estimate(
    *, required_active_power_kw: float, power_factor: float,
    reserve_fraction: float = 0.0, site_derating_fraction: float = 1.0,
    provenance: Provenance | None = None,
) -> GeneratorCapacityEstimate:
    """Compute minimum nameplate kVA before manufacturer/transient checks."""
    load_kw = _number("required_active_power_kw", required_active_power_kw, positive=True)
    pf = _fraction("power_factor", power_factor)
    reserve = _number("reserve_fraction", reserve_fraction)
    derating = _fraction("site_derating_fraction", site_derating_fraction)
    capacity = load_kw * (1.0 + reserve) / (pf * derating)
    if not math.isfinite(capacity):
        raise ValueError("minimum_nameplate_capacity_kva is outside the finite numeric range")
    return GeneratorCapacityEstimate(
        load_kw, pf, reserve, derating, capacity,
        provenance or Provenance(
            source_or_assumption="Declared steady active load, power factor, reserve and site derating",
            status="PRELIMINARY_STEADY_STATE_GENERATOR_CAPACITY_NO_TRANSIENT_OR_ALTITUDE_CURVE_CHECK",
        ),
    )


def ups_capacity_estimate(
    *, critical_load_power_kw: float, power_factor: float,
    loading_limit_fraction: float, reserve_fraction: float = 0.0,
    provenance: Provenance | None = None,
) -> UPSCapacityEstimate:
    """Compute apparent rating from load PF, declared loading limit and reserve."""
    load_kw = _number("critical_load_power_kw", critical_load_power_kw, positive=True)
    pf = _fraction("power_factor", power_factor)
    limit = _fraction("loading_limit_fraction", loading_limit_fraction)
    reserve = _number("reserve_fraction", reserve_fraction)
    capacity = load_kw / pf * (1.0 + reserve) / limit
    if not math.isfinite(capacity):
        raise ValueError("minimum_apparent_rating_kva is outside the finite numeric range")
    return UPSCapacityEstimate(
        load_kw, pf, limit, reserve, capacity,
        provenance or Provenance(
            source_or_assumption="Declared critical load, power factor, reserve and loading limit",
            status="PRELIMINARY_UPS_STEADY_STATE_RATING_NO_RUNTIME_OR_STEP_LOAD_CHECK",
        ),
    )


def battery_mission_capacity(
    *, nominal_voltage_v: float, continuous_load_kw: float, autonomy_h: float,
    events: Iterable[BatteryMissionEvent] = (), design_margin_fraction: float = 0.0,
    aging_capacity_retention_fraction: float = 1.0,
    temperature_capacity_retention_fraction: float = 1.0,
    depth_of_discharge_fraction: float = 1.0, discharge_efficiency_fraction: float = 1.0,
    capacity_step_ah: float | None = None, provenance: Provenance | None = None,
) -> BatteryMissionEstimate:
    """Estimate Ah from a constant-power mission plus declared transient events.

    Uses nominal DC voltage for every event and continuous period. It omits
    discharge-rate curves, voltage sag, recovery, cell string design and charger
    recovery, so the result is a study envelope and not a battery selection.
    """
    voltage = _number("nominal_voltage_v", nominal_voltage_v, positive=True)
    load_kw = _number("continuous_load_kw", continuous_load_kw)
    hours = _number("autonomy_h", autonomy_h, positive=True)
    margin = _number("design_margin_fraction", design_margin_fraction)
    aging_retention = _fraction("aging_capacity_retention_fraction", aging_capacity_retention_fraction)
    temperature_retention = _fraction(
        "temperature_capacity_retention_fraction", temperature_capacity_retention_fraction
    )
    dod = _fraction("depth_of_discharge_fraction", depth_of_discharge_fraction)
    efficiency = _fraction("discharge_efficiency_fraction", discharge_efficiency_fraction)
    continuous_ah = load_kw * 1000.0 * hours / voltage
    event_ah = 0.0
    seen: set[str] = set()
    for event in tuple(events):
        if not isinstance(event, BatteryMissionEvent):
            raise ValueError("each event must be a BatteryMissionEvent")
        if not isinstance(event.event_id, str) or not event.event_id.strip() or event.event_id in seen:
            raise ValueError("event_id values must be nonblank and unique")
        seen.add(event.event_id)
        power_kw = _number(f"{event.event_id}.constant_power_kw", event.constant_power_kw)
        duration_s = _number(f"{event.event_id}.duration_s", event.duration_s, positive=True)
        if isinstance(event.count, bool) or not isinstance(event.count, int) or event.count < 1:
            raise ValueError(f"{event.event_id}.count must be a positive integer")
        event_ah += power_kw * 1000.0 * duration_s * event.count / (voltage * 3600.0)
    raw_ah = continuous_ah + event_ah
    if raw_ah <= 0:
        raise ValueError("the battery mission must include positive continuous or event energy")
    required = raw_ah * (1.0 + margin) / (aging_retention * temperature_retention * dod * efficiency)
    if any(not math.isfinite(value) for value in (continuous_ah, event_ah, raw_ah, required)):
        raise ValueError("battery capacity estimate is outside the finite numeric range")
    step = None if capacity_step_ah is None else _number("capacity_step_ah", capacity_step_ah, positive=True)
    try:
        rounded = math.ceil(required / step) * step if step is not None else None
    except OverflowError as error:
        raise ValueError("rounded_capacity_ah is outside the finite numeric range") from error
    if rounded is not None and not math.isfinite(rounded):
        raise ValueError("rounded_capacity_ah is outside the finite numeric range")
    return BatteryMissionEstimate(
        voltage, load_kw, hours, continuous_ah, event_ah, raw_ah, margin,
        aging_retention, temperature_retention, dod, efficiency, required, step, rounded,
        provenance or Provenance(
            source_or_assumption=("Explicit constant-power mission, event schedule, capacity-retention fractions, "
                                  "depth of discharge, efficiency and design margin"),
            status="PRELIMINARY_BATTERY_MISSION_AH_NO_RATE_CURVE_OR_CELL_SELECTION",
        ),
    )


__all__ = [
    "BatteryMissionEstimate", "BatteryMissionEvent", "GeneratorCapacityEstimate",
    "UPSCapacityEstimate", "battery_mission_capacity", "generator_capacity_estimate",
    "ups_capacity_estimate",
]
