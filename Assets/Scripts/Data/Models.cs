using System;
using System.Collections.Generic;

namespace HexQuest
{
    // ---------------------------------------------------------------------
    // These classes mirror the proposal's message contract (STATE / ACTION /
    // RESULT). Field names are snake_case on purpose so JsonUtility can read
    // the same JSON your Python server will send. Enums travel as ints for now;
    // when you wire the real server, switch to Newtonsoft.Json (string enums).
    // ---------------------------------------------------------------------

    public enum SeatType { Empty = 0, Human = 1, Agent = 2 }
    public enum ActionType { Move, Claim, Reinforce, Attack, Gather, Upgrade, Pass }
    public enum CellStatus { Normal = 0, Blocked = 1 }

    [Serializable]
    public class CellData
    {
        public HexCoord coord;
        public int owner = -1;          // -1 = neutral, otherwise player_id
        public int garrison;
        public int deposit;             // resource deposit on this cell
        public CellStatus status;       // Blocked = impassable (event)
    }

    [Serializable]
    public class PlayerData
    {
        public int player_id;
        public string name;
        public SeatType seat_type;
        public string controller_id;    // user id, or agent + model version
        public HexCoord leader;
        public int resources;
        public int gather_level;
        public bool active = true;
        public int score;
    }

    [Serializable]
    public class LegalAction
    {
        public ActionType action_type;
        public HexCoord source;         // e.g. attacking cell, or leader cell
        public HexCoord target;         // hex the action is applied to
        public int cost;
    }

    /// <summary>STATE message: full authoritative snapshot + what the local seat may do.</summary>
    [Serializable]
    public class StateMessage
    {
        public int protocol_version = 1;
        public string match_id;
        public int turn_id;
        public int round;
        public int max_rounds;
        public int active_player = -1;
        public int state_revision;
        public bool game_over;
        public List<CellData> cells = new List<CellData>();
        public List<PlayerData> players = new List<PlayerData>();
        public List<LegalAction> legal_actions = new List<LegalAction>();
    }

    /// <summary>ACTION message: client -> server.</summary>
    [Serializable]
    public class ActionRequest
    {
        public string match_id;
        public int turn_id;
        public int player_id;
        public ActionType action_type;
        public HexCoord source;
        public HexCoord target;
        public string request_id;
        public int expected_state_revision;
    }

    /// <summary>RESULT message: server -> everyone. 'summary' is display text for the event feed.</summary>
    [Serializable]
    public class ResultMessage
    {
        public int actor;
        public ActionType action_type;
        public bool accepted;
        public string error;
        public string summary;
    }

    [Serializable]
    public class MatchConfig
    {
        public int player_count = 3;    // 2..6
        public int board_radius = 4;    // 4 -> 61 cells, 5 -> 91 cells
        public int rounds = 40;
        public int map_seed = 12345;
    }
}
