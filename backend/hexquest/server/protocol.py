"""
Wire format shared with the Unity client (Assets/Scripts/Data/Models.cs).

Field names, nesting and integer enums match Unity's JsonUtility classes exactly:
HexCoord = {"q","r"}; ActionType/SeatType/CellStatus travel as ints.

Server -> client
    {"type":"STATE",  ...StateMessage fields}
    {"type":"RESULT", ...ResultMessage fields}
    {"type":"ERROR",  "message": "..."}
Client -> server
    {"type":"START",  "player_count","board_radius","rounds","map_seed","agents":[...]}
    {"type":"ACTION", ...ActionRequest fields}
"""
from typing import List, Optional

from ..engine import GameState, legal_actions, scores
from ..rules import ACTION_NAMES, ATTACK, CLAIM, GATHER, MOVE, PASS, REINFORCE, UPGRADE

SEAT_HUMAN, SEAT_AGENT = 1, 2
POSITIONAL = (MOVE, CLAIM, REINFORCE, ATTACK)


def _coord(s: GameState, cell: int) -> dict:
    q, r = s.board.coords[cell] if cell >= 0 else (0, 0)
    return {"q": q, "r": r}


def _cost(s: GameState, p: int, atype: int) -> int:
    cfg = s.cfg
    if atype == CLAIM:
        return cfg.claim_cost
    if atype == REINFORCE:
        return cfg.reinforce_cost
    if atype == UPGRADE:
        return cfg.upgrade_cost(s.level[p])
    return 0


def turn_id(s: GameState) -> int:
    return s.steps + 1


def state_message(s: GameState, match_id: str, names: List[str], controllers: List[str],
                  human_seat: int = 0) -> dict:
    sc = scores(s)
    cells = [{
        "coord": _coord(s, i), "owner": s.owner[i], "garrison": s.garrison[i],
        "deposit": s.deposit[i], "status": 1 if s.is_blocked(i) else 0,
    } for i in range(s.board.n)]

    players = [{
        "player_id": p, "name": names[p],
        "seat_type": SEAT_HUMAN if p == human_seat else SEAT_AGENT,
        "controller_id": controllers[p], "leader": _coord(s, s.leader[p]),
        "resources": s.resources[p], "gather_level": s.level[p],
        "active": s.active[p], "score": sc[p],
    } for p in range(s.n)]

    legal = []
    if not s.done and s.current == human_seat:
        leader = _coord(s, s.leader[human_seat])
        for atype, src, dst in legal_actions(s, human_seat):
            positional = atype in POSITIONAL
            legal.append({
                "action_type": atype,
                "source": _coord(s, src) if positional else leader,
                "target": _coord(s, dst) if positional else leader,
                "cost": _cost(s, human_seat, atype),
            })

    return {
        "type": "STATE", "protocol_version": 1, "match_id": match_id,
        "turn_id": turn_id(s), "round": min(s.round, s.cfg.rounds), "max_rounds": s.cfg.rounds,
        "active_player": -1 if s.done else s.current, "state_revision": turn_id(s),
        "game_over": s.done, "cells": cells, "players": players, "legal_actions": legal,
    }


def result_message(actor: int, atype: int, accepted: bool, summary: str, error: str = "",
                   target: Optional[dict] = None, amount: int = 0, roll_a: int = 0, roll_b: int = 0,
                   won: bool = False) -> dict:
    """RESULT message. target/amount/rolls/won feed the client's sound and visual effects."""
    return {"type": "RESULT", "actor": actor, "action_type": atype, "accepted": accepted,
            "error": error, "summary": summary, "target": target or {"q": 0, "r": 0},
            "amount": amount, "roll_a": roll_a, "roll_b": roll_b, "won": won}


def result_for_action(s_before: GameState, name: str, action: tuple, info, names: List[str]) -> dict:
    """Accepted-action RESULT built from the state BEFORE the action was applied."""
    atype, src, dst = action
    actor = s_before.current
    cell = dst if dst >= 0 else s_before.leader[actor]
    amount = roll_a = roll_b = 0
    won = False
    if atype == GATHER:
        amount = s_before.cfg.gather_base + s_before.level[actor] + s_before.deposit[s_before.leader[actor]]
    elif atype == ATTACK and info:
        roll_a, roll_b, won = info
    return result_message(actor, atype, True, describe(s_before, name, action, info, names),
                          target=_coord(s_before, cell), amount=amount, roll_a=roll_a, roll_b=roll_b, won=bool(won))


def parse_action(msg: dict, s: GameState) -> Optional[tuple]:
    """Convert a client ACTION message to an engine action tuple, or None if malformed."""
    try:
        atype = int(msg["action_type"])
        if atype in (GATHER, UPGRADE, PASS):
            return (atype, -1, -1)
        src = s.board.index[(int(msg["source"]["q"]), int(msg["source"]["r"]))]
        dst = s.board.index[(int(msg["target"]["q"]), int(msg["target"]["r"]))]
        return (atype, src, dst)
    except (KeyError, TypeError, ValueError):
        return None


def describe(s_before: GameState, name: str, action: tuple, info, names: List[str]) -> str:
    """Human-readable event-feed line. `s_before` is the state before the action was applied."""
    atype, src, dst = action
    where = s_before.board.coords[dst] if dst >= 0 else None
    if atype == MOVE:
        return f"{name} moves to {where}"
    if atype == CLAIM:
        return f"{name} claims {where}"
    if atype == REINFORCE:
        return f"{name} reinforces {where}"
    if atype == ATTACK:
        atk, dfn, won = info
        defender = names[s_before.owner[dst]]
        return f"{name} attacks {defender} at {where}: {atk} vs {dfn} - {'captured!' if won else 'repelled'}"
    if atype == GATHER:
        p = s_before.current
        gain = s_before.cfg.gather_base + s_before.level[p] + s_before.deposit[s_before.leader[p]]
        return f"{name} gathers +{gain}"
    if atype == UPGRADE:
        return f"{name} upgrades gathering to Lv {s_before.level[s_before.current] + 1}"
    return f"{name} passes"
