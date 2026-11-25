"""
Learner training (proposal Phase 5).

What "learns after every game" means here
-----------------------------------------
The Learner's value function is linear:  V(s) = w . features(s).
During a game it picks the action whose resulting position ("afterstate") has the highest V,
exploring with probability epsilon. It records the features of every afterstate it created.
When the game ends it EVALUATES ITSELF against the real outcome (final score margin) and
updates the weights once, before the next game starts:

    target_t = lambda-return of the final outcome (lambda = 1 -> plain final outcome)
    w <- w + alpha * mean_t[(target_t - w . phi_t) * phi_t]  - alpha * l2 * (w - w0)

Weights start from the hand-tuned evaluator, so the Learner begins as Greedy and can only drift
away from it as evidence accumulates. Every `eval_every` games the frozen weights are tested on
held-out seeds; the best checkpoint (not the latest) is what gets deployed.

Run:  python -m hexquest.learn.trainer --games 3000
"""
import argparse
import csv
import json
import random
import time
from dataclasses import dataclass, field, asdict
from pathlib import Path
from typing import List, Sequence

from ..agents import make_agent
from ..agents.features import DEFAULT_WEIGHTS, FEATURE_NAMES, features
from ..agents.learner import LearnerAgent
from ..agents.model_io import load_model, save_model
from ..engine import advance_turn, apply_action, legal_actions, new_game, observation, scores
from ..rules import RulesConfig
from .evaluate import head_to_head

RETURN_SCALE = 10.0     # return = RETURN_SCALE * final_margin / 100, same scale as the margin feature


@dataclass
class TrainConfig:
    games: int = 3000
    players: tuple = (2, 3)
    alpha: float = 0.02
    alpha_end: float = 0.002           # linear decay alpha -> alpha_end (negative = constant)
    lam: float = 0.5
    l2: float = 0.0
    eps_start: float = 0.20
    eps_end: float = 0.03
    self_play_frac: float = 0.5
    freeze: tuple = ("score_margin",)  # weights that stay fixed (the goal itself is not re-learned)
    eval_every: int = 250
    eval_games: int = 100
    seed: int = 0
    out_dir: str = "models"
    init_path: str = ""
    clip: float = 10.0
    rules: dict = field(default_factory=dict)


# ----------------------------------------------------------------------------- learning maths

def dot(w: Sequence[float], f: Sequence[float]) -> float:
    return sum(a * b for a, b in zip(w, f))


def lambda_targets(values: List[float], final_return: float, lam: float) -> List[float]:
    """Per-step regression targets. values[t] = V(afterstate_t). lam=1 -> every target is the
    final return (Monte Carlo); lam=0 -> bootstrap from the next afterstate (TD(0))."""
    n = len(values)
    targets = [0.0] * n
    nxt = final_return
    for t in range(n - 1, -1, -1):
        if t == n - 1:
            targets[t] = final_return
        else:
            targets[t] = (1 - lam) * values[t + 1] + lam * nxt
        nxt = targets[t]
    return targets


def update_weights(w: List[float], w0: Sequence[float], samples, cfg: TrainConfig, frozen: Sequence[bool],
                   alpha: float = None):
    """One learning step from one finished game. samples = [(features, target), ...]."""
    if not samples:
        return 0.0
    alpha = cfg.alpha if alpha is None else alpha
    grad = [0.0] * len(w)
    abs_err = 0.0
    for f, target in samples:
        err = max(-cfg.clip, min(cfg.clip, target - dot(w, f)))
        abs_err += abs(err)
        for i, x in enumerate(f):
            grad[i] += err * x
    n = len(samples)
    for i in range(len(w)):
        if frozen[i]:
            continue
        w[i] += alpha * (grad[i] / n - cfg.l2 * (w[i] - w0[i]))
    return abs_err / n


# ----------------------------------------------------------------------------- one training game

def play_training_game(agents, learner_seats, seed, rules: RulesConfig):
    """Plays a game. Returns {seat: [afterstate features]} for the learner seats and final returns."""
    n = len(agents)
    s = new_game(n, seed, rules)
    for seat, a in enumerate(agents):
        a.reset({"seat": seat, "n_players": n, "seed": seed})
    traj = {seat: [] for seat in learner_seats}

    while not s.done:
        p = s.current
        action = agents[p].choose(observation(s, p), p, legal_actions(s, p), None)
        if p in traj:
            apply_action(s, action, advance=False)
            traj[p].append(features(s, p))              # the position this decision created
            advance_turn(s)
        else:
            apply_action(s, action)

    sc = scores(s)
    returns = {seat: RETURN_SCALE * (sc[seat] - max(x for i, x in enumerate(sc) if i != seat)) / 100.0
               for seat in learner_seats}
    return traj, returns, sc


# ----------------------------------------------------------------------------- main loop

def train(cfg: TrainConfig, log=print):
    rng = random.Random(cfg.seed)
    rules = RulesConfig(**cfg.rules)
    out = Path(cfg.out_dir)
    out.mkdir(parents=True, exist_ok=True)

    w0 = list(load_model(cfg.init_path)[0]) if cfg.init_path else list(DEFAULT_WEIGHTS)
    w = list(w0)
    frozen = [name in cfg.freeze for name in FEATURE_NAMES]
    best_w, best_score = list(w), None
    history, t0 = [], time.time()
    err_window: List[float] = []

    def evaluate_now(games_done):
        nonlocal best_w, best_score
        snap = list(w)
        vs_greedy = head_to_head(snap, lambda sd: make_agent("greedy", sd), cfg.eval_games, 2, rules)
        vs_random = head_to_head(snap, lambda sd: make_agent("random", sd), max(20, cfg.eval_games // 4), 2, rules)
        score = (vs_greedy["win_rate"], vs_greedy["margin"])
        is_best = best_score is None or score > best_score
        if is_best:
            best_w, best_score = list(snap), score
            save_model(out / "learner_best.json", snap, {"games_trained": games_done, "vs_greedy": vs_greedy["win_rate"]})
        row = {"games": games_done, "win_vs_greedy": round(vs_greedy["win_rate"], 3),
               "ci_lo": round(vs_greedy["ci"][0], 3), "ci_hi": round(vs_greedy["ci"][1], 3),
               "margin_vs_greedy": round(vs_greedy["margin"], 1),
               "win_vs_random": round(vs_random["win_rate"], 3),
               "mean_abs_error": round(sum(err_window[-200:]) / max(1, len(err_window[-200:])), 3),
               "best": is_best, "seconds": round(time.time() - t0)}
        history.append(row)
        log(f"[{games_done:>5}] vs greedy {row['win_vs_greedy']:.1%} [{row['ci_lo']:.0%},{row['ci_hi']:.0%}] "
            f"margin {row['margin_vs_greedy']:+.1f} | vs random {row['win_vs_random']:.0%} | "
            f"|err| {row['mean_abs_error']:.2f} | {'BEST' if is_best else ''} ({row['seconds']}s)")

    log(f"Training {cfg.games} games from {'checkpoint ' + cfg.init_path if cfg.init_path else 'hand-tuned weights'}")
    evaluate_now(0)                                   # baseline: the untrained learner

    for g in range(1, cfg.games + 1):
        frac = (g - 1) / max(1, cfg.games - 1)
        eps = cfg.eps_start + (cfg.eps_end - cfg.eps_start) * frac
        alpha = cfg.alpha if cfg.alpha_end < 0 else cfg.alpha + (cfg.alpha_end - cfg.alpha) * frac
        n = rng.choice(cfg.players)
        seed = cfg.seed * 1_000_000_000 + g

        if rng.random() < cfg.self_play_frac:         # symmetric self-play: every seat learns
            agents = [LearnerAgent(seed + i, weights=w, epsilon=eps) for i in range(n)]
            learner_seats = list(range(n))
        else:                                         # one learner vs fixed opponents
            seat = rng.randrange(n)
            opp = rng.choices(["greedy", "random", "best"], weights=[0.6, 0.15, 0.25])[0]
            def make_opp(i):
                if opp == "best":
                    return LearnerAgent(seed + i, weights=best_w, epsilon=0.05)
                return make_agent(opp, seed + i)
            agents = [make_opp(i) for i in range(n)]
            agents[seat] = LearnerAgent(seed + seat, weights=w, epsilon=eps)
            learner_seats = [seat]

        traj, returns, _ = play_training_game(agents, learner_seats, seed, rules)

        samples = []
        for seat, feats in traj.items():
            values = [dot(w, f) for f in feats]
            for f, tgt in zip(feats, lambda_targets(values, returns[seat], cfg.lam)):
                samples.append((f, tgt))
        err_window.append(update_weights(w, w0, samples, cfg, frozen, alpha))   # <- the per-game update

        if g % cfg.eval_every == 0 or g == cfg.games:
            evaluate_now(g)

    save_model(out / "learner_latest.json", w, {"games_trained": cfg.games})
    with open(out / "training_log.csv", "w", newline="") as f:
        wr = csv.DictWriter(f, fieldnames=list(history[0].keys()))
        wr.writeheader()
        wr.writerows(history)
    with open(out / "training_config.json", "w") as f:
        json.dump(asdict(cfg), f, indent=1)

    log("\nweights  (initial -> best)")
    for name, a, b in zip(FEATURE_NAMES, w0, best_w):
        log(f"  {name:16} {a:7.2f} -> {b:7.2f}")
    log(f"\nSaved {out/'learner_best.json'} (deploy this one), learner_latest.json, training_log.csv")
    return best_w, history


def main():
    ap = argparse.ArgumentParser(description="Train the HexQuest Learner by self-play.")
    ap.add_argument("--games", type=int, default=3000)
    ap.add_argument("--players", type=int, nargs="+", default=[2, 3])
    ap.add_argument("--alpha", type=float, default=0.02)
    ap.add_argument("--alpha-end", type=float, default=0.002, help="decay alpha linearly to this value (negative = constant)")
    ap.add_argument("--lam", type=float, default=0.5)
    ap.add_argument("--l2", type=float, default=0.0)
    ap.add_argument("--eps-start", type=float, default=0.20)
    ap.add_argument("--eps-end", type=float, default=0.03)
    ap.add_argument("--self-play-frac", type=float, default=0.5)
    ap.add_argument("--freeze", nargs="*", default=["score_margin"], help="feature names to keep fixed")
    ap.add_argument("--eval-every", type=int, default=250)
    ap.add_argument("--eval-games", type=int, default=100)
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--out", default="models")
    ap.add_argument("--init", default="", help="continue from a saved model file")
    a = ap.parse_args()
    train(TrainConfig(games=a.games, players=tuple(a.players), alpha=a.alpha, alpha_end=a.alpha_end, lam=a.lam, l2=a.l2,
                      eps_start=a.eps_start, eps_end=a.eps_end, self_play_frac=a.self_play_frac,
                      freeze=tuple(a.freeze), eval_every=a.eval_every, eval_games=a.eval_games,
                      seed=a.seed, out_dir=a.out, init_path=a.init))


if __name__ == "__main__":
    main()
