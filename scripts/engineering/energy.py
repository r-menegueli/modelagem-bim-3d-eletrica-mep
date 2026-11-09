"""Explicit energy accounting helpers; no load profile or boundary is inferred."""

from __future__ import annotations

from dataclasses import dataclass
import math
from typing import Iterable

from .electrical_core import Energy, Provenance, energy_from_average_power


@dataclass(frozen=True)
class EnergyPeriod:
    period_id: str
    average_power_kw: float
    duration_h: float


@dataclass(frozen=True)
class EnergyPeriodResult:
    period_id: str
    average_power_kw: float
    duration_h: float
    energy_kwh: float


@dataclass(frozen=True)
class EnergySchedule:
    total_energy_kwh: float
    total_duration_h: float
    periods: tuple[EnergyPeriodResult, ...]
    provenance: Provenance


@dataclass(frozen=True)
class EnergyBalance:
    generated_energy_kwh: float
    imported_energy_kwh: float
    exported_energy_kwh: float
    requested_load_energy_kwh: float
    served_load_energy_kwh: float
    unserved_load_energy_kwh: float
    technical_losses_energy_kwh: float
    auxiliary_consumption_energy_kwh: float
    balance_residual_kwh: float
    closure_error_percent_of_supply: float | None
    provenance: Provenance


@dataclass(frozen=True)
class EnergyEfficiency:
    input_energy_kwh: float
    useful_output_energy_kwh: float
    efficiency_fraction: float
    output_exceeds_input: bool
    provenance: Provenance


def _nonnegative(name: str, value: float) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0:
        raise ValueError(f"{name} must be a finite nonnegative real number")
    return float(value)


def energy_from_operating_periods(
    *, periods: Iterable[EnergyPeriod], source_or_assumption: str
) -> EnergySchedule:
    selected = tuple(periods)
    if not selected:
        raise ValueError("periods must contain at least one period")
    if not isinstance(source_or_assumption, str) or not source_or_assumption.strip():
        raise ValueError("source_or_assumption cannot be blank")
    ids: set[str] = set()
    rows: list[EnergyPeriodResult] = []
    for period in selected:
        if not isinstance(period, EnergyPeriod) or not isinstance(period.period_id, str) or not period.period_id.strip():
            raise ValueError("each period must have a nonblank ID and use EnergyPeriod")
        if period.period_id in ids:
            raise ValueError("period_id values must be unique")
        ids.add(period.period_id)
        power = _nonnegative(f"{period.period_id}.average_power_kw", period.average_power_kw)
        duration = _nonnegative(f"{period.period_id}.duration_h", period.duration_h)
        energy = power * duration
        if not math.isfinite(energy):
            raise ValueError("period energy is outside the finite numeric range")
        rows.append(EnergyPeriodResult(period.period_id, power, duration, energy))
    try:
        total_energy = math.fsum(row.energy_kwh for row in rows)
        total_duration = math.fsum(row.duration_h for row in rows)
    except OverflowError as error:
        raise ValueError("energy schedule totals are outside the finite numeric range") from error
    if not math.isfinite(total_energy) or not math.isfinite(total_duration):
        raise ValueError("energy schedule totals are outside the finite numeric range")
    return EnergySchedule(
        total_energy_kwh=total_energy,
        total_duration_h=total_duration,
        periods=tuple(rows),
        provenance=Provenance(source_or_assumption=source_or_assumption.strip(), status="ENERGY_SUMMED_FROM_DECLARED_PERIODS"),
    )


def energy_balance(
    *, generated_energy_kwh: float, imported_energy_kwh: float,
    exported_energy_kwh: float, requested_load_energy_kwh: float,
    served_load_energy_kwh: float, technical_losses_energy_kwh: float,
    auxiliary_consumption_energy_kwh: float,
    provenance: Provenance | None = None,
) -> EnergyBalance:
    """Close a declared energy boundary without assigning missing terms.

    Residual = generation + imports - exports - served load - unserved load
    - technical losses - auxiliary consumption. A nonzero residual flags an
    incomplete or inconsistent accounting boundary; no balancing item is added.
    """
    names = (
        "generated_energy_kwh", "imported_energy_kwh", "exported_energy_kwh",
        "requested_load_energy_kwh", "served_load_energy_kwh",
        "technical_losses_energy_kwh", "auxiliary_consumption_energy_kwh",
    )
    values = locals()
    v = {name: _nonnegative(name, values[name]) for name in names}
    if v["served_load_energy_kwh"] > v["requested_load_energy_kwh"]:
        raise ValueError("served_load_energy_kwh cannot exceed requested_load_energy_kwh")
    unserved = v["requested_load_energy_kwh"] - v["served_load_energy_kwh"]
    supply = v["generated_energy_kwh"] + v["imported_energy_kwh"]
    residual = math.fsum((supply, -v["exported_energy_kwh"], -v["served_load_energy_kwh"],
                          -unserved, -v["technical_losses_energy_kwh"], -v["auxiliary_consumption_energy_kwh"]))
    if not math.isfinite(residual) or not math.isfinite(supply):
        raise ValueError("energy balance is outside the finite numeric range")
    closure = abs(residual) / supply * 100.0 if supply > 0 else None
    return EnergyBalance(
        **v,
        unserved_load_energy_kwh=unserved,
        balance_residual_kwh=residual,
        closure_error_percent_of_supply=closure,
        provenance=provenance or Provenance(
            source_or_assumption="All energy terms and boundary explicitly supplied",
            status="DECLARED_BOUNDARY_ENERGY_BALANCE_NO_MISSING_TERM_IMPUTED",
        ),
    )


def energy_efficiency(
    *, input_energy_kwh: float, useful_output_energy_kwh: float,
    provenance: Provenance | None = None,
) -> EnergyEfficiency:
    """Compute output/input; values over 100% are retained and flagged for review."""
    incoming = _nonnegative("input_energy_kwh", input_energy_kwh)
    useful = _nonnegative("useful_output_energy_kwh", useful_output_energy_kwh)
    if incoming <= 0:
        raise ValueError("input_energy_kwh must be positive")
    fraction = useful / incoming
    if not math.isfinite(fraction):
        raise ValueError("efficiency_fraction is outside the finite numeric range")
    return EnergyEfficiency(
        input_energy_kwh=incoming,
        useful_output_energy_kwh=useful,
        efficiency_fraction=fraction,
        output_exceeds_input=useful > incoming,
        provenance=provenance or Provenance(
            source_or_assumption="Input and useful output energy supplied at a declared boundary",
            status="ENERGY_EFFICIENCY_BOUNDARY_SENSITIVE",
        ),
    )


__all__ = [
    "Energy", "EnergyBalance", "EnergyEfficiency", "EnergyPeriod", "EnergyPeriodResult",
    "EnergySchedule", "energy_balance", "energy_efficiency", "energy_from_average_power",
    "energy_from_operating_periods",
]
