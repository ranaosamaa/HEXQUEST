"""
Deterministic, Unity-free HexQuest rules engine (proposal Phase 0).

API
    new_game(n_players, seed, cfg)        -> GameState
    legal_actions(state, player)          -> list[Action]
    apply_action(state, action)           -> None (mutates; returns attack info for Attack)
    observation(state, player)            -> GameState view with hidden info removed
    scores(state) / winners(state)        -> final-score helpers
    state_hash(state)                     -> short hash for replay / integrity checks

An Action is a tuple (action_type, source_cell, target_cell). Cells are integer indices
into Board.coords. Gather/Upgrade/Pass use (type, -1, -1).

Determinism: dice come from a counter-style splitmix64 stream stored IN the state as one int,
so clone() is cheap and the same seed + same actions always yield the same game.
"""
import hashlib
import random
from typing import List, Optional, Tuple

from .board import get_board
from .rules import (ATTACK, CLAIM, DROP, GATHER, MOVE, PASS, REINFORCE, UPGRADE, RulesConfig)

Action = Tuple[int, int, int]
_MASK = (1 << 64) - 1


def _splitmix(state: int) -> Tuple[int, int]:
    state = (state + 0x9E3779B97F4A7C15) & _MASK
    z = state
    z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & _MASK
    z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & _MASK
    return state, z ^ (z >> 31)


class GameState:
    __slots__ = ("cfg", "board", "n", "round", "current", "done",
                 "owner", "garrison", "deposit", "blocked_until",
                 "leader", "resources", "level", "active",
                 "rng", "schedule", "steps")

    def clone(self) -> "GameState":
        s = GameState.__new__(GameState)
        s.cfg, s.board, s.n = self.cfg, self.board, self.n
        s.round, s.current, s.done = self.round, self.current, self.done
        s.owner = self.owner[:]
        s.garrison = self.garrison[:]
        s.deposit = self.deposit[:]
        s.blocked_until = self.blocked_until[:]
        s.leader = self.leader[:]
        s.resources = self.resources[:]
        s.level = self.level[:]
        s.active = self.active[:]
        s.rng = self.rng
        s.schedule = self.schedule  # never mutated after creation, safe to share
        s.steps = self.steps
        return s

    def is_blocked(self, cell: int) -> bool:
        return self.blocked_until[cell] > self.round


# ---------------------------------------------------------------------------- creation

def new_game(n_players: int, seed: int, cfg: Optional[RulesConfig] = None) -> GameState:
    cfg = cfg or RulesConfig()
    if not 2 <= n_players <= 6:
        raise ValueError("HexQuest supports 2-6 players")
    board = get_board(cfg.board_radius)

    s = GameState()
    s.cfg, s.board, s.n = cfg, board, n_players
    s.round, s.current, s.done, s.steps = 1, 0, False, 0
    s.owner = [-1] * board.n
    s.garrison = [0] * board.n
    s.blocked_until = [0] * board.n
    s.level = [0] * n_players
    s.resources = [cfg.start_resources] * n_players
    s.active = [True] * n_players

    map_rng = random.Random(f"map:{seed}")
    s.deposit = [map_rng.randint(1, cfg.deposit_max) if map_rng.random() < cfg.deposit_density else 0
                 for _ in range(board.n)]

    s.leader = []
    for p in range(n_players):
        c = board.start_cell(p, n_players)
        s.leader.append(c)
        s.owner[c] = p
        s.garrison[c] = cfg.start_garrison
        s.deposit[c] = cfg.start_deposit

    s.rng = int.from_bytes(hashlib.sha256(f"dice:{seed}".encode()).digest()[:8], "big")
    s.schedule = _build_schedule(board, cfg, n_players, seed)
    _apply_events(s)
    return s


def _build_schedule(board, cfg, n_players, seed):
    """Seeded event schedule. Kept inside the state but never shown to agents."""
    rng = random.Random(f"events:{seed}")
    schedule = {}
    for rnd in range(1, cfg.rounds + 1):
        events = []
        # Draw every random number each round so the stream is stable when probabilities change.
        r_shift, center, sign = rng.random(), rng.randrange(board.n), rng.choice((-1, 1))
        r_block, cells = rng.random(), tuple(rng.sample(range(board.n), cfg.block_count))
        r_drop, victim = rng.random(), rng.randrange(n_players)
        if r_shift < cfg.p_resource_shift:
            events.append(("shift", center, sign * cfg.shift_delta))
        if r_block < cfg.p_blocked_cells:
            events.append(("block", cells))
        if r_drop < cfg.p_dropout:
            events.append(("dropout", victim))
        if events:
            schedule[rnd] = events
    return schedule


def _apply_events(s: GameState) -> None:
    for ev in s.schedule.get(s.round, ()):
        kind = ev[0]
        if kind == "shift":
            _, center, delta = ev
            for c in range(s.board.n):
                if s.board.distance(center, c) <= s.cfg.shift_radius and s.deposit[c] > 0:
                    s.deposit[c] = max(0, min(s.cfg.deposit_max + 3, s.deposit[c] + delta))
        elif kind == "block":
            leaders = {s.leader[p] for p in range(s.n) if s.active[p]}
            for c in ev[1]:
                if c not in leaders:
                    s.blocked_until[c] = s.round + s.cfg.block_duration
        elif kind == "dropout":
            if sum(s.active) > 2 and s.active[ev[1]]:
                drop_player(s, ev[1])


def drop_player(s: GameState, p: int) -> None:
    """Remove a seat (dropout event, crash, timeout limit). Its cells stay on the board."""
    s.active[p] = False
    s.leader[p] = -1


# ---------------------------------------------------------------------------- rules

def legal_actions(s: GameState, p: int) -> List[Action]:
    if s.done or not s.active[p]:
        return []
    cfg, nbrs, owner, gar = s.cfg, s.board.nbrs, s.owner, s.garrison
    blocked, rnd, res = s.blocked_until, s.round, s.resources[p]
    out: List[Action] = []

    leader = s.leader[p]
    occupied = {s.leader[q] for q in range(s.n) if q != p and s.active[q]}
    for c in nbrs[leader]:
        if blocked[c] <= rnd and c not in occupied:
            out.append((MOVE, leader, c))

    can_claim = res >= cfg.claim_cost
    can_reinforce = res >= cfg.reinforce_cost
    for c in range(s.board.n):
        o = owner[c]
        if o == -1:
            if can_claim and blocked[c] <= rnd:
                for x in nbrs[c]:
                    if owner[x] == p:
                        out.append((CLAIM, c, c))
                        break
        elif o == p and blocked[c] <= rnd:
            g = gar[c]
            if can_reinforce and g < cfg.max_garrison:
                out.append((REINFORCE, c, c))
            if g >= 2:
                for x in nbrs[c]:
                    ox = owner[x]
                    if ox != -1 and ox != p and blocked[x] <= rnd:
                        out.append((ATTACK, c, x))

    out.append((GATHER, -1, -1))
    if s.level[p] < cfg.max_gather_level and res >= cfg.upgrade_cost(s.level[p]):
        out.append((UPGRADE, -1, -1))
    out.append((PASS, -1, -1))
    return out


def _roll(s: GameState, dice: int) -> int:
    total = 0
    for _ in range(dice):
        s.rng, z = _splitmix(s.rng)
        total += z % 6 + 1
    return total


def apply_action(s: GameState, action: Action, advance: bool = True):
    """Apply an action for the current player. The caller must have checked legality.

    Returns (attacker_roll, defender_roll, attacker_won) for Attack, otherwise None.
    """
    atype, src, dst = action
    p, cfg = s.current, s.cfg
    info = None

    if atype == MOVE:
        s.leader[p] = dst
    elif atype == CLAIM:
        s.resources[p] -= cfg.claim_cost
        s.owner[dst] = p
        s.garrison[dst] = 1
    elif atype == REINFORCE:
        s.resources[p] -= cfg.reinforce_cost
        s.garrison[dst] += 1
    elif atype == ATTACK:
        atk = _roll(s, s.garrison[src])
        dfn = _roll(s, s.garrison[dst])
        won = atk > dfn                      # ties favour the defender
        if won:
            s.owner[dst] = p
            s.garrison[dst] = max(1, s.garrison[src] - 1)
            s.garrison[src] = 1
        else:
            s.garrison[src] = max(1, s.garrison[src] - 1)
        info = (atk, dfn, won)
    elif atype == GATHER:
        s.resources[p] += cfg.gather_base + s.level[p] + s.deposit[s.leader[p]]
    elif atype == UPGRADE:
        s.resources[p] -= cfg.upgrade_cost(s.level[p])
        s.level[p] += 1
    # PASS: nothing

    s.steps += 1
    if advance:
        _advance(s)
    return info


def _advance(s: GameState) -> None:
    n = s.n
    nxt, wrapped, found = s.current, False, False
    for _ in range(n):
        nxt += 1
        if nxt == n:
            nxt, wrapped = 0, True
        if s.active[nxt]:
            found = True
            break
    if not found:
        s.done = True
        return
    if wrapped:
        s.round += 1
        if s.round > s.cfg.rounds:
            s.done = True
            return
        _apply_events(s)
        nxt = next((q for q in range(n) if s.active[q]), -1)
        if nxt == -1:
            s.done = True
            return
    s.current = nxt


# ---------------------------------------------------------------------------- views and results

def observation(s: GameState, player: int) -> GameState:
    """What a seat may know: the whole current board (public), but NOT future events or future dice."""
    o = s.clone()
    o.schedule = {}
    o.rng = 0
    return o


def scores(s: GameState) -> List[int]:
    cells = [0] * s.n
    for o in s.owner:
        if o >= 0:
            cells[o] += 1
    return [cells[p] * s.cfg.points_per_cell + s.resources[p] * s.cfg.points_per_resource for p in range(s.n)]


def territory(s: GameState) -> List[int]:
    cells = [0] * s.n
    for o in s.owner:
        if o >= 0:
            cells[o] += 1
    return cells


def winners(s: GameState) -> List[int]:
    sc, tr = scores(s), territory(s)
    best = max(zip(sc, tr))
    return [p for p in range(s.n) if (sc[p], tr[p]) == best]


def state_hash(s: GameState) -> str:
    payload = repr((s.round, s.current, s.done, s.owner, s.garrison, s.deposit, s.blocked_until,
                    s.leader, s.resources, s.level, s.active, s.rng))
    return hashlib.sha256(payload.encode()).hexdigest()[:16]


def advance_turn(s: GameState) -> None:
    """Pass the turn on. apply_action(..., advance=False) + advance_turn() == apply_action(...).
    Lets a trainer inspect the position right after its own move (the 'afterstate')."""
    _advance(s)
