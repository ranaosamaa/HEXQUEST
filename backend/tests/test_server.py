"""End-to-end tests of the WebSocket protocol, acting exactly like the Unity client does."""
import random

import pytest
from fastapi.testclient import TestClient

from hexquest.server.app import create_app

STATE_KEYS = {"type", "protocol_version", "match_id", "turn_id", "round", "max_rounds", "active_player",
              "state_revision", "game_over", "cells", "players", "legal_actions"}
CELL_KEYS = {"coord", "owner", "garrison", "deposit", "status"}
PLAYER_KEYS = {"player_id", "name", "seat_type", "controller_id", "leader", "resources",
               "gather_level", "active", "score"}
LEGAL_KEYS = {"action_type", "source", "target", "cost"}
RESULT_KEYS = {"type", "actor", "action_type", "accepted", "error", "summary"}


@pytest.fixture
def client():
    return TestClient(create_app(agent_delay=0))


def start(ws, **kw):
    ws.send_json({"type": "START", "player_count": 3, "board_radius": 4, "rounds": 5, "map_seed": 7,
                  "agents": ["greedy", "random"], **kw})


def read_until_my_turn(ws):
    """Collect messages until it is the human's turn (or the game ends). Returns (results, last_state)."""
    results = []
    while True:
        m = ws.receive_json()
        if m["type"] == "RESULT":
            results.append(m)
        elif m["type"] == "STATE" and (m["game_over"] or m["active_player"] == 0):
            return results, m


def action_from(state, legal, **over):
    msg = {"type": "ACTION", "match_id": state["match_id"], "turn_id": state["turn_id"], "player_id": 0,
           "action_type": legal["action_type"], "source": legal["source"], "target": legal["target"],
           "request_id": "t", "expected_state_revision": state["state_revision"]}
    msg.update(over)
    return msg


def test_health(client):
    assert client.get("/health").json()["ok"] is True


def test_messages_match_the_unity_model_field_names_exactly(client):
    with client.websocket_connect("/ws") as ws:
        start(ws)
        results, st = read_until_my_turn(ws)
        assert set(st) == STATE_KEYS
        assert len(st["cells"]) == 61 and len(st["players"]) == 3
        assert set(st["cells"][0]) == CELL_KEYS and set(st["cells"][0]["coord"]) == {"q", "r"}
        assert set(st["players"][0]) == PLAYER_KEYS
        assert st["legal_actions"] and set(st["legal_actions"][0]) == LEGAL_KEYS
        assert st["players"][0]["name"] == "You" and st["players"][0]["seat_type"] == 1
        assert st["players"][1]["seat_type"] == 2
        if results:
            assert set(results[0]) == RESULT_KEYS


def test_play_a_full_match_like_the_client_does(client):
    rng = random.Random(1)
    with client.websocket_connect("/ws") as ws:
        start(ws)
        _, st = read_until_my_turn(ws)
        my_moves = 0
        while not st["game_over"]:
            ws.send_json(action_from(st, rng.choice(st["legal_actions"])))
            my_moves += 1
            results, st = read_until_my_turn(ws)
            assert results[0]["accepted"] and results[0]["actor"] == 0
            assert {r["actor"] for r in results[1:]} <= {1, 2}      # then only the agents act
        assert my_moves == 5                                         # one action per round
        assert st["active_player"] == -1 and st["legal_actions"] == []
        assert max(p["score"] for p in st["players"]) > 0


def test_agents_act_between_human_turns_and_are_labelled(client):
    with client.websocket_connect("/ws") as ws:
        start(ws, agents=["planner:rollouts=4", "random"])
        _, st = read_until_my_turn(ws)
        assert st["players"][1]["controller_id"].startswith("planner")
        assert st["players"][1]["name"] == "Planner 1"
        pass_ = next(l for l in st["legal_actions"] if l["action_type"] == 6)
        ws.send_json(action_from(st, pass_))
        results, st2 = read_until_my_turn(ws)
        assert [r["actor"] for r in results] == [0, 1, 2]
        assert st2["turn_id"] > st["turn_id"]


def test_stale_illegal_and_out_of_turn_actions_are_rejected_then_resynced(client):
    with client.websocket_connect("/ws") as ws:
        start(ws)
        _, st = read_until_my_turn(ws)
        legal = st["legal_actions"][0]

        bad = {
            "stale_request": action_from(st, legal, expected_state_revision=st["state_revision"] - 1),
            "illegal_action": action_from(st, legal, action_type=3, source={"q": 0, "r": 0}, target={"q": 0, "r": 0}),
            "not_your_turn": action_from(st, legal, player_id=1),
        }
        for code, msg in bad.items():
            ws.send_json(msg)
            res = ws.receive_json()
            assert res["type"] == "RESULT" and res["accepted"] is False and res["error"] == code
            resync = ws.receive_json()                      # server re-sends authoritative state
            assert resync["type"] == "STATE" and resync["turn_id"] == st["turn_id"]

        ws.send_json(action_from(st, legal))                # the session is still healthy
        assert ws.receive_json()["accepted"] is True


def test_restart_replaces_the_match(client):
    with client.websocket_connect("/ws") as ws:
        start(ws, map_seed=1)
        _, a = read_until_my_turn(ws)
        start(ws, map_seed=2)
        _, b = read_until_my_turn(ws)
        assert a["match_id"] != b["match_id"]


def test_protocol_errors_do_not_kill_the_connection(client):
    with client.websocket_connect("/ws") as ws:
        ws.send_text("not json")
        assert ws.receive_json()["type"] == "ERROR"
        ws.send_json({"type": "ACTION"})                   # no match yet
        assert ws.receive_json()["type"] == "ERROR"
        ws.send_json({"type": "PING"})
        assert ws.receive_json()["type"] == "PONG"


def test_no_hidden_information_leaks(client):
    with client.websocket_connect("/ws") as ws:
        start(ws)
        _, st = read_until_my_turn(ws)
        blob = str(st)
        for secret in ("schedule", "rng", "dice", "seed"):
            assert secret not in blob
