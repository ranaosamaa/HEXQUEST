import math
from typing import Sequence, Tuple


def wilson(successes: float, n: int, z: float = 1.96) -> Tuple[float, float]:
    """95% Wilson score interval for a win rate (accepts fractional wins for ties)."""
    if n == 0:
        return (0.0, 1.0)
    p = successes / n
    denom = 1 + z * z / n
    centre = (p + z * z / (2 * n)) / denom
    half = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / denom
    return (max(0.0, centre - half), min(1.0, centre + half))


def mean_ci(xs: Sequence[float]) -> Tuple[float, float]:
    """Mean and 95% half-width (normal approximation)."""
    n = len(xs)
    if n == 0:
        return (0.0, 0.0)
    m = sum(xs) / n
    if n == 1:
        return (m, 0.0)
    var = sum((x - m) ** 2 for x in xs) / (n - 1)
    return (m, 1.96 * math.sqrt(var / n))


def percentile(xs: Sequence[float], q: float) -> float:
    if not xs:
        return 0.0
    ys = sorted(xs)
    return ys[min(len(ys) - 1, int(q * len(ys)))]
