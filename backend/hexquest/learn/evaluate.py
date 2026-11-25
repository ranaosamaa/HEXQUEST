"""Fair evaluation of a weight vector against a baseline on held-out seeds."""
from typing import Callable, Sequence

from ..agents.base import Agent
from ..agents.learner import LearnerAgent
from ..rules import RulesConfig
from ..runner import play_match
from ..stats import mean_ci, wilson

EVAL_SEED_BASE = 1_000_000          # training seeds never reach this range


def head_to_head(weights: Sequence[float], opponent: Callable[[int], Agent], games: int,
                 n_players: int = 2, cfg: RulesConfig = None, seed0: int = EVAL_SEED_BASE) -> dict:
    """Learner (frozen, epsilon=0) vs `opponent(seed)`.

    PAIRED design: every map seed is played once from each seat, so map luck and first-mover
    advantage cancel. (Unpaired 100-game evaluations were off by +-10 points.)"""
    wins, margins = 0.0, []
    for g in range(games):
        seed = seed0 + g // n_players
        seat = g % n_players
        agents = [opponent(seed) for _ in range(n_players)]
        agents[seat] = LearnerAgent(seed=seed, weights=weights, epsilon=0.0)
        rec = play_match(agents, seed, cfg, time_budget=None)
        if seat in rec.winners:
            wins += 1.0 / len(rec.winners)
        margins.append(rec.scores[seat] - max(s for i, s in enumerate(rec.scores) if i != seat))
    lo, hi = wilson(wins, games)
    m, h = mean_ci(margins)
    return {"games": games, "win_rate": wins / games, "ci": (lo, hi), "margin": m, "margin_ci": h}


def main():
    """Unbiased test of a saved model on seeds the trainer never used.
    python -m hexquest.learn.evaluate --model models/learner_best.json --games 400"""
    import argparse
    from ..agents import make_agent
    from ..agents.model_io import load_model

    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="models/learner_best.json")
    ap.add_argument("--games", type=int, default=400, help="per opponent (paired seats)")
    ap.add_argument("--opponents", nargs="+", default=["greedy", "random"])
    ap.add_argument("--players", type=int, default=2)
    ap.add_argument("--seed-base", type=int, default=9_000_000)
    a = ap.parse_args()

    w, meta = load_model(a.model)
    print(f"model {a.model}  (trained {meta.get('games_trained', '?')} games)\n")
    for spec in a.opponents:
        r = head_to_head(w, lambda sd: make_agent(spec, sd), a.games, a.players, seed0=a.seed_base)
        print(f"vs {spec:30} win {r['win_rate']:.1%}  95% CI [{r['ci'][0]:.1%}, {r['ci'][1]:.1%}]  "
              f"margin {r['margin']:+.1f} +-{r['margin_ci']:.1f}  ({r['games']} games)")


if __name__ == "__main__":
    main()
