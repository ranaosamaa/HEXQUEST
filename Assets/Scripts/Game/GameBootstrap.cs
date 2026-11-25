using UnityEngine;
using UnityEngine.EventSystems;

namespace HexQuest
{
    /// <summary>
    /// The ONLY component you add to the scene by hand. On Play it builds the camera setup,
    /// grid, HUD, connection and controller, so there is no manual scene wiring to get wrong.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] MatchConfig config = new MatchConfig();

        [Header("Connection")]
        [Tooltip("ON: play against the Python server (real rules + real agents). OFF: built-in mock.")]
        [SerializeField] bool useRealServer = true;
        [SerializeField] string serverUrl = "ws://127.0.0.1:8765/ws";

        void Start()
        {
            // Camera
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera));
                camGo.tag = "MainCamera";
                cam = camGo.GetComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Water;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            var camCtrl = cam.GetComponent<CameraController>();
            if (camCtrl == null) camCtrl = cam.gameObject.AddComponent<CameraController>();

            // uGUI needs exactly one EventSystem
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // Connection: the ONLY place that decides mock vs real server.
            IGameConnection connection;
            if (useRealServer)
            {
                var ws = new GameObject("ServerConnection").AddComponent<WebSocketGameConnection>();
                ws.Url = serverUrl;
                connection = ws;
            }
            else
            {
                connection = new GameObject("MockGameServer").AddComponent<MockGameServer>();
            }

            // World + UI
            var grid = new GameObject("HexGrid").AddComponent<HexGridView>();
            var hud = new GameObject("HUD").AddComponent<HudController>();
            hud.Build();

            var controller = gameObject.AddComponent<GameController>();
            controller.Init(connection, grid, hud, camCtrl, cam, config);
            controller.StartMatch();
        }
    }
}
