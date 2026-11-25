"""Static hex board geometry (axial coordinates, pointy-top), shared by all states."""
from functools import lru_cache

DIRS = ((1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1))


class Board:
    __slots__ = ("radius", "coords", "index", "nbrs", "n")

    def __init__(self, radius: int):
        self.radius = radius
        coords = []
        for q in range(-radius, radius + 1):  # same order as the Unity client's HexCoord.InRadius
            for r in range(max(-radius, -q - radius), min(radius, -q + radius) + 1):
                coords.append((q, r))
        self.coords = tuple(coords)
        self.index = {c: i for i, c in enumerate(coords)}
        self.nbrs = tuple(
            tuple(self.index[(q + dq, r + dr)] for dq, dr in DIRS if (q + dq, r + dr) in self.index)
            for q, r in coords
        )
        self.n = len(coords)

    def start_cell(self, seat: int, n_players: int) -> int:
        dq, dr = DIRS[seat * 6 // n_players]
        return self.index[(dq * self.radius, dr * self.radius)]

    def distance(self, a: int, b: int) -> int:
        (q1, r1), (q2, r2) = self.coords[a], self.coords[b]
        dq, dr = q1 - q2, r1 - r2
        return (abs(dq) + abs(dr) + abs(dq + dr)) // 2


@lru_cache(maxsize=None)
def get_board(radius: int) -> Board:
    return Board(radius)
