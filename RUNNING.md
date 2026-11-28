# Running HexQuest: server, tournaments, tests and training

You need **two Git Bash windows**: one runs the server for Unity and stays open; the other is for
tournaments, tests and training. Tournaments do not need the server, so they work with it stopped too.

---

## Window 1: the server (leave it open)

1. Open Git Bash and paste:

   ```bash
   cd "/r/Hagat Mohema/26F/DIA/PROJECT/HEXQUEST/backend"
   source .venv/Scripts/activate
   python -m uvicorn hexquest.server.app:app --port 8765
   ```

2. You should see a line ending in `Uvicorn running on http://127.0.0.1:8765`. The window stays busy,
   which is normal.
3. Check it works: open `http://127.0.0.1:8765/health` in a browser. It should show `{"ok":true,...}`.
4. Press **Play** in Unity.
5. Stop the server any time with **Ctrl+C** in this window.

---

## Window 2: tournaments, tests, training

Open a **new** Git Bash window (do not reuse window 1) and paste:

```bash
cd "/r/Hagat Mohema/26F/DIA/PROJECT/HEXQUEST/backend"
source .venv/Scripts/activate
```

The prompt must now start with `(.venv)`. If it does not, the commands below fail with
"module not found". Then run any of these.

### Quick check that everything works (about 6 seconds)

```bash
python -m pytest tests -q
```

### Tournaments

Each agent plays every seat in turn. The number after `--games` is how many games to play.

```bash
python -m hexquest.tournament --agents greedy random --games 200
python -m hexquest.tournament --agents learner greedy --games 200
python -m hexquest.tournament --agents planner greedy --games 40
python -m hexquest.tournament --agents learner planner greedy random --games 40
```

Tips:

- The **planner is slow**, so use fewer games with it. About 40 gives a rough picture.
- To go faster, add `--workers 4` to use four CPU cores. Check your core count first:
  `echo $NUMBER_OF_PROCESSORS`.
- To change the Planner's strength, put the settings in quotes (needed whenever a name has a colon):

  ```bash
  python -m hexquest.tournament --agents "planner:rollouts=30,horizon=3" greedy --games 60
  ```

### Retrain the Learner (about 3 minutes)

```bash
python -m hexquest.learn.trainer --games 3000
python -m hexquest.learn.evaluate --model models/learner_best.json --games 400
```

Restart the server (window 1) afterwards so it loads the new model.

---

## Reading a tournament result

You get one row per agent:

| Column | Meaning |
|---|---|
| **win rate** and **95% CI** | How often the agent wins, and how reliable that number is. If the interval includes the fair share (50% for two players), the difference could be luck: play more games to tighten it. |
| **margin** | Average score lead over the best opponent. |
| **lat p50/p95 ms** | How long a typical decision takes (median / slow end). |
| **fail** | Should be `0`. Anything else means an agent crashed or broke the rules. |

---

## Running both at once

You can, but tournaments use a lot of CPU, so Unity's opponents may feel slower while one runs.
If the game stutters, wait for the tournament to finish.

---

## Common problems

| Message | Cause and fix |
|---|---|
| `No module named hexquest` | You are not in the `backend` folder. Run `pwd` to check. |
| `No module named fastapi` or `uvicorn` | The `.venv` is not active (no `(.venv)` in the prompt), or the install did not finish. Run `pip install -r requirements.txt -r requirements-dev.txt`. |
| `Address already in use` | An old server is still running. Close that window, or start with `--port 8766` and update the address in the game's Settings. |

If something fails, copy the exact message and note which window it came from.
