"""Unit-explicit, assumption-traceable electrical calculation primitives.

This module provides reusable steady-state calculations. It does not size
conductors, apply electrical codes, select equipment, or prove protection
coordination. All inputs use the units named in the argument/field names.
"""

from __future__ import annotations

from dataclasses import asdict, dataclass, is_dataclass
import math
from typing import Any, Sequence


_SQRT_3 = math.sqrt(3.0)
_GRAVITY_M_PER_S2 = 9.80665


@dataclass(frozen=True)
class Provenance:
    """Source/assumption and validation status carried with each result."""

    source_or_assumption: str = "Inputs explicitly supplied to calculation"
    status: str = "CALCULATED_FROM_EXPLICIT_INPUTS"


@dataclass(frozen=True)
class PowerTriangle:
    active_power_kw: float
    reactive_power_kvar: float
    apparent_power_kva: float
    power_factor: float | None
    provenance: Provenance


@dataclass(frozen=True)
class ApparentPower:
    active_power_kw: float
    power_factor: float
    apparent_power_kva: float
    provenance: Provenance


@dataclass(frozen=True)
class ThreePhaseCurrent:
    apparent_power_kva: float
    line_voltage_v: float
    line_current_a: float
    provenance: Provenance


@dataclass(frozen=True)
class SinglePhaseCurrent:
    apparent_power_va: float
    voltage_v: float
    current_a: float
    provenance: Provenance


@dataclass(frozen=True)
class CablePerformance:
    active_power_kw: float
    reactive_power_kvar: float
    apparent_power_kva: float
    power_factor: float | None
    line_voltage_v: float
    line_current_a: float
    resistance_ohm_per_km: float
    reactance_ohm_per_km: float
    one_way_length_km: float
    approximate_voltage_drop_v: float
    approximate_voltage_drop_percent: float
    approximate_load_voltage_v: float
    three_phase_resistive_loss_kw: float
    provenance: Provenance


@dataclass(frozen=True)
class TransformerLoading:
    load_apparent_power_kva: float
    unit_rating_kva: float
    parallel_unit_count: int
    installed_capacity_kva: float
    equal_share_load_per_unit_kva: float
    equal_share_loading_percent: float
    one_unit_exceeds_nameplate_under_equal_share: bool
    provenance: Provenance


@dataclass(frozen=True)
class Energy:
    average_power_kw: float
    duration_h: float
    energy_kwh: float
    provenance: Provenance


@dataclass(frozen=True)
class MotorInput:
    shaft_output_power_kw: float
    efficiency_fraction: float
    power_factor: float
    line_voltage_v: float
    electrical_input_power_kw: float
    electrical_input_apparent_power_kva: float
    line_current_a: float
    provenance: Provenance


@dataclass(frozen=True)
class PVGeneration:
    installed_peak_power_kwp: float
    peak_sun_hours_h_per_day: float
    performance_ratio_fraction: float
    duration_days: float
    daily_energy_kwh: float
    total_energy_kwh: float
    provenance: Provenance


@dataclass(frozen=True)
class HydroGeneration:
    flow_m3_per_s: float
    net_head_m: float
    water_density_kg_per_m3: float
    overall_efficiency_fraction: float
    electrical_output_power_kw: float
    duration_h: float
    electrical_energy_kwh: float
    provenance: Provenance


@dataclass(frozen=True)
class BatteryAutonomy:
    rated_capacity_ah: float
    nominal_voltage_v: float
    dc_load_power_kw: float
    depth_of_discharge_fraction: float
    capacity_retention_fraction: float
    discharge_efficiency_fraction: float
    nominal_energy_kwh: float
    usable_energy_kwh: float
    constant_power_load_current_a: float
    autonomy_h: float
    provenance: Provenance


@dataclass(frozen=True)
class ShortCircuitSensitivity:
    line_voltage_v: float
    source_resistance_ohm_per_phase: float
    source_reactance_ohm_per_phase: float
    transformer_resistance_ohm_per_phase: float
    transformer_reactance_ohm_per_phase: float
    feeder_resistance_ohm_per_phase: float
    feeder_reactance_ohm_per_phase: float
    total_resistance_ohm_per_phase: float
    total_reactance_ohm_per_phase: float
    total_impedance_magnitude_ohm_per_phase: float
    symmetrical_rms_short_circuit_current_ka: float
    provenance: Provenance


@dataclass(frozen=True)
class DemandLoad:
    """Declared load; demand factor is explicit and reactive inputs are an optional pair."""

    equipment_id: str
    connected_active_power_kw: float
    power_factor: float | None
    demand_factor: float
    reactive_direction: str | None


@dataclass(frozen=True)
class DemandScheduleLine:
    equipment_id: str
    connected_active_power_kw: float
    demand_factor: float
    demand_active_power_kw: float
    demand_reactive_power_kvar: float | None
    demand_apparent_power_kva: float | None
    power_factor: float | None
    reactive_direction: str | None


@dataclass(frozen=True)
class DemandSchedule:
    connected_active_power_kw: float
    diversified_active_power_kw: float
    diversified_reactive_power_kvar: float | None
    diversified_apparent_power_kva: float | None
    aggregate_power_factor: float | None
    lines: tuple[DemandScheduleLine, ...]
    provenance: Provenance


@dataclass(frozen=True)
class CorrectedAmpacity:
    reference_ampacity_a: float
    applied_correction_factors: tuple[dict[str, Any], ...]
    combined_factor: float
    corrected_ampacity_a: float
    provenance: Provenance


@dataclass(frozen=True)
class AdiabaticConductorWithstand:
    k_a_sqrt_s_per_mm2: float
    conductor_area_mm2: float
    fault_duration_s: float
    withstand_current_a: float
    withstand_i2t_a2s: float
    provenance: Provenance


@dataclass(frozen=True)
class InterruptingRatingComparison:
    calculated_fault_current_ka: float
    device_interrupting_rating_ka: float
    fault_current_basis: str
    device_rating_basis: str
    bus_nominal_voltage_kv: float
    device_rated_voltage_kv: float
    rating_margin_ka: float | None
    current_rating_sufficient: bool | None
    voltage_rating_sufficient: bool
    status: str
    provenance: Provenance


@dataclass(frozen=True)
class PowerFactorCorrection:
    active_power_kw: float
    initial_power_factor: float
    target_power_factor: float
    initial_reactive_power_kvar: float
    target_reactive_power_kvar: float
    required_reactive_compensation_kvar: float
    provenance: Provenance


def _real(name: str, value: float) -> float:
    if isinstance(value, bool) or isinstance(value, (str, bytes)):
        raise ValueError(f"{name} must be a finite real number, not bool or text")
    try:
        parsed = float(value)
    except (TypeError, ValueError, OverflowError) as exc:
        raise ValueError(f"{name} must be a finite real number") from exc
    if not math.isfinite(parsed):
        raise ValueError(f"{name} must be finite")
    return parsed


def _positive(name: str, value: float) -> float:
    parsed = _real(name, value)
    if parsed <= 0:
        raise ValueError(f"{name} must be greater than zero")
    return parsed


def _nonnegative(name: str, value: float) -> float:
    parsed = _real(name, value)
    if parsed < 0:
        raise ValueError(f"{name} must be zero or greater")
    return parsed


def _fraction(name: str, value: float, *, allow_zero: bool = False) -> float:
    parsed = _real(name, value)
    lower_invalid = parsed < 0 if allow_zero else parsed <= 0
    if lower_invalid or parsed > 1:
        lower = "[0, 1]" if allow_zero else "(0, 1]"
        raise ValueError(f"{name} must be in {lower}")
    return parsed


def _positive_int(name: str, value: int) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
        raise ValueError(f"{name} must be a positive integer")
    return value


def _provenance(value: Provenance | None, default_status: str) -> Provenance:
    if value is None:
        return Provenance(status=default_status)
    if not isinstance(value, Provenance):
        raise ValueError("provenance must be a Provenance instance or None")
    if not isinstance(value.source_or_assumption, str) or not isinstance(value.status, str):
        raise ValueError("provenance source_or_assumption and status must be text")
    if not value.source_or_assumption.strip() or not value.status.strip():
        raise ValueError("provenance source_or_assumption and status cannot be blank")
    return value


def _finite_result(name: str, value: float) -> float:
    if not math.isfinite(value):
        raise ValueError(f"{name} is outside the finite numeric range")
    return value


def power_triangle_from_active_power_and_power_factor(
    *,
    active_power_kw: float,
    power_factor: float,
    reactive_direction: str = "lagging",
    provenance: Provenance | None = None,
) -> PowerTriangle:
    """Return S and signed Q from nonnegative active power and displacement PF.

    `reactive_direction` is `lagging` (positive Q) or `leading` (negative Q).
    At zero active power only unity PF is accepted because PF is otherwise
    undefined by this input form.
    """
    p_kw = _nonnegative("active_power_kw", active_power_kw)
    pf = _fraction("power_factor", power_factor)
    direction = reactive_direction.strip().lower() if isinstance(reactive_direction, str) else ""
    if direction not in {"lagging", "leading"}:
        raise ValueError("reactive_direction must be 'lagging' or 'leading'")
    if p_kw == 0 and pf != 1:
        raise ValueError("zero active power requires unity power factor in this input form")
    s_kva = apparent_power_from_active_power_and_power_factor(
        active_power_kw=p_kw,
        power_factor=pf,
        provenance=provenance,
    ).apparent_power_kva
    q_abs_kvar = _finite_result(
        "reactive_power_kvar", p_kw * math.sqrt(max(0.0, 1.0 - pf * pf)) / pf
    )
    q_kvar = q_abs_kvar if direction == "lagging" else -q_abs_kvar
    return PowerTriangle(
        active_power_kw=p_kw,
        reactive_power_kvar=q_kvar,
        apparent_power_kva=s_kva,
        power_factor=pf,
        provenance=_provenance(provenance, "CALCULATED_FROM_ACTIVE_POWER_AND_POWER_FACTOR"),
    )


def apparent_power_from_active_power_and_power_factor(
    *,
    active_power_kw: float,
    power_factor: float,
    provenance: Provenance | None = None,
) -> ApparentPower:
    """Calculate S=P/PF without assuming the leading/lagging reactive sign."""
    p_kw = _nonnegative("active_power_kw", active_power_kw)
    pf = _fraction("power_factor", power_factor)
    apparent_kva = _finite_result("apparent_power_kva", p_kw / pf)
    return ApparentPower(
        active_power_kw=p_kw,
        power_factor=pf,
        apparent_power_kva=apparent_kva,
        provenance=_provenance(provenance, "APPARENT_POWER_FROM_ACTIVE_POWER_AND_POWER_FACTOR"),
    )


def power_triangle_from_active_and_reactive_power(
    *,
    active_power_kw: float,
    reactive_power_kvar: float,
    provenance: Provenance | None = None,
) -> PowerTriangle:
    """Return apparent power and displacement PF from signed P and Q inputs."""
    p_kw = _nonnegative("active_power_kw", active_power_kw)
    q_kvar = _real("reactive_power_kvar", reactive_power_kvar)
    s_kva = _finite_result("apparent_power_kva", math.hypot(p_kw, q_kvar))
    pf = p_kw / s_kva if s_kva > 0 else None
    return PowerTriangle(
        active_power_kw=p_kw,
        reactive_power_kvar=q_kvar,
        apparent_power_kva=s_kva,
        power_factor=pf,
        provenance=_provenance(provenance, "CALCULATED_FROM_ACTIVE_AND_REACTIVE_POWER"),
    )


def three_phase_line_current(
    *,
    apparent_power_kva: float,
    line_voltage_v: float,
    provenance: Provenance | None = None,
) -> ThreePhaseCurrent:
    """Balanced three-phase line current: I = S/(sqrt(3) * V_LL)."""
    s_kva = _nonnegative("apparent_power_kva", apparent_power_kva)
    voltage_v = _positive("line_voltage_v", line_voltage_v)
    current_a = _finite_result("line_current_a", s_kva * 1000.0 / (_SQRT_3 * voltage_v))
    return ThreePhaseCurrent(
        apparent_power_kva=s_kva,
        line_voltage_v=voltage_v,
        line_current_a=current_a,
        provenance=_provenance(provenance, "BALANCED_THREE_PHASE_CURRENT_FROM_APPARENT_POWER"),
    )


def single_phase_current(
    *,
    apparent_power_va: float,
    voltage_v: float,
    provenance: Provenance | None = None,
) -> SinglePhaseCurrent:
    """Single-phase current from apparent power and line-to-neutral voltage."""
    apparent_va = _nonnegative("apparent_power_va", apparent_power_va)
    voltage = _positive("voltage_v", voltage_v)
    current_a = _finite_result("current_a", apparent_va / voltage)
    return SinglePhaseCurrent(
        apparent_power_va=apparent_va,
        voltage_v=voltage,
        current_a=current_a,
        provenance=_provenance(provenance, "SINGLE_PHASE_CURRENT_FROM_APPARENT_POWER"),
    )


def balanced_three_phase_cable_performance(
    *,
    active_power_kw: float,
    reactive_power_kvar: float,
    line_voltage_v: float,
    resistance_ohm_per_km: float,
    reactance_ohm_per_km: float,
    one_way_length_km: float,
    provenance: Provenance | None = None,
) -> CablePerformance:
    """Approximate balanced 3-phase steady-state voltage drop and I²R loss.

    Uses the usual small-drop line-to-line approximation
    `dV ~= sqrt(3) I L (R cos(phi) + X sin(phi))`, with signed Q so a leading
    load can produce a negative approximate drop. Resistive losses use
    `3 I² R L`. It does not calculate ampacity, thermal withstand, charging
    current, harmonics, or installation correction factors.
    """
    p_kw = _nonnegative("active_power_kw", active_power_kw)
    q_kvar = _real("reactive_power_kvar", reactive_power_kvar)
    voltage_v = _positive("line_voltage_v", line_voltage_v)
    r_ohm_km = _nonnegative("resistance_ohm_per_km", resistance_ohm_per_km)
    x_ohm_km = _nonnegative("reactance_ohm_per_km", reactance_ohm_per_km)
    length_km = _nonnegative("one_way_length_km", one_way_length_km)
    s_kva = _finite_result("apparent_power_kva", math.hypot(p_kw, q_kvar))
    current_a = _finite_result("line_current_a", s_kva * 1000.0 / (_SQRT_3 * voltage_v))
    if s_kva > 0:
        cos_phi = p_kw / s_kva
        sin_phi = q_kvar / s_kva
        pf: float | None = cos_phi
    else:
        cos_phi = 0.0
        sin_phi = 0.0
        pf = None
    drop_v = _finite_result(
        "approximate_voltage_drop_v",
        _SQRT_3 * current_a * length_km * (r_ohm_km * cos_phi + x_ohm_km * sin_phi),
    )
    drop_percent = _finite_result("approximate_voltage_drop_percent", 100.0 * drop_v / voltage_v)
    loss_kw = _finite_result("three_phase_resistive_loss_kw", 3.0 * current_a * current_a * r_ohm_km * length_km / 1000.0)
    return CablePerformance(
        active_power_kw=p_kw,
        reactive_power_kvar=q_kvar,
        apparent_power_kva=s_kva,
        power_factor=pf,
        line_voltage_v=voltage_v,
        line_current_a=current_a,
        resistance_ohm_per_km=r_ohm_km,
        reactance_ohm_per_km=x_ohm_km,
        one_way_length_km=length_km,
        approximate_voltage_drop_v=drop_v,
        approximate_voltage_drop_percent=drop_percent,
        approximate_load_voltage_v=_finite_result("approximate_load_voltage_v", voltage_v - drop_v),
        three_phase_resistive_loss_kw=loss_kw,
        provenance=_provenance(provenance, "BALANCED_3PH_APPROXIMATION_NO_AMPACITY_CHECK"),
    )


def transformer_loading(
    *,
    load_apparent_power_kva: float,
    unit_rating_kva: float,
    parallel_unit_count: int = 1,
    provenance: Provenance | None = None,
) -> TransformerLoading:
    """Estimate loading with equal sharing across identical parallel units."""
    load_kva = _nonnegative("load_apparent_power_kva", load_apparent_power_kva)
    rating_kva = _positive("unit_rating_kva", unit_rating_kva)
    count = _positive_int("parallel_unit_count", parallel_unit_count)
    total_kva = _finite_result("installed_capacity_kva", rating_kva * count)
    per_unit_kva = load_kva / count
    loading_pct = _finite_result("equal_share_loading_percent", per_unit_kva / rating_kva * 100.0)
    return TransformerLoading(
        load_apparent_power_kva=load_kva,
        unit_rating_kva=rating_kva,
        parallel_unit_count=count,
        installed_capacity_kva=total_kva,
        equal_share_load_per_unit_kva=per_unit_kva,
        equal_share_loading_percent=loading_pct,
        one_unit_exceeds_nameplate_under_equal_share=per_unit_kva > rating_kva,
        provenance=_provenance(provenance, "EQUAL_LOAD_SHARING_ASSUMED"),
    )


def energy_from_average_power(
    *,
    average_power_kw: float,
    duration_h: float,
    provenance: Provenance | None = None,
) -> Energy:
    """Energy from average power over a declared duration: E = P * t."""
    power_kw = _nonnegative("average_power_kw", average_power_kw)
    hours = _nonnegative("duration_h", duration_h)
    energy_kwh = _finite_result("energy_kwh", power_kw * hours)
    return Energy(
        average_power_kw=power_kw,
        duration_h=hours,
        energy_kwh=energy_kwh,
        provenance=_provenance(provenance, "ENERGY_FROM_DECLARED_AVERAGE_POWER_AND_DURATION"),
    )


def motor_electrical_input(
    *,
    shaft_output_power_kw: float,
    efficiency_fraction: float,
    power_factor: float,
    line_voltage_v: float,
    provenance: Provenance | None = None,
) -> MotorInput:
    """Estimate steady-state input power/current for a balanced 3-phase motor."""
    shaft_kw = _nonnegative("shaft_output_power_kw", shaft_output_power_kw)
    efficiency = _fraction("efficiency_fraction", efficiency_fraction)
    pf = _fraction("power_factor", power_factor)
    voltage_v = _positive("line_voltage_v", line_voltage_v)
    input_kw = _finite_result("electrical_input_power_kw", shaft_kw / efficiency)
    input_kva = _finite_result("electrical_input_apparent_power_kva", input_kw / pf)
    current_a = _finite_result("line_current_a", input_kva * 1000.0 / (_SQRT_3 * voltage_v))
    return MotorInput(
        shaft_output_power_kw=shaft_kw,
        efficiency_fraction=efficiency,
        power_factor=pf,
        line_voltage_v=voltage_v,
        electrical_input_power_kw=input_kw,
        electrical_input_apparent_power_kva=input_kva,
        line_current_a=current_a,
        provenance=_provenance(provenance, "STEADY_STATE_MOTOR_INPUT_FROM_ASSUMED_EFFICIENCY_AND_POWER_FACTOR"),
    )


def pv_energy_yield(
    *,
    installed_peak_power_kwp: float,
    peak_sun_hours_h_per_day: float,
    performance_ratio_fraction: float,
    duration_days: float,
    provenance: Provenance | None = None,
) -> PVGeneration:
    """Scenario energy yield from kWp, peak-sun-hours, PR, and days."""
    peak_kwp = _nonnegative("installed_peak_power_kwp", installed_peak_power_kwp)
    sun_hours = _nonnegative("peak_sun_hours_h_per_day", peak_sun_hours_h_per_day)
    pr = _fraction("performance_ratio_fraction", performance_ratio_fraction, allow_zero=True)
    days = _nonnegative("duration_days", duration_days)
    daily_kwh = _finite_result("daily_energy_kwh", peak_kwp * sun_hours * pr)
    total_kwh = _finite_result("total_energy_kwh", daily_kwh * days)
    return PVGeneration(
        installed_peak_power_kwp=peak_kwp,
        peak_sun_hours_h_per_day=sun_hours,
        performance_ratio_fraction=pr,
        duration_days=days,
        daily_energy_kwh=daily_kwh,
        total_energy_kwh=total_kwh,
        provenance=_provenance(provenance, "PV_ENERGY_SCENARIO_NOT_A_SITE_FORECAST"),
    )


def hydro_generation(
    *,
    flow_m3_per_s: float,
    net_head_m: float,
    overall_efficiency_fraction: float,
    duration_h: float,
    water_density_kg_per_m3: float = 1000.0,
    provenance: Provenance | None = None,
) -> HydroGeneration:
    """Idealized hydro output from P = rho*g*Q*H*eta and E=P*t."""
    flow = _nonnegative("flow_m3_per_s", flow_m3_per_s)
    head = _nonnegative("net_head_m", net_head_m)
    efficiency = _fraction("overall_efficiency_fraction", overall_efficiency_fraction, allow_zero=True)
    duration_h = _nonnegative("duration_h", duration_h)
    density = _positive("water_density_kg_per_m3", water_density_kg_per_m3)
    power_kw = _finite_result("electrical_output_power_kw", density * _GRAVITY_M_PER_S2 * flow * head * efficiency / 1000.0)
    energy_kwh = _finite_result("electrical_energy_kwh", power_kw * duration_h)
    return HydroGeneration(
        flow_m3_per_s=flow,
        net_head_m=head,
        water_density_kg_per_m3=density,
        overall_efficiency_fraction=efficiency,
        electrical_output_power_kw=power_kw,
        duration_h=duration_h,
        electrical_energy_kwh=energy_kwh,
        provenance=_provenance(provenance, "HYDRO_STEADY_FLOW_HEAD_EFFICIENCY_ESTIMATE"),
    )


def battery_autonomy(
    *,
    rated_capacity_ah: float,
    nominal_voltage_v: float,
    dc_load_power_kw: float,
    depth_of_discharge_fraction: float,
    capacity_retention_fraction: float = 1.0,
    discharge_efficiency_fraction: float = 1.0,
    provenance: Provenance | None = None,
) -> BatteryAutonomy:
    """Idealized constant-power battery autonomy from Ah and usable fractions.

    `capacity_retention_fraction` represents a declared retained-capacity
    factor (for example an explicitly assumed aging factor); it is not a
    battery chemistry or manufacturer discharge curve.
    """
    capacity_ah = _positive("rated_capacity_ah", rated_capacity_ah)
    voltage_v = _positive("nominal_voltage_v", nominal_voltage_v)
    load_kw = _positive("dc_load_power_kw", dc_load_power_kw)
    dod = _fraction("depth_of_discharge_fraction", depth_of_discharge_fraction)
    retention = _fraction("capacity_retention_fraction", capacity_retention_fraction)
    efficiency = _fraction("discharge_efficiency_fraction", discharge_efficiency_fraction)
    nominal_kwh = _finite_result("nominal_energy_kwh", capacity_ah * voltage_v / 1000.0)
    usable_kwh = _finite_result("usable_energy_kwh", nominal_kwh * dod * retention * efficiency)
    current_a = _finite_result("constant_power_load_current_a", load_kw * 1000.0 / voltage_v)
    autonomy_h = _finite_result("autonomy_h", usable_kwh / load_kw)
    return BatteryAutonomy(
        rated_capacity_ah=capacity_ah,
        nominal_voltage_v=voltage_v,
        dc_load_power_kw=load_kw,
        depth_of_discharge_fraction=dod,
        capacity_retention_fraction=retention,
        discharge_efficiency_fraction=efficiency,
        nominal_energy_kwh=nominal_kwh,
        usable_energy_kwh=usable_kwh,
        constant_power_load_current_a=current_a,
        autonomy_h=autonomy_h,
        provenance=_provenance(provenance, "IDEALIZED_AUTONOMY_NO_DISCHARGE_CURVE"),
    )


def short_circuit_from_series_impedances(
    *,
    line_voltage_v: float,
    source_resistance_ohm_per_phase: float,
    source_reactance_ohm_per_phase: float,
    transformer_resistance_ohm_per_phase: float,
    transformer_reactance_ohm_per_phase: float,
    feeder_resistance_ohm_per_phase: float,
    feeder_reactance_ohm_per_phase: float,
    provenance: Provenance | None = None,
) -> ShortCircuitSensitivity:
    """Symmetrical RMS 3-phase fault sensitivity using explicit series R/X.

    Each impedance is a per-phase Thevenin equivalent in ohms, already
    referred to the bus whose line-to-line voltage is supplied. The function
    sums the series equivalents and computes `I = V_LL/(sqrt(3)*|Z_1|)`.
    It omits X/R asymmetry, DC offset, decay, motors, converters, grounding,
    protection clearing times, standards multipliers, and device selection.
    """
    voltage_v = _positive("line_voltage_v", line_voltage_v)
    r_source = _nonnegative("source_resistance_ohm_per_phase", source_resistance_ohm_per_phase)
    x_source = _nonnegative("source_reactance_ohm_per_phase", source_reactance_ohm_per_phase)
    r_transformer = _nonnegative("transformer_resistance_ohm_per_phase", transformer_resistance_ohm_per_phase)
    x_transformer = _nonnegative("transformer_reactance_ohm_per_phase", transformer_reactance_ohm_per_phase)
    r_feeder = _nonnegative("feeder_resistance_ohm_per_phase", feeder_resistance_ohm_per_phase)
    x_feeder = _nonnegative("feeder_reactance_ohm_per_phase", feeder_reactance_ohm_per_phase)
    r_total = r_source + r_transformer + r_feeder
    x_total = x_source + x_transformer + x_feeder
    z_magnitude = _finite_result("total_impedance_magnitude_ohm_per_phase", math.hypot(r_total, x_total))
    if z_magnitude == 0:
        raise ValueError("total series impedance must be nonzero")
    current_ka = _finite_result("symmetrical_rms_short_circuit_current_ka", voltage_v / (_SQRT_3 * z_magnitude) / 1000.0)
    return ShortCircuitSensitivity(
        line_voltage_v=voltage_v,
        source_resistance_ohm_per_phase=r_source,
        source_reactance_ohm_per_phase=x_source,
        transformer_resistance_ohm_per_phase=r_transformer,
        transformer_reactance_ohm_per_phase=x_transformer,
        feeder_resistance_ohm_per_phase=r_feeder,
        feeder_reactance_ohm_per_phase=x_feeder,
        total_resistance_ohm_per_phase=r_total,
        total_reactance_ohm_per_phase=x_total,
        total_impedance_magnitude_ohm_per_phase=z_magnitude,
        symmetrical_rms_short_circuit_current_ka=current_ka,
        provenance=_provenance(provenance, "SYMMETRICAL_RMS_SENSITIVITY_NO_ASYMMETRY_OR_PROTECTION"),
    )


def aggregate_demand_schedule(
    *,
    loads: Sequence[DemandLoad],
    provenance: Provenance | None = None,
) -> DemandSchedule:
    """Aggregate an explicitly diversified load schedule.

    Each load must provide connected active power and a demand factor in
    [0, 1]. Power factor and leading/lagging direction are an optional pair.
    If any load lacks either value, reactive/apparent aggregates remain unknown
    instead of being inferred. The result assumes stated demand factors are
    mutually applicable and does not infer coincidence, phases, voltage, or
    feeder topology.
    """
    if isinstance(loads, (str, bytes)) or not isinstance(loads, Sequence) or not loads:
        raise ValueError("loads must be a non-empty sequence of DemandLoad values")
    lines: list[DemandScheduleLine] = []
    seen_ids: set[str] = set()
    connected_kw_total = 0.0
    demand_kw_total = 0.0
    demand_kvar_total = 0.0
    all_reactive_data_known = True

    for index, load in enumerate(loads):
        if not isinstance(load, DemandLoad):
            raise ValueError(f"loads[{index}] must be a DemandLoad value")
        equipment_id = load.equipment_id
        if not isinstance(equipment_id, str) or not equipment_id.strip():
            raise ValueError(f"loads[{index}].equipment_id cannot be blank")
        if equipment_id in seen_ids:
            raise ValueError(f"duplicate equipment_id: {equipment_id}")
        seen_ids.add(equipment_id)
        connected_kw = _nonnegative(
            f"loads[{index}].connected_active_power_kw", load.connected_active_power_kw
        )
        pf = (
            None
            if load.power_factor is None
            else _fraction(f"loads[{index}].power_factor", load.power_factor)
        )
        demand_factor = _fraction(
            f"loads[{index}].demand_factor", load.demand_factor, allow_zero=True
        )
        direction = (
            load.reactive_direction.strip().lower()
            if isinstance(load.reactive_direction, str)
            else None
        )
        if (pf is None) != (direction is None):
            raise ValueError(f"loads[{index}] must supply both power_factor and reactive_direction, or omit both")
        if direction is not None and direction not in {"leading", "lagging"}:
            raise ValueError(f"loads[{index}].reactive_direction must be 'leading' or 'lagging'")

        demand_kw = _finite_result("demand_active_power_kw", connected_kw * demand_factor)
        if pf is None:
            demand_kvar = None
            demand_kva = None
            all_reactive_data_known = False
        else:
            kvar_magnitude = demand_kw * math.tan(math.acos(pf)) if demand_kw > 0.0 else 0.0
            demand_kvar = kvar_magnitude if direction == "lagging" else -kvar_magnitude
            demand_kva = _finite_result(
                "demand_apparent_power_kva", math.hypot(demand_kw, demand_kvar)
            )
            demand_kvar_total = _finite_result(
                "diversified_reactive_power_kvar", demand_kvar_total + demand_kvar
            )
        connected_kw_total = _finite_result(
            "connected_active_power_kw", connected_kw_total + connected_kw
        )
        demand_kw_total = _finite_result(
            "diversified_active_power_kw", demand_kw_total + demand_kw
        )
        lines.append(
            DemandScheduleLine(
                equipment_id=equipment_id,
                connected_active_power_kw=connected_kw,
                demand_factor=demand_factor,
                demand_active_power_kw=demand_kw,
                demand_reactive_power_kvar=demand_kvar,
                demand_apparent_power_kva=demand_kva,
                power_factor=pf,
                reactive_direction=direction,
            )
        )

    aggregate_kvar = demand_kvar_total if all_reactive_data_known else None
    aggregate_kva = (
        _finite_result("diversified_apparent_power_kva", math.hypot(demand_kw_total, demand_kvar_total))
        if all_reactive_data_known
        else None
    )
    aggregate_pf = demand_kw_total / aggregate_kva if aggregate_kva is not None and aggregate_kva > 0.0 else None
    return DemandSchedule(
        connected_active_power_kw=connected_kw_total,
        diversified_active_power_kw=demand_kw_total,
        diversified_reactive_power_kvar=aggregate_kvar,
        diversified_apparent_power_kva=aggregate_kva,
        aggregate_power_factor=aggregate_pf,
        lines=tuple(lines),
        provenance=_provenance(provenance, "EXPLICIT_LOADS_AND_DEMAND_FACTORS_NO_INFERRED_SIMULTANEITY"),
    )


def ampacity_after_corrections(
    *,
    reference_ampacity_a: float,
    correction_factors: Sequence[dict[str, Any]],
    provenance: Provenance | None = None,
) -> CorrectedAmpacity:
    """Apply named, supplied multipliers to a supplied reference ampacity.

    This does not determine the base ampacity or verify an installation/code
    method. Each factor must identify its value and source/assumption. Factors
    greater than one are allowed because this is a generic multiplier; the
    caller must justify them from a source applicable to the installation.
    """
    reference_a = _positive("reference_ampacity_a", reference_ampacity_a)
    if isinstance(correction_factors, (str, bytes)) or not isinstance(correction_factors, Sequence) or not correction_factors:
        raise ValueError("correction_factors must be a non-empty sequence of named factor records")
    seen: set[str] = set()
    records: list[dict[str, Any]] = []
    combined = 1.0
    for index, factor in enumerate(correction_factors):
        if not isinstance(factor, dict):
            raise ValueError(f"correction_factors[{index}] must be a dict")
        name = factor.get("name")
        source = factor.get("source_or_assumption")
        if not isinstance(name, str) or not name.strip():
            raise ValueError(f"correction_factors[{index}].name cannot be blank")
        if name in seen:
            raise ValueError(f"duplicate correction factor name: {name}")
        seen.add(name)
        if not isinstance(source, str) or not source.strip():
            raise ValueError(f"correction_factors[{index}].source_or_assumption cannot be blank")
        multiplier = _positive(f"correction_factors[{index}].multiplier", factor.get("multiplier"))
        combined = _finite_result("combined_factor", combined * multiplier)
        records.append({
            "name": name,
            "multiplier": multiplier,
            "source_or_assumption": source,
        })
    corrected_a = _finite_result("corrected_ampacity_a", reference_a * combined)
    return CorrectedAmpacity(
        reference_ampacity_a=reference_a,
        applied_correction_factors=tuple(records),
        combined_factor=combined,
        corrected_ampacity_a=corrected_a,
        provenance=_provenance(provenance, "MULTIPLIERS_APPLIED_TO_USER_SUPPLIED_REFERENCE_AMPACITY"),
    )


def adiabatic_conductor_withstand(
    *,
    k_a_sqrt_s_per_mm2: float,
    conductor_area_mm2: float,
    fault_duration_s: float,
    provenance: Provenance | None = None,
) -> AdiabaticConductorWithstand:
    """Calculate a simplified adiabatic I²t withstand from explicitly given k.

    `k` is supplied by the caller with units A*sqrt(s)/mm² for its declared
    conductor and temperature assumptions. No material, insulation, initial
    temperature, final temperature, or normative k value is inferred.
    """
    k_value = _positive("k_a_sqrt_s_per_mm2", k_a_sqrt_s_per_mm2)
    area = _positive("conductor_area_mm2", conductor_area_mm2)
    duration = _positive("fault_duration_s", fault_duration_s)
    current_a = _finite_result(
        "withstand_current_a", k_value * area / math.sqrt(duration)
    )
    i2t = _finite_result("withstand_i2t_a2s", (k_value * area) ** 2)
    return AdiabaticConductorWithstand(
        k_a_sqrt_s_per_mm2=k_value,
        conductor_area_mm2=area,
        fault_duration_s=duration,
        withstand_current_a=current_a,
        withstand_i2t_a2s=i2t,
        provenance=_provenance(provenance, "ADIABATIC_I2T_FROM_EXPLICIT_K_AREA_AND_DURATION"),
    )


def compare_interrupting_rating(
    *,
    calculated_fault_current_ka: float,
    device_interrupting_rating_ka: float,
    fault_current_basis: str,
    device_rating_basis: str,
    bus_nominal_voltage_kv: float,
    device_rated_voltage_kv: float,
    provenance: Provenance | None = None,
) -> InterruptingRatingComparison:
    """Compare explicitly supplied current/voltage ratings without selecting protection.

    Current is compared only when both basis labels match exactly. This is a
    numeric screen, not a standards-compliance, transient, duty, or selectivity
    study. Device data must be applicable to the bus and fault basis.
    """
    fault_ka = _positive("calculated_fault_current_ka", calculated_fault_current_ka)
    device_ka = _positive("device_interrupting_rating_ka", device_interrupting_rating_ka)
    bus_kv = _positive("bus_nominal_voltage_kv", bus_nominal_voltage_kv)
    device_kv = _positive("device_rated_voltage_kv", device_rated_voltage_kv)
    if not isinstance(fault_current_basis, str) or not fault_current_basis.strip():
        raise ValueError("fault_current_basis cannot be blank")
    if not isinstance(device_rating_basis, str) or not device_rating_basis.strip():
        raise ValueError("device_rating_basis cannot be blank")
    voltage_ok = device_kv >= bus_kv
    comparable = fault_current_basis == device_rating_basis
    margin = device_ka - fault_ka if comparable else None
    current_ok = margin >= 0.0 if margin is not None else None
    if not comparable:
        status = "CURRENT_BASES_NOT_COMPARABLE"
    elif not voltage_ok:
        status = "DEVICE_VOLTAGE_RATING_BELOW_BUS_NOMINAL"
    elif not current_ok:
        status = "DEVICE_INTERRUPT_RATING_BELOW_DECLARED_FAULT_CURRENT"
    else:
        status = "NUMERIC_RATINGS_MEET_DECLARED_SCREEN_ONLY"
    return InterruptingRatingComparison(
        calculated_fault_current_ka=fault_ka,
        device_interrupting_rating_ka=device_ka,
        fault_current_basis=fault_current_basis.strip(),
        device_rating_basis=device_rating_basis.strip(),
        bus_nominal_voltage_kv=bus_kv,
        device_rated_voltage_kv=device_kv,
        rating_margin_ka=margin,
        current_rating_sufficient=current_ok,
        voltage_rating_sufficient=voltage_ok,
        status=status,
        provenance=_provenance(provenance, "NUMERIC_DEVICE_RATING_SCREEN_NO_COORDINATION_OR_STANDARD_CHECK"),
    )


def required_reactive_compensation(
    *,
    active_power_kw: float,
    initial_power_factor: float,
    target_power_factor: float,
    provenance: Provenance | None = None,
) -> PowerFactorCorrection:
    """Calculate signed capacitive kvar to move a constant-P load between PFs.

    Positive compensation is capacitive; negative compensation means the
    target would require more lagging reactive power. Harmonics, voltage
    variation, switching steps, resonance, and equipment sizing are excluded.
    """
    p_kw = _nonnegative("active_power_kw", active_power_kw)
    initial_pf = _fraction("initial_power_factor", initial_power_factor)
    target_pf = _fraction("target_power_factor", target_power_factor)
    q_initial = _finite_result(
        "initial_reactive_power_kvar",
        p_kw * math.tan(math.acos(initial_pf)),
    )
    q_target = _finite_result(
        "target_reactive_power_kvar",
        p_kw * math.tan(math.acos(target_pf)),
    )
    return PowerFactorCorrection(
        active_power_kw=p_kw,
        initial_power_factor=initial_pf,
        target_power_factor=target_pf,
        initial_reactive_power_kvar=q_initial,
        target_reactive_power_kvar=q_target,
        required_reactive_compensation_kvar=_finite_result(
            "required_reactive_compensation_kvar", q_initial - q_target
        ),
        provenance=_provenance(provenance, "CONSTANT_ACTIVE_POWER_POWER_FACTOR_SCENARIO_NO_RESONANCE_STUDY"),
    )


def result_record(result: Any) -> dict[str, Any]:
    """Flatten a core dataclass to a JSON-friendly record with provenance.

    Dataclass fields retain explicit unit suffixes. The nested provenance is
    flattened into the familiar `source_or_assumption` and `status` columns.
    """
    if not is_dataclass(result) or isinstance(result, type) or not isinstance(getattr(result, "provenance", None), Provenance):
        raise ValueError("result must be a core result dataclass with provenance")
    record = asdict(result)
    provenance = record.pop("provenance")
    record["source_or_assumption"] = provenance["source_or_assumption"]
    record["status"] = provenance["status"]
    return record


def identified_calculation_record(
    *,
    calc_id: str,
    inputs: dict[str, Any],
    method: str,
    result: Any,
    unit: str,
    limitations: Sequence[str],
) -> dict[str, Any]:
    """Add stable calculation identity and traceability to a core result."""
    required_text = {
        "calc_id": calc_id,
        "method": method,
        "unit": unit,
    }
    for name, value in required_text.items():
        if not isinstance(value, str) or not value.strip():
            raise ValueError(f"{name} cannot be blank")
    if not isinstance(inputs, dict) or not inputs:
        raise ValueError("inputs must be a non-empty structured dictionary")
    if isinstance(limitations, (str, bytes)) or not isinstance(limitations, Sequence) or not limitations:
        raise ValueError("limitations must be a non-empty sequence of explicit statements")
    if any(not isinstance(item, str) or not item.strip() for item in limitations):
        raise ValueError("each limitation must be non-empty text")
    values = result_record(result)
    source_or_assumption = values.pop("source_or_assumption")
    status = values.pop("status")
    return {
        "calc_id": calc_id.strip(),
        "inputs": inputs,
        "method": method.strip(),
        "result": values,
        "unit": unit.strip(),
        "source_or_assumption": source_or_assumption,
        "status": status,
        "limitations": [item.strip() for item in limitations],
    }
