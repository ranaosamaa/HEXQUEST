
from .greedy import GreedyAgent
from .model_io import model_id, resolve_model


class LearnerAgent(GreedyAgent):
    """
    One-step lookahead with LEARNED weights: V(s) = w . features(s).

    At match time (epsilon = 0) the weights are frozen and loaded from a versioned file, so results
    are reproducible. During training the trainer sets epsilon > 0 for exploration and updates the
    weights after every game (see hexquest/learn/trainer.py).
    If no model file exists it falls back to the hand-tuned weights, i.e. it plays like Greedy.
    """
    name = "learner"

    def __init__(self, seed=0, path=None, weights=None, epsilon=0.0):
        if weights is not None:
            w, mid = tuple(weights), model_id(weights)
        else:
            w, mid = resolve_model(path)
        super().__init__(seed, weights=w)
        self._model_id = mid
        self.epsilon = float(epsilon)

    @property
    def version(self):                      # shows up in match logs / the Unity Seats panel
        return self._model_id

    def choose(self, obs, seat, legal, deadline):
        if self.epsilon > 0 and self.rng.random() < self.epsilon:
            self.last_trace = {"n_legal": len(legal), "explore": True}
            return self.rng.choice(legal)
        return super().choose(obs, seat, legal, deadline)
