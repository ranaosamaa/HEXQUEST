# HexQuest

A turn-based competitive strategy game on a hexagonal map for 2–6 players, where every seat can be a human or an AI agent. Players expand territory, gather resources, reinforce garrisons and attack rivals over 40 rounds. The project is both a playable multiplayer game and an environment for comparing AI decision-making approaches (Random, Greedy, Planner, Adaptive and a self-learning Learner).

This repository contains the **Unity client**. The authoritative rules engine, agents and training environment live in a separate backend (Python / FastAPI) that is planned for this project.

## Status

Early development. The client currently runs against a **mock server** that stands in for the real backend, so the game is playable locally with placeholder rules.

| Area | State |
|---|---|
| Hex board rendering (61 or 91 cells) | Done |
| HUD (turn order, resources, action bar, event feed, scoreboard) | Done |
| Human play: Move, Claim, Reinforce, Attack, Gather, Upgrade, Pass | Done (mock rules) |
| Mock server with random agent seats | Done (temporary) |
| Lobby and main menu scenes | Planned |
| WebSocket connection to the real server | Planned |
| Seat takeover and handback, reconnect | Planned |
| Replay viewer and agent inspector | Planned |
| Event animations, blocked-cell and resource-shift events | Planned |

## Requirements

- Unity 6 LTS (created from the **Universal 2D** template)
- Git
- Project setting: **Edit → Project Settings → Player → Other Settings → Active Input Handling = Both**

## Getting started

1. Clone the repository:
   ```
   git clone https://github.com/USERNAME/REPO.git
   ```
2. Open the folder in Unity Hub (Add → select the project folder) using Unity 6 LTS.
3. Set **Active Input Handling** to **Both** (see above) and restart the editor.
4. Open `Assets/Scenes/SampleScene`.
5. Make sure the scene has an empty GameObject with the **GameBootstrap** component. Add one if it is missing.
6. Press **Play**.

## Controls

| Input | Action |
|---|---|
| `1`–`6` | Choose Move, Claim, Reinforce, Attack, Gather, Upgrade |
| Click a highlighted hex | Apply the chosen action |
| Click an attack target twice | Confirm an attack |
| `Space` | Pass |
| `Esc` | Cancel the current selection |
| Right or middle mouse drag, `WASD` | Pan the camera |
| Mouse wheel | Zoom |

## Project structure

```
Assets/Scripts/
├── Core/   HexCoord: axial hex maths, neighbours, distance, world <-> hex
├── Data/   Models: STATE / ACTION / RESULT message classes (pure data)
├── Net/    IGameConnection, MockGameServer (temporary stand-in for the backend)
├── View/   HexGridView, CellView, LeaderView, CameraController, Palette, ProceduralArt
├── UI/     HudController, UIKit (HUD is built in code)
└── Game/   GameController (client logic), GameBootstrap (scene entry point)
```

## Architecture

HexQuest uses a **server-authoritative** design:

- The server is the only component that changes match state or decides whether an action is legal.
- The Unity client displays the latest `STATE` message, offers only the actions the server lists as legal, and sends the chosen `ACTION` back.
- Animation is cosmetic and only plays after the server confirms a result.
- Logical game data (`Models.cs`) is kept separate from GameObjects (`View/`).
- All networking goes through `IGameConnection`. `MockGameServer` implements it today; a WebSocket implementation will replace it without changing the rest of the client.

## Game rules (current placeholders)

Numbers such as costs, gathering yield and combat resolution are placeholders in `MockGameServer` until the rules configuration is frozen.

- **Move:** move your leader to an adjacent, unoccupied cell.
- **Claim:** spend resources to claim a neutral cell adjacent to your territory.
- **Reinforce:** spend resources to add a garrison unit to an owned cell.
- **Attack:** from an owned cell with at least 2 units, attack an adjacent enemy cell. Each side rolls one d6 per unit; the higher total wins and ties favour the defender.
- **Gather:** collect resources from the cell your leader occupies.
- **Upgrade:** spend resources to raise your gathering level.
- **Pass:** do nothing.
- **Scoring:** 10 points per owned cell plus 1 per unspent resource, after 40 rounds.

## Roadmap

1. Freeze the rules configuration and message protocol.
2. Lobby scene with human, agent and empty seats.
3. `WebSocketGameConnection` against the real game service, with Newtonsoft JSON.
4. Reconnect and snapshot recovery, seat takeover and handback, turn timers.
5. Replay viewer, agent inspector, event animations.
6. Final art pass to replace the procedurally generated meshes and sprites.

## Contributing

- Work on a branch and open a pull request into `main`.
- Do not commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` or generated `.csproj` / `.sln` files. The `.gitignore` excludes them.
- Always commit `.meta` files alongside their assets.
- Keep game rules out of the client. If logic decides what is legal, it belongs on the server.

## License

To be decided.
