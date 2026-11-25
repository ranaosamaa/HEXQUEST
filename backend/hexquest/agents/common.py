"""Helpers shared by the search-based agents."""
import random
from typing import Sequence

from ..engine import Action, GameState, apply_action
from ..rules import ATTACK, CLAIM, GATHER, MOVE, PASS, REINFORCE, UPGRADE
from .features import evaluate

ATTACK_SAMPLES = 6


def expected_value(obs: GameState, action: Action, seat: int, weights: Sequence[float],
                   rng: random.Random, attack_samples: int = ATTACK_SAMPLES) -> float:
    """Value of the position right after `action`. Dice are unknown to agents, so an attack is
    averaged over several sampled rolls; every other action is deterministic."""
    samples = attack_samples if action[0] == ATTACK else 1
    total = 0.0
    for _ in range(samples):
        s = obs.clone()
        s.rng = rng.getrandbits(64)
        apply_action(s, action, advance=False)   # evaluate within the same round
        total += evaluate(s, seat, weights)
    return total / samples


def fast_policy(s: GameState, legal, rng: random.Random) -> Action:
    """Cheap, biased-random playout policy: plausible play without evaluating every action."""
    cfg, gar, dep = s.cfg, s.garrison, s.deposit
    early = (s.round - 1) / cfg.rounds < 0.5
    leader_dep = dep[s.leader[s.current]] if s.leader[s.current] >= 0 else 0
    weights = []
    for a in legal:
        t = a[0]
        if t == CLAIM:       w = 4.0
        elif t == ATTACK:    w = 3.5 if gar[a[1]] > gar[a[2]] else 0.4
        elif t == GATHER:    w = 3.0
        elif t == UPGRADE:   w = 1.5 if early else 0.2
        elif t == REINFORCE: w = 1.0
        elif t == MOVE:      w = 1.5 if dep[a[2]] > leader_dep else 0.15
        else:                w = 0.05   # PASS
        weights.append(w)
    return rng.choices(legal, weights=weights, k=1)[0]
