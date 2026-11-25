# HexQuest

Turn-based hex strategy for 2-6 players. A Unity client talks over WebSocket to a Python server
that runs the authoritative rules engine and the AI agents (Random, Greedy, Planner, Learner).

```
<repo root>
├── Assets/Scripts/      Unity client (C#)             -> see "Unity" below
├── backend/             Python engine, agents, server, training
│   ├── hexquest/        engine, agents/, learn/, server/
│   ├── models/          learner_best.json (trained weights, commit this)
│   └── tests/           54 tests
├── Packages/ ProjectSettings/ ...                     (your Unity project, untouched)
└── README.md
```

## One-time setup

1. **Unity:** Unity 6 LTS, project from the *Universal 2D* template. Set
   *Edit > Project Settings > Player > Other Settings > Active Input Handling = Both* and restart the editor.
2. **Scene:** open `SampleScene`, create an Empty GameObject, add the **GameBootstrap** component.
3. **Python 3.10+:**
   ```
   cd backend
   python -m venv .venv
   .venv\Scripts\activate          # Windows;  macOS/Linux: source .venv/bin/activate
   pip install -r requirements.txt -r requirements-dev.txt
   ```

## Run the game (two terminals' worth of steps)

1. **Start the server** (keep it running):
   ```
   cd backend
   python -m uvicorn hexquest.server.app:app --port 8765
   ```
   Open http://127.0.0.1:8765/health - you should see `{"ok":true,...}`.
2. **Press Play in Unity** with *Use Real Server* ticked on GameBootstrap
   (URL `ws://127.0.0.1:8765/ws`).

You play seat 1 ("You"); the other seats are controlled by the agents listed under
**Match Config > Agents** on GameBootstrap. Examples (seat 2, seat 3, ...):

| Agents list | Opponents |
|---|---|
| `planner`, `greedy` | strongest search agent + a Greedy |
| `learner`, `greedy` | the trained Learner + a Greedy |
| `learner`, `planner`, `random` | all kinds (set Player Count to 4) |
| `planner:rollouts=30,horizon=4` | a faster, weaker Planner |

No Python? Untick *Use Real Server* to use the built-in mock (random agents, placeholder rules).

Controls: `1`-`6` choose action, click a highlighted hex, `Space` pass, `Esc` cancel,
right-drag / WASD pan, wheel zoom. Attacks need a second click to confirm.

## Agents without Unity (headless)

```
cd backend
python -m pytest tests -q                                                    # all tests
python -m hexquest.tournament --agents learner planner greedy random --games 40
python -m hexquest.tournament --agents greedy random --games 200 --workers 4
```

## Train the Learner

```
cd backend
python -m hexquest.learn.trainer --games 3000       # ~3 min; writes models/learner_best.json
python -m hexquest.learn.evaluate --model models/learner_best.json --games 400 --opponents greedy random
```
Restart the server afterwards so it reloads the new weights. If a new model is worse, restore the old
`learner_best.json` from Git.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Unity feed says "Cannot reach server" | Server not running / wrong port. Start it, or untick *Use Real Server*. |
| Mouse errors in the Console | Active Input Handling must be *Both*. |
| `ModuleNotFoundError: hexquest` | Run Python commands from inside `backend/`. |
| Agents too slow / too fast | `set HEXQUEST_AGENT_DELAY=0` (Windows) or `export HEXQUEST_AGENT_DELAY=0` before starting the server. |
| Learner plays like Greedy | No `models/learner_best.json` found: run from `backend/` or set `HEXQUEST_MODEL`. |
| WebGL build can't connect | `System.Net.WebSockets` is unsupported in WebGL; use desktop/mobile builds. |

## Status and limits

Rules numbers (costs, yields, combat) are placeholders in `backend/hexquest/rules.py` until frozen.
Not built yet: lobby scene, reconnect/snapshot recovery, seat takeover/handback, replay viewer,
agent inspector, multiple human seats, authentication.
