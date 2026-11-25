"""The one interface every controller implements (proposal Phase 1)."""
import random
from typing import List, Optional

from ..engine import Action, GameState


class Agent:
    name = "agent"
    version = "0.1"

    def __init__(self, seed: int = 0):
        self.seed = seed
        self.rng = random.Random(seed)
        self.last_trace: dict = {}   # filled by choose(); the runner logs it as an agent_decision

    def reset(self, match_info: dict) -> None:
        """Called once per match. match_info: seat, n_players, seed. Reseeds so matches are reproducible."""
        self.rng = random.Random(f"{self.name}:{match_info['seed']}:{match_info['seat']}:{self.seed}")
        self.last_trace = {}

    def choose(self, obs: GameState, seat: int, legal: List[Action],
               deadline: Optional[float]) -> Action:
        """Return ONE action from `legal`. Never mutate obs (clone it to simulate).
        `deadline` is an absolute time.perf_counter() value, or None for no limit."""
        raise NotImplementedError

    @property
    def label(self) -> str:
        return f"{self.name}-{self.version}"
