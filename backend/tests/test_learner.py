import json

import pytest
from fastapi.testclient import TestClient

from hexquest.agents import make_agent
from hexquest.agents import model_io
from hexquest.agents.features import DEFAULT_WEIGHTS, FEATURE_NAMES
from hexquest.agents.learner import LearnerAgent
from hexquest.engine import legal_actions, new_game, observation
from hexquest.learn.evaluate import head_to_head
from hexquest.learn.trainer import (TrainConfig, dot, lambda_targets, play_training_game, train,
                                    update_weights)
from hexquest.rules import RulesConfig
from hexquest.runner import play_match
from hexquest.server.app import create_app


# ------------------------------------------------------------------ model files

def test_model_roundtrip_and_id(tmp_path):
    w = [i * 0.5 for i in range(len(FEATURE_NAMES))]
    path = tmp_path / "m.json"
    model_io.save_model(path, w, {"games_trained": 7})
    loaded, meta = model_io.load_model(path)
    assert list(loaded) == w and meta["games_trained"] == 7
    assert model_io.model_id(w) == json.load(open(path))["model_id"]


def test_model_with_different_feature_set_is_rejected(tmp_path):
    path = tmp_path / "old.json"
    json.dump({"format": 1, "feature_names": ["a", "b"], "weights": [1, 2]}, open(path, "w"))
    with pytest.raises(ValueError):
        model_io.load_model(path)


def test_missing_model_falls_back_to_hand_tuned_weights(tmp_path, monkeypatch):
    monkeypatch.chdir(tmp_path)
    monkeypatch.setattr(model_io, "REPO_MODEL", tmp_path / "nope.json")
    monkeypatch.delenv("HEXQUEST_MODEL", raising=False)
    a = LearnerAgent()
    assert a.weights == DEFAULT_WEIGHTS and a.version == "untrained"


# ------------------------------------------------------------------ agent behaviour

def test_learner_returns_legal_actions_and_is_reproducible():
    def run():
        return play_match([LearnerAgent(1), make_agent("greedy", 1)], seed=5, cfg=RulesConfig(rounds=15)).final_hash
    assert run() == run()
    s = new_game(2, 3)
    a = LearnerAgent(1)
    a.reset({"seat": 0, "n_players": 2, "seed": 3})
    assert a.choose(observation(s, 0), 0, legal_actions(s, 0), None) in legal_actions(s, 0)


def test_epsilon_one_always_explores():
    s = new_game(2, 3)
    a = LearnerAgent(1, epsilon=1.0)
    a.reset({"seat": 0, "n_players": 2, "seed": 3})
    a.choose(observation(s, 0), 0, legal_actions(s, 0), None)
    assert a.last_trace.get("explore") is True


# ------------------------------------------------------------------ learning maths

def test_lambda_targets_limits():
    vals = [1.0, 2.0, 3.0, 4.0]
    assert lambda_targets(vals, 9.0, lam=1.0) == [9.0] * 4                 # Monte Carlo
    assert lambda_targets(vals, 9.0, lam=0.0) == [2.0, 3.0, 4.0, 9.0]      # TD(0): bootstrap from next
    mid = lambda_targets(vals, 9.0, lam=0.5)
    assert mid[-1] == 9.0 and 2.0 < mid[0] < 9.0


def test_update_reduces_error_and_respects_frozen_weights():
    cfg = TrainConfig(alpha=0.1, l2=0.0)
    w0 = list(DEFAULT_WEIGHTS)
    w = list(w0)
    frozen = [n == "score_margin" for n in FEATURE_NAMES]
    f = [0.5] * len(FEATURE_NAMES)
    target = dot(w, f) + 3.0
    errs = [abs(target - dot(w, f))]
    for _ in range(50):
        update_weights(w, w0, [(f, target)], cfg, frozen)
        errs.append(abs(target - dot(w, f)))
    assert errs[-1] < errs[0] * 0.5
    assert w[FEATURE_NAMES.index("score_margin")] == w0[FEATURE_NAMES.index("score_margin")]


def test_l2_pulls_weights_back_toward_the_starting_point():
    cfg = TrainConfig(alpha=0.1, l2=1.0)
    w0 = [1.0] * len(FEATURE_NAMES)
    w = [3.0] * len(FEATURE_NAMES)
    update_weights(w, w0, [([0.0] * len(FEATURE_NAMES), 0.0)], cfg, [False] * len(FEATURE_NAMES))
    assert all(1.0 < x < 3.0 for x in w)


def test_training_game_records_one_afterstate_per_decision_and_zero_sum_returns():
    rules = RulesConfig(rounds=8)
    agents = [LearnerAgent(1, epsilon=0.1), LearnerAgent(2, epsilon=0.1)]
    traj, returns, scores_ = play_training_game(agents, [0, 1], seed=4, rules=rules)
    assert len(traj[0]) == len(traj[1]) == 8
    assert returns[0] == pytest.approx(-returns[1])


# ------------------------------------------------------------------ training end to end

def test_training_run_writes_models_and_is_reproducible(tmp_path):
    def run(sub):
        cfg = TrainConfig(games=8, eval_every=4, eval_games=4, out_dir=str(tmp_path / sub),
                          rules={"rounds": 6}, seed=3)
        train(cfg, log=lambda *_: None)
        return model_io.load_model(tmp_path / sub / "learner_latest.json")[0]
    a, b = run("a"), run("b")
    assert a == b                                         # same seed -> identical learned weights
    assert a != tuple(DEFAULT_WEIGHTS)                    # and it actually learned something
    for name in ("learner_best.json", "learner_latest.json", "training_log.csv", "training_config.json"):
        assert (tmp_path / "a" / name).exists()


def test_head_to_head_is_paired_and_reports_ci():
    r = head_to_head(DEFAULT_WEIGHTS, lambda sd: make_agent("random", sd), games=6, cfg=RulesConfig(rounds=10))
    eps = 1e-9
    assert r["games"] == 6 and -eps <= r["ci"][0] <= r["win_rate"] + eps and r["win_rate"] <= r["ci"][1] + eps <= 1 + 2 * eps


# ------------------------------------------------------------------ server integration

def test_server_can_seat_the_learner():
    client = TestClient(create_app(agent_delay=0))
    with client.websocket_connect("/ws") as ws:
        ws.send_json({"type": "START", "player_count": 2, "rounds": 3, "map_seed": 1, "agents": ["learner"]})
        while True:
            m = ws.receive_json()
            if m["type"] == "STATE" and m["active_player"] == 0:
                break
        assert m["players"][1]["controller_id"].startswith("learner-")
        assert m["players"][1]["name"] == "Learner 1"
