using UnityEngine;
using UnityEngine.EventSystems;

namespace HexQuest
{
    /// <summary>
    /// The ONLY component you add to the scene by hand. On Play it builds the camera setup,
    /// grid, HUD, mock server and controller, so there is no manual scene wiring to get wrong.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] MatchConfig config = new MatchConfig();

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

            // World + UI + "server"
            var grid = new GameObject("HexGrid").AddComponent<HexGridView>();
            var hud = new GameObject("HUD").AddComponent<HudController>();
            hud.Build();
            var server = new GameObject("MockGameServer").AddComponent<MockGameServer>();

            var controller = gameObject.AddComponent<GameController>();
            controller.Init(server, grid, hud, camCtrl, cam, config);
            controller.StartMatch();
        }
    }
}
