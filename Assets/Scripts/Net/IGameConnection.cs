using System;

namespace HexQuest
{
    /// <summary>
    /// Everything the client needs from "the server". MockGameServer implements it
    /// today; a WebSocketGameConnection implements it later. No other client code changes.
    /// </summary>
    public interface IGameConnection
    {
        event Action<StateMessage> StateReceived;
        event Action<ResultMessage> ResultReceived;

        int LocalPlayerId { get; }
        void Connect(MatchConfig config);
        void SubmitAction(ActionRequest request);
    }
}
