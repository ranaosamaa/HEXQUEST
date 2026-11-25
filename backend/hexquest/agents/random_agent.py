from .base import Agent


class RandomAgent(Agent):
    """Uniform choice over legal actions, using its own seeded RNG."""
    name = "random"

    def choose(self, obs, seat, legal, deadline):
        self.last_trace = {"n_legal": len(legal)}
        return self.rng.choice(legal)
