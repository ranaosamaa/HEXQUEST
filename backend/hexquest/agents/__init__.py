import ast
from .base import Agent
from .random_agent import RandomAgent
from .greedy import GreedyAgent
from .planner import PlannerAgent
from .learner import LearnerAgent

REGISTRY = {"random": RandomAgent, "greedy": GreedyAgent, "planner": PlannerAgent, "learner": LearnerAgent}


def make_agent(spec: str, seed: int = 0) -> Agent:
    """'planner' or 'planner:rollouts=24,horizon=4' -> configured agent instance."""
    name, _, params = spec.partition(":")
    kwargs = {}
    for part in filter(None, params.split(",")):
        k, v = part.split("=")
        kwargs[k.strip()] = ast.literal_eval(v.strip())
    return REGISTRY[name](seed=seed, **kwargs)
