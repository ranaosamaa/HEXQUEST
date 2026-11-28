using UnityEngine;

namespace HexQuest
{
    /// <summary>
    /// The ONLY component you add to the scene by hand. On Play it creates the whole game
    /// (world, menus, lobby, HUD, audio) in code. Server address and offline mode are in the in-game Settings.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        void Start()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera));
                camGo.tag = "MainCamera";
                cam = camGo.GetComponent<Camera>();
            }
            gameObject.AddComponent<AppFlow>().Boot(cam);
        }
    }
}
