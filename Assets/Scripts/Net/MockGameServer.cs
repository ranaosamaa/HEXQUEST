using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HexQuest
{
    /// <summary>
    /// TEMPORARY stand-in for the Python/FastAPI authoritative server, so you can build and
    /// test the Unity client before the backend exists (proposal Week 2-3).
    /// The client never touches this class directly - only IGameConnection.
    /// All numbers below are PLACEHOLDERS until the rules config is frozen.
    /// Player 0 is the human; every other seat is a dumb random "agent".
    /// </summary>
    public class MockGameServer : MonoBehaviour, IGameConnection
    {
        public event Action<StateMessage> StateReceived;
        public event Action<ResultMessage> ResultReceived;
        public int LocalPlayerId => 0;

        const int ClaimCost = 5;
        const int ReinforceCost = 3;
        const int MaxGarrison = 9;
        const int MaxGatherLevel = 3;
        static int UpgradeCost(int level) => 8 + 6 * level;

        StateMessage state;
        Dictionary<HexCoord, CellData> cellMap;
        System.Random rng;
        MatchConfig config;

        // ------------------------------------------------------------ IGameConnection

        public void Connect(MatchConfig cfg)
        {
            StopAllCoroutines();
            config = cfg;
            rng = new System.Random(cfg.map_seed);
            BuildMatch();
            Publish();
        }

        public void Disconnect() { StopAllCoroutines(); }

        public void SubmitAction(ActionRequest req)
        {
            if (state.game_over) { Reject(req, "match_over"); return; }
            if (req.turn_id != state.turn_id || req.expected_state_revision != state.state_revision) { Reject(req, "stale_request"); return; }
            if (req.player_id != state.active_player) { Reject(req, "not_your_turn"); return; }

            var player = state.players[req.player_id];
            bool legal = LegalActions(player).Exists(a =>
                a.action_type == req.action_type && a.source == req.source && a.target == req.target);
            if (!legal) { Reject(req, "illegal_action"); return; }

            Resolve(player, req.action_type, req.source, req.target);
            AdvanceTurn();
            Publish();
            if (!state.game_over) StartCoroutine(RunAgents());
        }

        // ------------------------------------------------------------ match setup

        void BuildMatch()
        {
            state = new StateMessage
            {
                match_id = "m" + config.map_seed + "-mock",
                turn_id = 1, round = 1, max_rounds = config.rounds,
                active_player = 0, state_revision = 1,
            };
            cellMap = new Dictionary<HexCoord, CellData>();

            foreach (var c in HexCoord.InRadius(config.board_radius))
            {
                var cell = new CellData { coord = c, owner = -1, deposit = rng.Next(100) < 30 ? rng.Next(1, 4) : 0 };
                state.cells.Add(cell);
                cellMap[c] = cell;
            }

            int n = Mathf.Clamp(config.player_count, 2, 6);
            for (int i = 0; i < n; i++)
            {
                var dir = HexCoord.Directions[i * 6 / n];
                var start = new HexCoord(dir.q * config.board_radius, dir.r * config.board_radius);
                state.players.Add(new PlayerData
                {
                    player_id = i,
                    name = i == 0 ? "You" : "Agent " + i,
                    seat_type = i == 0 ? SeatType.Human : SeatType.Agent,
                    controller_id = i == 0 ? "local-user" : "mock-agent-v0",
                    leader = start,
                    resources = 10,
                });
                var home = cellMap[start];
                home.owner = i; home.garrison = 2; home.deposit = 2;
            }
        }

        // ------------------------------------------------------------ rules (placeholder)

        static LegalAction L(ActionType t, HexCoord src, HexCoord dst, int cost)
            => new LegalAction { action_type = t, source = src, target = dst, cost = cost };

        bool LeaderAt(HexCoord c)
        {
            foreach (var p in state.players) if (p.active && p.leader == c) return true;
            return false;
        }

        bool TouchesOwner(HexCoord c, int owner)
        {
            for (int d = 0; d < 6; d++)
                if (cellMap.TryGetValue(c.Neighbor(d), out var nb) && nb.owner == owner) return true;
            return false;
        }

        List<LegalAction> LegalActions(PlayerData p)
        {
            var list = new List<LegalAction>();

            for (int d = 0; d < 6; d++)
            {
                var n = p.leader.Neighbor(d);
                if (cellMap.TryGetValue(n, out var moveCell) && moveCell.status == CellStatus.Normal && !LeaderAt(n))
                    list.Add(L(ActionType.Move, p.leader, n, 0));
            }

            foreach (var c in state.cells)
            {
                if (c.status != CellStatus.Normal) continue;

                if (c.owner == -1 && p.resources >= ClaimCost && TouchesOwner(c.coord, p.player_id))
                    list.Add(L(ActionType.Claim, c.coord, c.coord, ClaimCost));

                if (c.owner != p.player_id) continue;

                if (p.resources >= ReinforceCost && c.garrison < MaxGarrison)
                    list.Add(L(ActionType.Reinforce, c.coord, c.coord, ReinforceCost));

                if (c.garrison >= 2)
                    for (int d = 0; d < 6; d++)
                    {
                        var n = c.coord.Neighbor(d);
                        if (cellMap.TryGetValue(n, out var enemy) && enemy.status == CellStatus.Normal
                            && enemy.owner >= 0 && enemy.owner != p.player_id)
                            list.Add(L(ActionType.Attack, c.coord, n, 0));
                    }
            }

            list.Add(L(ActionType.Gather, p.leader, p.leader, 0));
            if (p.gather_level < MaxGatherLevel && p.resources >= UpgradeCost(p.gather_level))
                list.Add(L(ActionType.Upgrade, p.leader, p.leader, UpgradeCost(p.gather_level)));
            list.Add(L(ActionType.Pass, p.leader, p.leader, 0));
            return list;
        }

        int RollDice(int units)
        {
            int sum = 0;
            for (int i = 0; i < units; i++) sum += rng.Next(1, 7);
            return sum;
        }

        void Resolve(PlayerData p, ActionType type, HexCoord src, HexCoord dst)
        {
            string text;
            int amount = 0, rollA = 0, rollB = 0;
            bool won = false;
            switch (type)
            {
                case ActionType.Move:
                    p.leader = dst;
                    text = $"{p.name} moves to {dst}";
                    break;

                case ActionType.Claim:
                {
                    var cell = cellMap[dst];
                    p.resources -= ClaimCost;
                    cell.owner = p.player_id; cell.garrison = 1;
                    text = $"{p.name} claims {dst}";
                    break;
                }

                case ActionType.Reinforce:
                {
                    var cell = cellMap[dst];
                    p.resources -= ReinforceCost;
                    cell.garrison++;
                    text = $"{p.name} reinforces {dst}";
                    break;
                }

                case ActionType.Attack:
                {
                    var atkCell = cellMap[src];
                    var defCell = cellMap[dst];
                    int atk = RollDice(atkCell.garrison);
                    int def = RollDice(defCell.garrison);
                    bool win = atk > def;                       // ties favour the defender
                    rollA = atk; rollB = def; won = win;
                    string defender = state.players[defCell.owner].name;
                    if (win)
                    {
                        defCell.owner = p.player_id;
                        defCell.garrison = Mathf.Max(1, atkCell.garrison - 1);
                        atkCell.garrison = 1;
                    }
                    else atkCell.garrison = Mathf.Max(1, atkCell.garrison - 1);
                    text = $"{p.name} attacks {defender} at {dst}: {atk} vs {def} - {(win ? "captured!" : "repelled")}";
                    break;
                }

                case ActionType.Gather:
                {
                    int gain = 1 + p.gather_level + cellMap[p.leader].deposit;
                    p.resources += gain;
                    amount = gain;
                    text = $"{p.name} gathers +{gain}";
                    break;
                }

                case ActionType.Upgrade:
                    p.resources -= UpgradeCost(p.gather_level);
                    p.gather_level++;
                    text = $"{p.name} upgrades gathering to Lv {p.gather_level}";
                    break;

                default:
                    text = $"{p.name} passes";
                    break;
            }

            ResultReceived?.Invoke(new ResultMessage
            {
                actor = p.player_id, action_type = type, accepted = true, summary = text,
                target = dst, amount = amount, roll_a = rollA, roll_b = rollB, won = won,
            });
        }

        void AdvanceTurn()
        {
            state.turn_id++;
            state.state_revision++;

            int n = state.players.Count;
            int next = (state.active_player + 1) % n;
            if (next <= state.active_player) state.round++;
            state.active_player = next;

            if (state.round > state.max_rounds)
            {
                state.game_over = true;
                state.active_player = -1;
            }
        }

        void Reject(ActionRequest req, string code)
        {
            ResultReceived?.Invoke(new ResultMessage
            {
                actor = req.player_id, action_type = req.action_type, accepted = false,
                error = code, summary = "Rejected: " + code,
            });
        }

        // ------------------------------------------------------------ publishing + fake agents

        void Publish()
        {
            foreach (var p in state.players)
            {
                int owned = 0;
                foreach (var c in state.cells) if (c.owner == p.player_id) owned++;
                p.score = owned * 10 + p.resources;
            }

            state.legal_actions = new List<LegalAction>();
            if (!state.game_over)
            {
                var active = state.players[state.active_player];
                if (active.seat_type == SeatType.Human) state.legal_actions = LegalActions(active);
            }

            // Round-trip through JSON so the client never shares memory with the "server".
            var copy = JsonUtility.FromJson<StateMessage>(JsonUtility.ToJson(state));
            StateReceived?.Invoke(copy);
        }

        IEnumerator RunAgents()
        {
            while (!state.game_over && state.players[state.active_player].seat_type == SeatType.Agent)
            {
                yield return new WaitForSeconds(0.45f);
                var p = state.players[state.active_player];
                var choice = ChooseAgentAction(LegalActions(p));
                Resolve(p, choice.action_type, choice.source, choice.target);
                AdvanceTurn();
                Publish();
            }
        }

        static float Weight(ActionType t)
        {
            switch (t)
            {
                case ActionType.Attack: return 3f;
                case ActionType.Claim: return 3f;
                case ActionType.Gather: return 2f;
                case ActionType.Upgrade: return 1f;
                case ActionType.Reinforce: return 1f;
                case ActionType.Move: return 0.3f;
                default: return 0.2f;
            }
        }

        LegalAction ChooseAgentAction(List<LegalAction> legal)
        {
            float total = 0f;
            foreach (var a in legal) total += Weight(a.action_type);
            float roll = (float)rng.NextDouble() * total;
            foreach (var a in legal)
            {
                roll -= Weight(a.action_type);
                if (roll <= 0f) return a;
            }
            return legal[legal.Count - 1];
        }
    }
}
