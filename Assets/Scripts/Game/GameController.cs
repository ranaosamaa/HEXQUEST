using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HexQuest
{
    /// <summary>
    /// The client's brain during a match. It NEVER changes game state: it shows the latest authoritative
    /// StateMessage, lets the human pick one of the server's legal actions, sends it back, and plays the
    /// sounds and effects for results the server reports.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public event Action<StateMessage> MatchFinished;
        public event Action<string, bool> ConnectionProblem;     // message, hadState
        public event Action PauseRequested;

        public bool InputEnabled = true;

        IGameConnection conn;
        HexGridView grid;
        HudController hud;
        CameraController camCtrl;
        Camera cam;
        FxManager fx;

        StateMessage state;
        ActionType? pending;
        HexCoord? attackTarget;
        bool awaitingServer, finishedRaised;
        int lastActive = -2;
        string infoKind = "";

        public bool HasPending { get { return pending.HasValue; } }
        bool IsMyTurn { get { return conn != null && state != null && !state.game_over && state.active_player == conn.LocalPlayerId; } }
        int Me { get { return conn != null ? conn.LocalPlayerId : 0; } }

        public void Init(HexGridView gridView, HudController hudController, CameraController cameraController,
                         Camera camera, FxManager effects)
        {
            grid = gridView; hud = hudController; camCtrl = cameraController; cam = camera; fx = effects;
            hud.ActionClicked += OnActionClicked;
            hud.PassClicked += SubmitPass;
            hud.CancelClicked += CancelSelection;
            hud.FocusClicked += FocusOnLeader;
            hud.StatsClicked += ShowStats;
            hud.SeatsClicked += ShowSeats;
        }

        public void BeginMatch(IGameConnection connection, MatchConfig cfg)
        {
            EndMatch();
            conn = connection;
            conn.StateReceived += OnState;
            conn.ResultReceived += OnResult;
            hud.ClearFeed();
            hud.HideInfo();
            hud.SetBanner("Summoning the realm...");
            conn.Connect(cfg);
        }

        public void EndMatch()
        {
            if (conn != null)
            {
                conn.StateReceived -= OnState;
                conn.ResultReceived -= OnResult;
                conn.Disconnect();
                conn = null;
            }
            if (grid != null) grid.Clear();
            state = null; pending = null; attackTarget = null;
            awaitingServer = false; finishedRaised = false; lastActive = -2;
        }

        void OnDestroy() { EndMatch(); }

        /// <summary>Escape: cancel a half-made choice first, otherwise ask for the pause menu.</summary>
        public void HandleEscape()
        {
            if (pending.HasValue || hud.InfoVisible) { CancelSelection(); hud.HideInfo(); return; }
            if (PauseRequested != null) PauseRequested();
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
            hud.Refresh(s, Me, null);

            if (s.game_over)
            {
                hud.SetBanner("The quest is over");
                if (!finishedRaised)
                {
                    finishedRaised = true;
                    if (MatchFinished != null) MatchFinished(s);
                }
            }
            else if (IsMyTurn)
            {
                hud.SetBanner("Your turn - choose a deed", lastActive != Me);
                if (lastActive != Me && !first) AudioManager.PlaySfx("turn", 0.8f, 0f);
            }
            else hud.SetBanner(s.players[s.active_player].name + " is plotting...");
            lastActive = s.active_player;
        }

        void OnResult(ResultMessage r)
        {
            if (!r.accepted)
            {
                awaitingServer = false;
                hud.AddFeed(r.summary, true);
                if (r.error == "connection")
                {
                    if (ConnectionProblem != null) ConnectionProblem(r.summary, state != null);
                }
                else AudioManager.PlaySfx("error", 0.7f, 0f);
                return;
            }
            hud.AddFeed("<color=#" + Palette.Hex(Palette.ForPlayer(r.actor)) + ">> </color>" + r.summary);
            PlayEffects(r);
        }

        void PlayEffects(ResultMessage r)
        {
            if (!grid.IsBuilt) return;
            Vector3 target = grid.WorldPosition(r.target);
            Vector3 hero = grid.LeaderPosition(r.actor, target);
            Color col = Palette.ForPlayer(r.actor);
            Vector3 up = new Vector3(0f, 0.55f, 0f);
            switch (r.action_type)
            {
                case ActionType.Move:
                    AudioManager.PlaySfx("move", 0.8f);
                    break;
                case ActionType.Claim:
                    AudioManager.PlaySfx("claim", 0.9f);
                    fx.Ring(target, col);
                    fx.FloatText(target + up, "Claimed", Palette.Cream, 0.9f);
                    break;
                case ActionType.Reinforce:
                    AudioManager.PlaySfx("reinforce", 0.9f);
                    fx.Spark(target, Palette.Gold, 1.2f);
                    fx.FloatText(target + up, "+1 soldier", Palette.Gold, 0.9f);
                    break;
                case ActionType.Attack:
                    AudioManager.PlaySfx("attack", 1f);
                    AudioManager.PlaySfx(r.won ? "claim" : "block", 0.8f);
                    fx.Slash(target, Color.white);
                    if (r.won) fx.Ring(target, col, 0.5f, 2.8f, 0.8f);
                    fx.FloatText(target + up, r.roll_a + "  vs  " + r.roll_b, Palette.Cream, 0.95f);
                    fx.FloatText(target + up * 1.9f, r.won ? "Captured!" : "Repelled", r.won ? Palette.Gold : Palette.Danger, 1.15f);
                    camCtrl.Shake(0.14f, 0.28f);
                    break;
                case ActionType.Gather:
                    AudioManager.PlaySfx("gather", 0.9f);
                    fx.CoinBurst(hero + up);
                    fx.FloatText(hero + up * 1.7f, "+" + r.amount, Palette.Gold, 1.2f);
                    break;
                case ActionType.Upgrade:
                    AudioManager.PlaySfx("upgrade", 0.9f);
                    fx.Spark(hero + up, Palette.Good, 1.5f);
                    fx.FloatText(hero + up * 1.9f, "Level up!", Palette.Good, 1.1f);
                    break;
                default:
                    AudioManager.PlaySfx("pass", 0.7f);
                    break;
            }
        }

        // =============================================================== input

        void Update()
        {
            if (state == null || !InputEnabled) return;
            if (!IsMyTurn || awaitingServer) return;

            if (Input.GetKeyDown(KeyCode.Space)) SubmitPass();
            for (int i = 0; i < HudController.CardOrder.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) OnActionClicked(HudController.CardOrder[i]);

            if (!pending.HasValue || !Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Vector2 world = cam.ScreenToWorldPoint(Input.mousePosition);
            HexCoord coord;
            if (!grid.TryPick(world, out coord)) return;

            var type = pending.Value;
            var matches = state.legal_actions.FindAll(a => a.action_type == type && a.target == coord);
            if (matches.Count == 0) return;

            var chosen = matches[0];
            if (type == ActionType.Attack) chosen = StrongestAttacker(matches);

            if (type == ActionType.Attack && !(attackTarget.HasValue && attackTarget.Value == coord))
            {
                attackTarget = coord;
                HighlightTargets(type);
                grid.SetHighlight(coord, Highlight.Selected);
                hud.SetBanner("Click again to confirm the attack");
                AudioManager.PlaySfx("ui_click", 0.8f);
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
            if (!InputEnabled || !IsMyTurn || awaitingServer) return;

            if (type == ActionType.Gather || type == ActionType.Upgrade)
            {
                var a = state.legal_actions.Find(x => x.action_type == type);
                if (a != null) Submit(a);
                return;
            }

            if (!state.legal_actions.Exists(x => x.action_type == type)) return;
            pending = type;
            attackTarget = null;
            hud.HideInfo();
            HighlightTargets(type);
            hud.Refresh(state, Me, pending);
            hud.SetBanner(type + ": click a glowing hex");
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
            hud.Refresh(state, Me, null);
            if (IsMyTurn) hud.SetBanner("Your turn - choose a deed");
        }

        void SubmitPass()
        {
            if (!InputEnabled || !IsMyTurn || awaitingServer) return;
            var a = state.legal_actions.Find(x => x.action_type == ActionType.Pass);
            if (a != null) Submit(a);
        }

        void Submit(LegalAction a)
        {
            awaitingServer = true;
            pending = null; attackTarget = null;
            grid.ClearHighlights();
            hud.SetBanner("Awaiting the realm...");

            conn.SubmitAction(new ActionRequest
            {
                match_id = state.match_id,
                turn_id = state.turn_id,
                player_id = Me,
                action_type = a.action_type,
                source = a.source,
                target = a.target,
                request_id = Guid.NewGuid().ToString(),
                expected_state_revision = state.state_revision,
            });
        }

        // =============================================================== info panels

        void FocusOnLeader()
        {
            if (state == null) return;
            camCtrl.FocusOn(grid.WorldPosition(state.players[Me].leader));
        }

        List<PlayerData> Standings()
        {
            var list = new List<PlayerData>(state.players);
            list.Sort((a, b) => b.score.CompareTo(a.score));
            return list;
        }

        static string Tag(int id) { return "<color=#" + Palette.Hex(Palette.Darken(Palette.ForPlayer(id), 0.72f)) + ">"; }

        void ShowStats()
        {
            if (state == null) return;
            if (hud.InfoVisible && infoKind == "stats") { hud.HideInfo(); return; }
            infoKind = "stats";
            var owned = new int[state.players.Count];
            foreach (var c in state.cells) if (c.owner >= 0) owned[c.owner]++;
            var sb = new StringBuilder();
            int rank = 1;
            foreach (var p in Standings())
                sb.Append(rank++).Append(".  ").Append(Tag(p.player_id)).Append(p.name).Append("</color>   ")
                  .Append(p.score).Append(" pts\n      ").Append(owned[p.player_id]).Append(" hexes, ")
                  .Append(p.resources).Append(" coins, gather Lv ").Append(p.gather_level).Append("\n");
            sb.Append("\nScore: 10 per hex + 1 per coin.");
            hud.ShowInfo("Scoreboard", sb.ToString());
        }

        void ShowSeats()
        {
            if (state == null) return;
            if (hud.InfoVisible && infoKind == "seats") { hud.HideInfo(); return; }
            infoKind = "seats";
            var sb = new StringBuilder();
            foreach (var p in state.players)
                sb.Append(Tag(p.player_id)).Append(p.name).Append("</color>\n      ")
                  .Append(p.seat_type == SeatType.Human ? "Human" : "AI").Append("  -  ").Append(p.controller_id)
                  .Append(p.active ? "" : "  (left the quest)").Append("\n");
            hud.ShowInfo("Seats", sb.ToString());
        }
    }
}
