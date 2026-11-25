import random
import time
from collections import defaultdict

from ..engine import apply_action, legal_actions
from .base import Agent
from .common import expected_value, fast_policy
from .model_io import resolve_model
from .features import DEFAULT_WEIGHTS, FEATURE_NAMES, evaluate

MARGIN_ONLY = tuple(10.0 if n == "score_margin" else 0.0 for n in FEATURE_NAMES)


class PlannerAgent(Agent):
    """
    Goal-directed lookahead (flat Monte Carlo with a shortlist).

    1. Shortlist the `top_k` actions by one-step value (cheap pruning).
    2. For each candidate run `rollouts` simulations: apply it, then let every seat play the
       biased-random playout policy for `horizon` rounds (this is the simplified opponent model).
    3. Score the final position with the goal-aligned evaluator (projected final score margin).
    4. Play the candidate with the best average.

    Rollouts use common random numbers (same dice for every candidate) to cut variance.
    Work is bounded by a rollout count, so results are reproducible; `deadline` only cuts it short.
    """
    name = "planner"

    def __init__(self, seed=0, top_k=8, rollouts=100, horizon=5, weights=None, leaf="shaped", path=None):
        super().__init__(seed)
        if weights is None and path:
            weights = resolve_model(path)[0]       # planner using LEARNED evaluator weights
        self.top_k, self.rollouts, self.horizon = int(top_k), int(rollouts), int(horizon)
        self.weights = tuple(weights) if weights is not None else DEFAULT_WEIGHTS
        # leaf="margin" scores playout ends purely by projected score margin (the true goal);
        # leaf="shaped" also rewards potential (income, frontier...) via the full evaluator.
        self.leaf_weights = MARGIN_ONLY if leaf == "margin" else self.weights

    def choose(self, obs, seat, legal, deadline):
        if len(legal) == 1:
            self.last_trace = {"n_legal": 1, "rollouts": 0}
            return legal[0]

        scored = sorted(((expected_value(obs, a, seat, self.weights, self.rng), a) for a in legal),
                        key=lambda t: t[0], reverse=True)
        cands = [a for _, a in scored[: self.top_k]]

        remaining_rounds = obs.cfg.rounds - obs.round + 1
        plies = min(self.horizon, remaining_rounds) * obs.n
        totals, counts, done_rollouts = defaultdict(float), defaultdict(int), 0

        for _ in range(self.rollouts):
            seed_k = self.rng.getrandbits(64)
            for a in cands:
                s = obs.clone()
                s.rng = seed_k
                prng = random.Random(seed_k)
                apply_action(s, a)
                for _ in range(plies):
                    if s.done:
                        break
                    apply_action(s, fast_policy(s, legal_actions(s, s.current), prng))
                totals[a] += evaluate(s, seat, self.leaf_weights)
                counts[a] += 1
            done_rollouts += 1
            if deadline is not None and time.perf_counter() > deadline:
                break

        best = max(cands, key=lambda a: totals[a] / counts[a])
        self.last_trace = {
            "n_legal": len(legal), "candidates": len(cands), "rollouts": done_rollouts,
            "horizon_plies": plies, "best_value": round(totals[best] / counts[best], 4),
        }
        return best
