import random
import time
from collections import Counter

import pytest

from hexquest.agents import REGISTRY, make_agent
from hexquest.agents.features import DEFAULT_WEIGHTS, FEATURE_NAMES, features
from hexquest.engine import legal_actions, new_game, observation, state_hash
from hexquest.runner import PASS_ACTION, play_match, replay
from hexquest.tournament import run_tournament
from hexquest.agents.base import Agent
from hexquest.rules import RulesConfig


# ---------------------------------------------------------------- contract tests for every agent

@pytest.mark.parametrize("spec", ["random", "greedy", "planner:rollouts=3,horizon=2,top_k=3"])
def test_agent_always_returns_legal_action_and_never_mutates_obs(spec):
    for seed in range(3):
        agents = [make_agent(spec, seed), make_agent("random", seed), make_agent("greedy", seed)]
        s = new_game(3, seed)
        for seat, a in enumerate(agents):
            a.reset({"seat": seat, "n_players": 3, "seed": seed})
        rng = random.Random(seed)
        while not s.done:
            p = s.current
            legal = legal_actions(s, p)
            obs = observation(s, p)
            before = state_hash(obs)
            action = agents[p].choose(obs, p, legal, None)
            assert tuple(action) in legal
            assert state_hash(obs) == before, "agent mutated its observation"
            from hexquest.engine import apply_action
            apply_action(s, action)


def test_all_registered_agents_play_a_full_headless_match_together():
    agents = [make_agent(k, 0) for k in REGISTRY]
    rec = play_match(agents, seed=3, cfg=RulesConfig(rounds=10))
    assert rec.rounds_played == 10 and sum(sum(f.values()) for f in rec.failures) == 0


def test_features_match_declared_names_and_are_finite():
    s = new_game(3, 0)
    f = features(s, 0)
    assert len(f) == len(FEATURE_NAMES) == len(DEFAULT_WEIGHTS)
    assert all(abs(x) < 50 for x in f)


def test_features_are_seat_relative():
    s = new_game(2, 0)
    f0, f1 = features(s, 0), features(s, 1)
    margin = FEATURE_NAMES.index("score_margin")
    assert f0[margin] == pytest.approx(-f1[margin])


def test_agents_are_reproducible():
    def run():
        return play_match([make_agent("planner:rollouts=4,horizon=2", 0), make_agent("greedy", 0)], seed=11,
                          cfg=RulesConfig(rounds=12)).final_hash
    assert run() == run()


# ---------------------------------------------------------------- runner failure handling

class IllegalAgent(Agent):
    name = "illegal"
    def choose(self, obs, seat, legal, deadline):
        return (3, 0, 0)


class CrashAgent(Agent):
    name = "crash"
    def choose(self, obs, seat, legal, deadline):
        raise RuntimeError("boom")


class SlowAgent(Agent):
    name = "slow"
    def choose(self, obs, seat, legal, deadline):
        time.sleep(0.05)
        return legal[-1]


@pytest.mark.parametrize("bad,kind", [(IllegalAgent, "illegal"), (CrashAgent, "crash")])
def test_bad_agent_is_replaced_by_pass_and_eventually_dropped(bad, kind):
    rec = play_match([bad(), make_agent("greedy", 0), make_agent("random", 0)], seed=2,
                     cfg=RulesConfig(rounds=15), max_failures=4)
    assert rec.failures[0][kind] == 4              # dropped after 4 failures, not 15
    assert rec.rounds_played == 15                 # the match still finishes
    assert replay(rec, RulesConfig(rounds=15)) == rec.final_hash   # failures replay exactly


def test_timeout_becomes_pass():
    rec = play_match([SlowAgent(), make_agent("random", 0)], seed=2, cfg=RulesConfig(rounds=3),
                     time_budget=0.01, max_failures=99)
    assert rec.failures[0]["timeout"] == 3
    assert all(a[1:] == PASS_ACTION for a in rec.actions if a[0] == 0)


def test_replay_reproduces_a_full_agent_match():
    rec = play_match([make_agent("greedy", 1), make_agent("random", 1), make_agent("greedy", 1)], seed=21)
    assert replay(rec) == rec.final_hash


def test_decision_traces_are_recorded_for_audit():
    rec = play_match([make_agent("planner:rollouts=3", 0), make_agent("greedy", 0)], seed=1,
                     cfg=RulesConfig(rounds=5), record_traces=True)
    assert rec.traces and all("n_legal" in t for _, _, t in rec.traces)


# ---------------------------------------------------------------- strength / acceptance checks

def test_random_has_no_seat_advantage():
    wins = Counter()
    for seed in range(240):
        rec = play_match([make_agent("random", seed) for _ in range(3)], seed)
        for w in rec.winners:
            wins[w] += 1 / len(rec.winners)
    for seat in range(3):
        assert 0.22 < wins[seat] / 240 < 0.45, dict(wins)


def test_greedy_crushes_random():
    stat = run_tournament(["greedy", "random"], games=30)
    assert stat["greedy"]["wins"] / stat["greedy"]["games"] >= 0.9


def test_planner_beats_random():
    stat = run_tournament(["planner:rollouts=8,horizon=3", "random"], games=16)
    assert stat["planner:rollouts=8,horizon=3"]["wins"] / 16 >= 0.9
