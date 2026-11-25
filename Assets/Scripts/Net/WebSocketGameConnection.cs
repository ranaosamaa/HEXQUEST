using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace HexQuest
{
    /// <summary>
    /// IGameConnection backed by the Python server (ws://host:port/ws).
    /// Network work runs on background tasks; every message is queued and delivered from Update(),
    /// so the rest of the client (which touches GameObjects) only ever runs on Unity's main thread.
    /// Works in the Editor and desktop/mobile builds. System.Net.WebSockets does NOT work in WebGL.
    /// </summary>
    public class WebSocketGameConnection : MonoBehaviour, IGameConnection
    {
        public event Action<StateMessage> StateReceived;
        public event Action<ResultMessage> ResultReceived;
        public int LocalPlayerId => 0;

        public string Url = "ws://127.0.0.1:8765/ws";

        [Serializable] class Envelope { public string type; }
        [Serializable] class ErrorMessage { public string message; }

        [Serializable]
        class StartRequest
        {
            public string type = "START";
            public int player_count, board_radius, rounds, map_seed;
            public System.Collections.Generic.List<string> agents;
        }

        [Serializable]
        class LocalNotice      // synthesised on the client for connection problems
        {
            public string type = "RESULT";
            public int actor = -1;
            public bool accepted = false;
            public string error = "connection";
            public string summary;
        }

        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        ClientWebSocket socket;
        CancellationTokenSource cts;
        int session;

        // ------------------------------------------------------------------ IGameConnection

        public void Connect(MatchConfig cfg)
        {
            Disconnect();
            session++;
            while (inbox.TryDequeue(out _)) { }

            // Serialise on the main thread, before any await.
            string startJson = JsonUtility.ToJson(new StartRequest
            {
                player_count = cfg.player_count, board_radius = cfg.board_radius,
                rounds = cfg.rounds, map_seed = cfg.map_seed, agents = cfg.agents,
            });
            _ = RunAsync(session, startJson);
        }

        public void SubmitAction(ActionRequest request)
        {
            var ws = socket;
            if (ws == null || ws.State != WebSocketState.Open)
            {
                Notice("Not connected to the server.");
                return;
            }
            string json = JsonUtility.ToJson(request);
            _ = SendAsync(ws, json, cts.Token, session);
        }

        // ------------------------------------------------------------------ background tasks

        async Task RunAsync(int mySession, string startJson)
        {
            var ws = new ClientWebSocket();
            var tokenSource = new CancellationTokenSource();
            socket = ws;
            cts = tokenSource;
            try
            {
                await ws.ConnectAsync(new Uri(Url), tokenSource.Token);
                await SendAsync(ws, startJson, tokenSource.Token, mySession);
                await ReceiveLoop(ws, mySession, tokenSource.Token);
                if (mySession == session && !tokenSource.IsCancellationRequested)
                    Notice("Server closed the connection.");
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (mySession == session)
                    Notice($"Cannot reach server at {Url}. Is it running?  ({e.Message})");
            }
        }

        async Task ReceiveLoop(ClientWebSocket ws, int mySession, CancellationToken token)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    ms.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (result.MessageType == WebSocketMessageType.Close) return;
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (mySession == session)
                        inbox.Enqueue(Encoding.UTF8.GetString(ms.ToArray()));
                }
            }
        }

        async Task SendAsync(ClientWebSocket ws, string json, CancellationToken token, int mySession)
        {
            bool locked = false;
            try
            {
                await sendLock.WaitAsync(token);      // WebSocket allows only one send at a time
                locked = true;
                var bytes = Encoding.UTF8.GetBytes(json);
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (mySession == session) Notice("Send failed: " + e.Message);
            }
            finally { if (locked) sendLock.Release(); }
        }

        void Notice(string text)
        {
            inbox.Enqueue(JsonUtility.ToJson(new LocalNotice { summary = text }));
        }

        // ------------------------------------------------------------------ main thread

        void Update()
        {
            while (inbox.TryDequeue(out var json)) Dispatch(json);
        }

        void Dispatch(string json)
        {
            string type = JsonUtility.FromJson<Envelope>(json).type;
            switch (type)
            {
                case "STATE":
                    StateReceived?.Invoke(JsonUtility.FromJson<StateMessage>(json));
                    break;
                case "RESULT":
                    ResultReceived?.Invoke(JsonUtility.FromJson<ResultMessage>(json));
                    break;
                case "ERROR":
                    ResultReceived?.Invoke(new ResultMessage
                    {
                        accepted = false,
                        summary = "Server error: " + JsonUtility.FromJson<ErrorMessage>(json).message,
                    });
                    break;
            }
        }

        void Disconnect()
        {
            try
            {
                cts?.Cancel();
                socket?.Abort();
                socket?.Dispose();
            }
            catch (Exception) { }
            socket = null;
            cts = null;
        }

        void OnDestroy() => Disconnect();
    }
}
