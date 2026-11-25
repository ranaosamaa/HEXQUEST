"""Save / load learned weight files. A model file is plain JSON so it can be versioned in Git."""
import hashlib
import json
import os
from pathlib import Path
from typing import Optional, Sequence, Tuple

from .features import DEFAULT_WEIGHTS, FEATURE_NAMES

MODEL_FORMAT = 1
REPO_MODEL = Path(__file__).resolve().parents[2] / "models" / "learner_best.json"


def model_id(weights: Sequence[float]) -> str:
    return hashlib.sha1(",".join(f"{w:.5f}" for w in weights).encode()).hexdigest()[:8]


def save_model(path, weights: Sequence[float], meta: Optional[dict] = None) -> None:
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    doc = {"format": MODEL_FORMAT, "feature_names": list(FEATURE_NAMES),
           "weights": [float(w) for w in weights], "model_id": model_id(weights), "meta": meta or {}}
    with open(path, "w") as f:
        json.dump(doc, f, indent=1)


def load_model(path) -> Tuple[Tuple[float, ...], dict]:
    with open(path) as f:
        doc = json.load(f)
    if doc.get("feature_names") != list(FEATURE_NAMES):
        raise ValueError(f"{path}: feature set differs from this code version; retrain the model")
    return tuple(doc["weights"]), doc.get("meta", {})


def resolve_model(path: Optional[str] = None) -> Tuple[Tuple[float, ...], str]:
    """Find the weights to play with. Order: explicit path, $HEXQUEST_MODEL, ./models, repo models.
    If nothing is found, fall back to the hand-tuned weights (i.e. behave like Greedy)."""
    candidates = [path, os.environ.get("HEXQUEST_MODEL"), "models/learner_best.json", str(REPO_MODEL)]
    for c in candidates:
        if c and Path(c).is_file():
            w, _ = load_model(c)
            return w, model_id(w)
    return DEFAULT_WEIGHTS, "untrained"
