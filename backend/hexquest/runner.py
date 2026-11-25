"""
Headless match runner (Phase 1): plays agents against each other through the same
observation / legal-action contract the live server will use.

Failure handling follows the proposal: an illegal action, a crash or a timeout becomes Pass,
is counted, and a seat that fails too often is removed like a dropout.
"""
import time
from dataclasses import dataclass, field
from typing import Dict, List, Optional

from .engine import (apply_action, drop_player, legal_actions, new_game, observation, scores,
                     state_hash, territory, winners)
from .rules import DROP, PASS, RulesConfig

PASS_ACTION = (PASS, -1, -1)


@dataclass
class MatchRecord:
    seed: int
    n_players: int
    rules_version: str
    agents: List[str]
    scores: List[int] = field(default_factory=list)
    territory: List[int] = field(default_factory=list)
    winners: List[int] = field(default_factory=list)
    rounds_played: int = 0
    final_hash: str = ""
    actions: List[tuple] = field(default_factory=list)          # (seat, type, src, dst)
    latencies: List[List[float]] = field(default_factory=list)  # seconds, per seat
    failures: List[Dict[str, int]] = field(default_factory=list)
    traces: List[tuple] = field(default_factory=list)           # (step, seat, trace dict)


def request_action(agent, s, p, time_budget):
    """Ask an agent for its move safely. Returns (action, failure_or_None, latency_s, trace).
    Illegal action, crash or timeout all become Pass. Shared by the headless runner and the server."""
    legal = legal_actions(s, p)
    obs = observation(s, p)
    t0 = time.perf_counter()
    deadline = t0 + time_budget if time_budget else None
    failure, action = None, None
    try:
        action = agent.choose(obs, p, legal, deadline)
        action = tuple(action) if action is not None else None
    except Exception:                      # a crashing agent must never crash the match
        failure = "crash"
    latency = time.perf_counter() - t0
    if failure is None and time_budget and latency > time_budget * 1.5:
        failure = "timeout"
    if failure is None and action not in legal:
        failure = "illegal"
    if failure:
        return PASS_ACTION, failure, latency, {}
    return action, None, latency, dict(agent.last_trace)


def play_match(agents, seed: int, cfg: Optional[RulesConfig] = None,
               time_budget: Optional[float] = 5.0, max_failures: int = 5,
               record_traces: bool = False) -> MatchRecord:
    n = len(agents)
    s = new_game(n, seed, cfg)
    rec = MatchRecord(seed=seed, n_players=n, rules_version=s.cfg.version,
                      agents=[a.label for a in agents],
                      latencies=[[] for _ in range(n)],
                      failures=[{"illegal": 0, "crash": 0, "timeout": 0} for _ in range(n)])
    for seat, agent in enumerate(agents):
        agent.reset({"seat": seat, "n_players": n, "seed": seed})

    while not s.done:
        p = s.current
        action, failure, latency, trace = request_action(agents[p], s, p, time_budget)

        rec.latencies[p].append(latency)
        if failure:
            rec.failures[p][failure] += 1
        elif record_traces:
            rec.traces.append((s.steps, p, trace))

        rec.actions.append((p, *action))
        apply_action(s, action)

        if failure and sum(rec.failures[p].values()) >= max_failures and s.active[p]:
            drop_player(s, p)
            rec.actions.append((p, DROP, -1, -1))

    rec.scores, rec.territory, rec.winners = scores(s), territory(s), winners(s)
    rec.rounds_played = min(s.round, s.cfg.rounds)
    rec.final_hash = state_hash(s)
    return rec


def replay(rec: MatchRecord, cfg: Optional[RulesConfig] = None) -> str:
    """Re-run a recorded match from its seed and action log; returns the final state hash."""
    s = new_game(rec.n_players, rec.seed, cfg)
    for seat, atype, src, dst in rec.actions:
        if atype == DROP:
            drop_player(s, seat)
        else:
            assert seat == s.current, "action log out of order"
            apply_action(s, (atype, src, dst))
    return state_hash(s)
