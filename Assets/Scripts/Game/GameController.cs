using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HexQuest
{
    /// <summary>
    /// The client's brain. It NEVER changes game state: it shows the latest authoritative
    /// StateMessage, lets the human pick one of the server's legal actions, and sends it back.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        IGameConnection conn;
        HexGridView grid;
        HudController hud;
        CameraController camCtrl;
        Camera cam;
        MatchConfig config;

        StateMessage state;
        ActionType? pending;          // action type chosen from the bar, waiting for a target click
        HexCoord? attackTarget;       // first click on an attack target (second click confirms)
        bool awaitingServer;

        bool IsMyTurn => state != null && !state.game_over && state.active_player == conn.LocalPlayerId;

        public void Init(IGameConnection connection, HexGridView gridView, HudController hudController,
                         CameraController cameraController, Camera camera, MatchConfig matchConfig)
        {
            conn = connection; grid = gridView; hud = hudController;
            camCtrl = cameraController; cam = camera; config = matchConfig;

            conn.StateReceived += OnState;
            conn.ResultReceived += OnResult;

            hud.ActionClicked += OnActionClicked;
            hud.PassClicked += SubmitPass;
            hud.CancelClicked += CancelSelection;
            hud.FocusClicked += FocusOnLeader;
            hud.StatsClicked += ShowStats;
            hud.SeatsClicked += ShowSeats;
            hud.MenuClicked += ShowMenu;
            hud.PanelButtonClicked += StartMatch;
        }

        void OnDestroy()
        {
            if (conn == null) return;
            conn.StateReceived -= OnState;
            conn.ResultReceived -= OnResult;
        }

        public void StartMatch()
        {
            grid.Clear();
            hud.ClearFeed();
            hud.HidePanel();
            state = null; pending = null; attackTarget = null; awaitingServer = false;
            conn.Connect(config);
        }

        // =============================================================== messages from the server

        void OnState(StateMessage s)
        {
            bool first = state == null;
            state = s;
            awaitingServer = false;
            pending = null;
            attackTarget = null;

            if (!grid.IsBuilt)
            {
                grid.Build(s, UIKit.Font);
                camCtrl.Frame(grid.WorldBounds);
            }
            grid.Refresh(s, first);
            grid.ClearHighlights();
            hud.Refresh(s, conn.LocalPlayerId, null);

            if (s.game_over) { hud.SetBanner("Match over"); ShowGameOver(s); }
            else if (IsMyTurn) hud.SetBanner("Your turn - pick an action");
            else hud.SetBanner(s.players[s.active_player].name + " is thinking...");
        }

        void OnResult(ResultMessage r)
        {
            hud.AddFeed(r.summary, !r.accepted);
            if (!r.accepted) awaitingServer = false;
        }

        // =============================================================== input

        void Update()
        {
            if (state == null) return;

            if (Input.GetKeyDown(KeyCode.Escape)) CancelSelection();
            if (!IsMyTurn || awaitingServer) return;

            if (Input.GetKeyDown(KeyCode.Space)) SubmitPass();
            for (int i = 0; i < HudController.CardOrder.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) OnActionClicked(HudController.CardOrder[i]);

            if (!pending.HasValue || !Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Vector2 world = cam.ScreenToWorldPoint(Input.mousePosition);
            if (!grid.TryPick(world, out var coord)) return;

            var type = pending.Value;
            var matches = state.legal_actions.FindAll(a => a.action_type == type && a.target == coord);
            if (matches.Count == 0) return;

            var chosen = matches[0];
            if (type == ActionType.Attack) chosen = StrongestAttacker(matches);

            // Attacks need a confirming second click on the same hex.
            if (type == ActionType.Attack && attackTarget != coord)
            {
                attackTarget = coord;
                HighlightTargets(type);
                grid.SetHighlight(coord, Highlight.Selected);
                hud.SetBanner("Click the hex again to CONFIRM the attack");
                return;
            }
            Submit(chosen);
        }

        LegalAction StrongestAttacker(List<LegalAction> options)
        {
            LegalAction best = options[0];
            int bestGarrison = -1;
            foreach (var o in options)
                foreach (var c in state.cells)
                    if (c.coord == o.source && c.garrison > bestGarrison) { bestGarrison = c.garrison; best = o; }
            return best;
        }

        void OnActionClicked(ActionType type)
        {
            if (!IsMyTurn || awaitingServer) return;

            // Instant actions: no target needed.
            if (type == ActionType.Gather || type == ActionType.Upgrade)
            {
                var a = state.legal_actions.Find(x => x.action_type == type);
                if (a != null) Submit(a);
                return;
            }

            if (!state.legal_actions.Exists(x => x.action_type == type)) return;
            pending = type;
            attackTarget = null;
            HighlightTargets(type);
            hud.Refresh(state, conn.LocalPlayerId, pending);
            hud.SetBanner(type + ": click a highlighted hex   (Esc = cancel)");
        }

        void HighlightTargets(ActionType type)
        {
            grid.ClearHighlights();
            foreach (var a in state.legal_actions)
                if (a.action_type == type)
                    grid.SetHighlight(a.target, type == ActionType.Attack ? Highlight.Attack : Highlight.Legal);
        }

        void CancelSelection()
        {
            if (state == null) return;
            pending = null; attackTarget = null;
            grid.ClearHighlights();
            hud.Refresh(state, conn.LocalPlayerId, null);
            if (IsMyTurn) hud.SetBanner("Your turn - pick an action");
        }

        void SubmitPass()
        {
            if (!IsMyTurn || awaitingServer) return;
            var a = state.legal_actions.Find(x => x.action_type == ActionType.Pass);
            if (a != null) Submit(a);
        }

        void Submit(LegalAction a)
        {
            awaitingServer = true;
            pending = null; attackTarget = null;
            grid.ClearHighlights();
            hud.SetBanner("Waiting for server...");

            conn.SubmitAction(new ActionRequest
            {
                match_id = state.match_id,
                turn_id = state.turn_id,
                player_id = conn.LocalPlayerId,
                action_type = a.action_type,
                source = a.source,
                target = a.target,
                request_id = Guid.NewGuid().ToString(),
                expected_state_revision = state.state_revision,
            });
        }

        // =============================================================== side buttons / panels

        void FocusOnLeader()
        {
            if (state == null) return;
            camCtrl.FocusOn(grid.WorldPosition(state.players[conn.LocalPlayerId].leader));
        }

        List<PlayerData> Standings()
        {
            var list = new List<PlayerData>(state.players);
            list.Sort((a, b) => b.score.CompareTo(a.score));
            return list;
        }

        void ShowStats()
        {
            if (state == null) return;
            var sb = new StringBuilder();
            int rank = 1;
            foreach (var p in Standings())
                sb.AppendLine($"{rank++}. {p.name}   {p.score} pts   (res {p.resources}, Lv {p.gather_level})");
            sb.AppendLine().Append("Score = 10 per cell + unspent resources.");
            hud.ShowPanel("Scoreboard", sb.ToString(), null);
        }

        void ShowSeats()
        {
            if (state == null) return;
            var sb = new StringBuilder();
            foreach (var p in state.players)
                sb.AppendLine($"Seat {p.player_id + 1}: {p.name}\n    {p.seat_type}  ({p.controller_id})");
            sb.AppendLine().Append("Takeover / hand-back arrives with SEAT_REQUEST in the real protocol.");
            hud.ShowPanel("Seats", sb.ToString(), null);
        }

        void ShowMenu() => hud.ShowPanel("Menu",
            "Mock match against random agents.\n\nControls:\n1-6  choose action\nSpace  pass\nEsc  cancel\nRight-drag / WASD  pan\nScroll  zoom",
            "Restart match");

        void ShowGameOver(StateMessage s)
        {
            var sb = new StringBuilder();
            var standings = Standings();
            sb.AppendLine("Winner: " + standings[0].name + "\n");
            int rank = 1;
            foreach (var p in standings) sb.AppendLine($"{rank++}. {p.name}   {p.score} pts");
            hud.ShowPanel("Game over", sb.ToString(), "Play again");
        }
    }
}
