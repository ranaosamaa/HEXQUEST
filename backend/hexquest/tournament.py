"""
Tournament runner. Examples:
    python -m hexquest.tournament --agents greedy random --games 200
    python -m hexquest.tournament --agents planner greedy random --games 60 --workers 4
    python -m hexquest.tournament --agents "planner:rollouts=24" greedy --games 100 --events

Seats rotate every game so no agent keeps a positional advantage. Repeated names are pooled.
"""
import argparse
import dataclasses
import json
from collections import defaultdict
from concurrent.futures import ProcessPoolExecutor

from .agents import make_agent
from .rules import RulesConfig
from .runner import play_match
from .stats import mean_ci, percentile, wilson


def _play_one(task):
    specs, game, seed0, cfg_dict, time_budget = task
    n = len(specs)
    lineup = [(game + i) % n for i in range(n)]            # rotate seats
    seed = seed0 + game
    agents = [make_agent(specs[k], seed=seed) for k in lineup]
    rec = play_match(agents, seed, RulesConfig(**cfg_dict), time_budget=time_budget)
    return lineup, rec


def run_tournament(specs, games, seed0=0, cfg=None, workers=1, time_budget=5.0):
    cfg = cfg or RulesConfig()
    tasks = [(specs, g, seed0, dataclasses.asdict(cfg), time_budget) for g in range(games)]
    if workers > 1:
        with ProcessPoolExecutor(workers) as ex:
            outcomes = list(ex.map(_play_one, tasks, chunksize=1))
    else:
        outcomes = [_play_one(t) for t in tasks]

    stat = defaultdict(lambda: {"games": 0, "wins": 0.0, "scores": [], "margins": [],
                                "territory": [], "lat": [], "fail": 0})
    for lineup, rec in outcomes:
        for seat, k in enumerate(lineup):
            st = stat[specs[k]]
            st["games"] += 1
            if seat in rec.winners:
                st["wins"] += 1.0 / len(rec.winners)
            others = [rec.scores[q] for q in range(len(lineup)) if q != seat]
            st["scores"].append(rec.scores[seat])
            st["margins"].append(rec.scores[seat] - max(others))
            st["territory"].append(rec.territory[seat])
            st["lat"].extend(rec.latencies[seat])
            st["fail"] += sum(rec.failures[seat].values())
    return stat


def report(stat, specs, games):
    n = len(specs)
    print(f"{games} games, {n} players, seats rotated. Fair share = {1/n:.0%}\n")
    print(f"{'agent':28} {'win rate':>9} {'95% CI':>15} {'score':>12} {'margin':>12} {'cells':>6} "
          f"{'lat p50/p95 ms':>15} {'fail':>5}")
    for spec in dict.fromkeys(specs):
        st = stat[spec]
        lo, hi = wilson(st["wins"], st["games"])
        sm, sh = mean_ci(st["scores"])
        mm, mh = mean_ci(st["margins"])
        tm, _ = mean_ci(st["territory"])
        p50, p95 = percentile(st["lat"], .5) * 1e3, percentile(st["lat"], .95) * 1e3
        print(f"{spec:28} {st['wins']/st['games']:9.1%} [{lo:5.1%},{hi:5.1%}] "
              f"{sm:6.1f}±{sh:4.1f} {mm:+7.1f}±{mh:4.1f} {tm:6.1f} {p50:7.1f}/{p95:<7.1f} {st['fail']:5d}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--agents", nargs="+", required=True, help="one spec per seat, e.g. planner greedy random")
    ap.add_argument("--games", type=int, default=100)
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--workers", type=int, default=1)
    ap.add_argument("--radius", type=int, default=4)
    ap.add_argument("--events", action="store_true", help="enable dropout events too")
    ap.add_argument("--json", help="write raw summary to this file")
    a = ap.parse_args()

    cfg = RulesConfig(board_radius=a.radius, p_dropout=0.02 if a.events else 0.0)
    stat = run_tournament(a.agents, a.games, a.seed, cfg, a.workers)
    report(stat, a.agents, a.games)
    if a.json:
        with open(a.json, "w") as f:
            json.dump({k: {**v, "lat": None} for k, v in stat.items()}, f, indent=1)


if __name__ == "__main__":
    main()
