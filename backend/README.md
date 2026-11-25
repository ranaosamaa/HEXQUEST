# HexQuest backend: rules engine and agents

Unity-free Python implementation of the HexQuest rules engine, the agent interface, a headless
match runner, and Random / Greedy / Planner agents. Python 3.10+, no runtime dependencies
(`pytest` for tests).

```
hexquest/
  rules.py        RulesConfig (all tunable numbers, versioned) + action type constants
  board.py        hex geometry, neighbour tables
  engine.py       GameState, new_game, legal_actions, apply_action, observation, scores, state_hash
  runner.py       play_match (failure handling, logs, traces) and replay
  tournament.py   seat-rotating tournaments with confidence intervals (CLI)
  stats.py        Wilson interval, mean +- CI, percentiles
  agents/
    base.py       Agent interface
    random_agent.py
    features.py   shared seat-relative evaluator (Greedy, Planner, future Learner)
    common.py     expected_value + playout policy
    greedy.py     one-step lookahead
    planner.py    shortlist + Monte Carlo rollouts, goal = projected final score
    learner.py    one-step lookahead with LEARNED weights (loads models/learner_best.json)
    model_io.py   save / load / locate weight files
  learn/
    trainer.py    self-play training: weights updated after every game
    evaluate.py   paired, held-out evaluation CLI
  server/
    protocol.py   engine state <-> JSON exactly as Unity's Models.cs expects
    session.py    one match per WebSocket: human seat 0 + server-side agents
    app.py        FastAPI app: GET /health, WebSocket /ws
tests/            pytest suite (engine, determinism, agent contract, failure handling, strength)
```

## Run the server for Unity

```
pip install -r requirements.txt
python -m uvicorn hexquest.server.app:app --port 8765
# check: http://127.0.0.1:8765/health
```
Then press Play in Unity with *Use Real Server* ticked (default URL `ws://127.0.0.1:8765/ws`).
`HEXQUEST_AGENT_DELAY=0.35` (seconds) paces agent moves so the human can follow; set 0 for speed.

Protocol: client sends `START` then `ACTION`; server sends `STATE`, `RESULT`, `ERROR`
(see `server/protocol.py`). The server re-sends `STATE` after any rejected action to resync the client.

## Test and tournaments

```
pip install pytest
python -m pytest tests -q
python -m hexquest.tournament --agents greedy random --games 200
python -m hexquest.tournament --agents "planner:rollouts=100,horizon=5,top_k=8" greedy --games 100
python -m hexquest.tournament --agents planner greedy random --games 60 --events   # dropout events on
```

Agent specs are `name` or `name:key=value,key=value`. One spec per seat; seats rotate each game.

## Conventions

* An **action** is `(type, source_cell, target_cell)` with integer cell indices. Type values equal
  the Unity client's `ActionType` enum (Move=0 ... Pass=6).
* **Determinism**: board, events and dice derive from the match seed. Dice use a splitmix64 stream
  stored in the state, so the same seed plus the same actions reproduce the same match
  (`runner.replay`, `engine.state_hash`).
* **Information**: agents only receive `observation(state, seat)`: the public board, with the event
  schedule and future dice removed. Agents must `clone()` before simulating and never mutate it.
* **Failures**: illegal action, crash or timeout becomes Pass and is counted; a seat is dropped after
  `max_failures`. The match never crashes because of an agent.

## The Learner

Linear value function `V(s) = w . features(s)`, started from the hand-tuned weights (so it begins as
Greedy). After EVERY self-play game it compares its predictions with the real final score margin and
nudges the weights (lambda-return targets, learning rate decaying 0.02 -> 0.002, margin weight frozen).
Every 250 games the frozen weights play held-out seeds; the best checkpoint is saved as
`models/learner_best.json`. Weights are never updated during real matches, only in training.

```
python -m hexquest.learn.trainer --games 3000                      # ~3 min on one CPU
python -m hexquest.learn.evaluate --model models/learner_best.json --games 400 --opponents greedy random
python -m hexquest.tournament --agents learner greedy random --games 100
```

Use it anywhere an agent spec is accepted: `learner`, or `learner:path='other/model.json'`.
With no model file it silently behaves like Greedy. `HEXQUEST_MODEL=/path/model.json` overrides the location.

Measured on 400 held-out games (seats paired), shipped model:
vs Greedy 66.5% [61.7, 70.9], vs Random 100%; vs Planner (40 games) 40% [26, 55].
Training is unstable without learning-rate decay and checkpoint selection (see `models/training_log.csv`).
