import random

import pytest

from hexquest.engine import (apply_action, legal_actions, new_game, observation, scores,
                             state_hash, winners)
from hexquest.rules import ATTACK, PASS, RulesConfig


def random_game(n, seed, cfg=None, policy_seed=0):
    s = new_game(n, seed, cfg)
    rng = random.Random(policy_seed)
    log = []
    while not s.done:
        a = rng.choice(legal_actions(s, s.current))
        log.append((s.current, a))
        apply_action(s, a)
    return s, log


def test_board_sizes():
    assert new_game(2, 0, RulesConfig(board_radius=4)).board.n == 61
    assert new_game(2, 0, RulesConfig(board_radius=5)).board.n == 91


def test_player_count_bounds():
    with pytest.raises(ValueError):
        new_game(1, 0)
    with pytest.raises(ValueError):
        new_game(7, 0)


@pytest.mark.parametrize("n", [2, 3, 4, 5, 6])
def test_full_match_completes_with_all_player_counts(n):
    s, log = random_game(n, seed=n)
    assert s.done
    assert len(log) == n * s.cfg.rounds           # one action per player per round
    assert winners(s)


def test_same_seed_same_actions_is_deterministic():
    s1, log1 = random_game(3, 42)
    s2, log2 = random_game(3, 42)
    assert log1 == log2 and state_hash(s1) == state_hash(s2)


def test_replaying_the_action_log_reproduces_the_final_state():
    s1, log = random_game(4, 7)
    s2 = new_game(4, 7)
    for seat, a in log:
        assert seat == s2.current
        apply_action(s2, a)
    assert state_hash(s1) == state_hash(s2)


def test_different_seeds_differ():
    assert state_hash(random_game(3, 1)[0]) != state_hash(random_game(3, 2)[0])


def test_clone_is_independent():
    s = new_game(3, 5)
    c = s.clone()
    apply_action(c, legal_actions(c, 0)[0])
    assert state_hash(s) == state_hash(new_game(3, 5))


def test_every_legal_action_can_be_applied_without_error():
    for seed in range(20):
        s = new_game(3, seed)
        rng = random.Random(seed)
        while not s.done:
            for a in legal_actions(s, s.current):
                apply_action(s.clone(), a)
            apply_action(s, rng.choice(legal_actions(s, s.current)))


def test_pass_and_gather_always_available_and_resources_never_negative():
    s = new_game(3, 3)
    rng = random.Random(3)
    while not s.done:
        legal = legal_actions(s, s.current)
        assert any(a[0] == PASS for a in legal)
        apply_action(s, rng.choice(legal))
        assert min(s.resources) >= 0 and min(s.garrison) >= 0


def test_attack_needs_two_units_and_adjacent_enemy():
    for seed in range(15):
        t = new_game(2, seed)
        rng = random.Random(seed)
        while not t.done:
            for a in legal_actions(t, t.current):
                if a[0] == ATTACK:
                    assert t.garrison[a[1]] >= 2
                    assert t.owner[a[1]] == t.current
                    assert t.owner[a[2]] not in (-1, t.current)
                    assert a[2] in t.board.nbrs[a[1]]
            apply_action(t, rng.choice(legal_actions(t, t.current)))


def test_ties_favour_defender():
    s = new_game(2, 0)
    # force a 1v1... attack needs >=2 attackers, so craft an attack where defender garrison is huge
    p = s.current
    src = s.leader[p]
    nb = s.board.nbrs[src][0]
    s.owner[nb], s.garrison[nb] = 1 - p, 9
    s.garrison[src] = 2
    wins = sum(apply_action(_with_seed(s, k), (ATTACK, src, nb))[2] for k in range(200))
    assert wins < 5                                # 2 dice almost never beat 9 dice


def _with_seed(s, k):
    c = s.clone()
    c.rng = k * 7919 + 1
    return c


def test_observation_hides_future_events_and_dice():
    cfg = RulesConfig(p_resource_shift=1.0, p_blocked_cells=1.0)
    s = new_game(3, 9, cfg)
    assert s.schedule                              # real state knows the schedule
    o = observation(s, 0)
    assert o.schedule == {} and o.rng == 0         # an agent's view does not


def test_scoring_formula_and_tiebreak():
    s = new_game(2, 0)
    s.owner = [-1] * s.board.n
    s.owner[0] = s.owner[1] = 0
    s.owner[2] = 1
    s.resources = [0, 20]
    assert scores(s) == [20, 30]
    s.resources = [10, 20]                         # 30 vs 30 -> territory breaks the tie
    assert winners(s) == [0]


def test_blocked_cells_are_impassable_then_expire():
    cfg = RulesConfig(p_blocked_cells=1.0, block_count=10, block_duration=2, p_resource_shift=0)
    s = new_game(2, 4, cfg)
    blocked = [c for c in range(s.board.n) if s.is_blocked(c)]
    assert blocked
    for a in legal_actions(s, 0):
        if a[2] >= 0:                              # Move / Claim / Attack / Reinforce targets
            assert not s.is_blocked(a[2])
    s.round += 2
    assert not any(s.is_blocked(c) for c in blocked)


def test_dropout_event_removes_a_seat_but_keeps_two_players():
    cfg = RulesConfig(p_dropout=1.0)
    s = new_game(4, 1, cfg)
    rng = random.Random(0)
    while not s.done:
        apply_action(s, rng.choice(legal_actions(s, s.current)))
    assert sum(s.active) == 2                      # never drops below two active players
