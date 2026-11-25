"""
Shared, seat-relative evaluator (Phase 3). Greedy, Planner and (later) the Learner all use it.

evaluate(state, seat) = weights . features(state, seat)  ->  "how good is this position for me?"
The Learner will start from DEFAULT_WEIGHTS and improve them from game outcomes.
"""
from typing import List, Sequence

from ..engine import GameState

FEATURE_NAMES = (
    "cell_share",        # my cells / board size
    "cell_lead",         # my cells minus best opponent's, / board size
    "resources",         # unspent resources
    "score_margin",      # my final-score formula minus best opponent's  (THE goal)
    "income_here",       # what Gather would yield on my leader's cell
    "owned_deposit",     # deposit value sitting on my cells
    "garrison",          # total garrison on my cells
    "exposed",           # my cells next to an enemy cell with >= garrison
    "attack_options",    # my cells that out-garrison a neighbouring enemy cell
    "expandable",        # neutral cells I could claim
    "upgrade_early",     # gather level, fading as the game progresses
    "late_resources",    # resources, growing in value near the end
)

DEFAULT_WEIGHTS = (2.0, 2.0, 0.5, 10.0, 0.6, 1.0, 3.0, -2.0, 1.5, 0.8, 1.5, 0.0)


def features(s: GameState, p: int) -> List[float]:
    nbrs, owner, gar, dep = s.board.nbrs, s.owner, s.garrison, s.deposit
    blocked, rnd = s.blocked_until, s.round
    n_cells = s.board.n
    cells = [0] * s.n
    my_gar = my_dep = exposed = attack_opts = expandable = 0

    for c in range(n_cells):
        o = owner[c]
        if o == -1:
            if blocked[c] <= rnd:
                for x in nbrs[c]:
                    if owner[x] == p:
                        expandable += 1
                        break
            continue
        cells[o] += 1
        if o == p:
            my_gar += gar[c]
            my_dep += dep[c]
            is_exposed = can_attack = False
            for x in nbrs[c]:
                ox = owner[x]
                if ox >= 0 and ox != p:
                    if gar[x] >= gar[c]:
                        is_exposed = True
                    if gar[c] >= 2 and gar[c] > gar[x]:
                        can_attack = True
            exposed += is_exposed
            attack_opts += can_attack

    cfg = s.cfg
    sc = [cells[q] * cfg.points_per_cell + s.resources[q] * cfg.points_per_resource for q in range(s.n)]
    others = [q for q in range(s.n) if q != p]
    best_sc = max(sc[q] for q in others)
    best_cells = max(cells[q] for q in others)
    progress = (rnd - 1) / cfg.rounds
    leader = s.leader[p]
    income = (cfg.gather_base + s.level[p] + dep[leader]) if leader >= 0 else 0
    res = s.resources[p]

    return [
        cells[p] / n_cells,
        (cells[p] - best_cells) / n_cells,
        res / 30.0,
        (sc[p] - best_sc) / 100.0,
        income / 6.0,
        my_dep / 10.0,
        my_gar / (n_cells / 2.0),
        exposed / 8.0,
        attack_opts / 8.0,
        expandable / 12.0,
        s.level[p] * (1.0 - progress),
        (res / 30.0) * progress,
    ]


def evaluate(s: GameState, p: int, weights: Sequence[float] = DEFAULT_WEIGHTS) -> float:
    f = features(s, p)
    return sum(w * x for w, x in zip(weights, f))
