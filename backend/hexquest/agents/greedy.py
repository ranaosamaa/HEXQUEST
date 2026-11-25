import math
from .base import Agent
from .common import expected_value
from .features import DEFAULT_WEIGHTS


class GreedyAgent(Agent):
    """One-step lookahead: pick the action whose resulting position scores highest."""
    name = "greedy"

    def __init__(self, seed=0, weights=None):
        super().__init__(seed)
        self.weights = tuple(weights) if weights is not None else DEFAULT_WEIGHTS

    def choose(self, obs, seat, legal, deadline):
        best, best_v, values = legal[-1], -math.inf, []
        for a in legal:
            v = expected_value(obs, a, seat, self.weights, self.rng)
            v += self.rng.random() * 1e-9          # random tie-break, still reproducible
            values.append(round(v, 4))
            if v > best_v:
                best, best_v = a, v
        self.last_trace = {"n_legal": len(legal), "best_value": round(best_v, 4), "values": values}
        return best
