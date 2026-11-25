"""Versioned rules configuration. Every tunable number lives here, never scattered in code.

Action types use the SAME integer values as the Unity client's ActionType enum
(Move=0 ... Pass=6), so messages can be mapped one-to-one later.
"""
from dataclasses import dataclass, asdict

RULES_VERSION = "0.1.0"

MOVE, CLAIM, REINFORCE, ATTACK, GATHER, UPGRADE, PASS = range(7)
DROP = 7  # internal marker used in match logs when a seat is removed; never a legal agent action
ACTION_NAMES = ("Move", "Claim", "Reinforce", "Attack", "Gather", "Upgrade", "Pass", "Drop")


@dataclass(frozen=True)
class RulesConfig:
    version: str = RULES_VERSION
    board_radius: int = 4            # 4 -> 61 cells, 5 -> 91 cells
    rounds: int = 40

    start_resources: int = 10
    start_garrison: int = 2
    start_deposit: int = 2

    claim_cost: int = 5
    reinforce_cost: int = 3
    max_garrison: int = 9
    max_gather_level: int = 3
    upgrade_base_cost: int = 8
    upgrade_step_cost: int = 6
    gather_base: int = 1

    deposit_density: float = 0.30
    deposit_max: int = 3

    points_per_cell: int = 10
    points_per_resource: int = 1

    # Event probabilities, per round boundary (seeded and hidden from agents).
    p_resource_shift: float = 0.15
    p_blocked_cells: float = 0.10
    p_dropout: float = 0.0           # off by default; enable for dropout experiments
    shift_radius: int = 2
    shift_delta: int = 2
    block_count: int = 3
    block_duration: int = 3

    def upgrade_cost(self, level: int) -> int:
        return self.upgrade_base_cost + self.upgrade_step_cost * level

    def to_dict(self) -> dict:
        return asdict(self)
