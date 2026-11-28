using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace HexQuest
{
    /// <summary>
    /// Owns the whole application: world, UI, menus, lobby, match lifecycle, pause, game over,
    /// settings, transitions and the server connection choice. Built entirely in code.
    /// </summary>
    public class AppFlow : MonoBehaviour
    {
        [Serializable] class Health { public bool ok; public string rules_version; }

        Camera cam;
        CameraController camCtrl;
        GameObject worldRoot, vignette;
        HexGridView grid;
        FxManager fx;
        HudController hud;
        GameController controller;
        CanvasScaler scaler, overlayScaler;
        Image fader;
        Text toast;
        TitleBackground bg;
        MainMenuScreen menu;
        LobbyScreen lobby;
        SettingsScreen settings;
        HowToScreen howTo;
        PauseScreen pause;
        GameOverScreen gameOver;
        DialogScreen dialog;

        readonly List<UiScreen> overlays = new List<UiScreen>();
        MockGameServer mock;
        WebSocketGameConnection ws;
        MatchConfig lastConfig;
        bool playing, busy;
        Coroutine toastRoutine;

        // =============================================================== boot

        public void Boot(Camera camera)
        {
            cam = camera;
            AudioManager.Create();
            UserSettings.ApplyGraphics();
            UserSettings.ApplyDisplay();

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Water;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camCtrl = cam.GetComponent<CameraController>();
            if (camCtrl == null) camCtrl = cam.gameObject.AddComponent<CameraController>();
            camCtrl.InputEnabled = false;

            // ---- world (hidden until a match starts)
            worldRoot = new GameObject("World");
            new GameObject("Water").AddComponent<WaterView>().transform.SetParent(worldRoot.transform, false);
            grid = new GameObject("HexGrid").AddComponent<HexGridView>();
            grid.transform.SetParent(worldRoot.transform, false);
            fx = new GameObject("Effects").AddComponent<FxManager>();
            fx.transform.SetParent(worldRoot.transform, false);
            fx.Init(UIKit.Font);
            worldRoot.SetActive(false);

            // ---- UI
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            BuildUi();
            UserSettings.Changed += ApplyUiScale;
            ApplyUiScale();

            controller = gameObject.AddComponent<GameController>();
            controller.Init(grid, hud, camCtrl, cam, fx);
            controller.MatchFinished += s => StartCoroutine(ShowGameOver(s));
            controller.ConnectionProblem += OnConnectionProblem;
            controller.PauseRequested += OpenPause;
            hud.MenuClicked += OpenPause;

            StartCoroutine(Transition(() => ShowMainMenuNow(), true));
        }

        CanvasScaler NewCanvas(string canvasName, int order, out Transform root)
        {
            var go = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0.5f;
            root = go.transform;
            return sc;
        }

        void BuildUi()
        {
            Transform ui, top;
            scaler = NewCanvas("UI", 10, out ui);
            overlayScaler = NewCanvas("UI Overlay", 50, out top);

            var vig = UIKit.NewRect("Vignette", ui).Stretch();
            UIKit.Img(vig, "ui_vignette", new Color(1f, 1f, 1f, 0.8f), false);
            vignette = vig.gameObject;
            vignette.SetActive(false);

            bg = gameObject.AddComponent<TitleBackground>();
            bg.Build(ui);
            hud = gameObject.AddComponent<HudController>();
            hud.Build(ui);

            menu = Screen_<MainMenuScreen>(ui);
            lobby = Screen_<LobbyScreen>(ui);
            howTo = Screen_<HowToScreen>(ui);
            settings = Screen_<SettingsScreen>(ui);
            pause = Screen_<PauseScreen>(ui);
            gameOver = Screen_<GameOverScreen>(ui);
            dialog = Screen_<DialogScreen>(ui);

            menu.PlayClicked = () => Go(() => { menu.Close(0f); lobby.Open(); bg.SetDim(0.38f); });
            menu.HowToClicked = () => PushOverlay(howTo);
            menu.SettingsClicked = () => PushOverlay(settings);
            menu.QuitClicked = Quit;

            lobby.BackClicked = () => Go(() => { lobby.Close(0f); menu.Open(); bg.SetDim(0f); });
            lobby.StartClicked = cfg => Go(() => { lobby.Close(0f); BeginMatch(cfg); });
            lobby.CheckServer = CheckServer;

            howTo.CloseClicked = () => howTo.Close();
            settings.CloseClicked = () => settings.Close();

            pause.ResumeClicked = ClosePause;
            pause.SettingsClicked = () => PushOverlay(settings);
            pause.HowToClicked = () => PushOverlay(howTo);
            pause.RestartClicked = () => { ClosePause(); Go(() => BeginMatch(lastConfig)); };
            pause.LobbyClicked = () => Go(() => { ShowLobbyNow(); });
            pause.MenuClicked = () => Go(() => { ShowMainMenuNow(); });

            gameOver.RematchClicked = () => { lastConfig.map_seed = UnityEngine.Random.Range(1, 99999); Go(() => BeginMatch(lastConfig)); };
            gameOver.LobbyClicked = () => Go(() => { ShowLobbyNow(); });
            gameOver.MenuClicked = () => Go(() => { ShowMainMenuNow(); });

            // screens remove themselves from the overlay stack when they close
            foreach (var s in new UiScreen[] { howTo, settings, pause, gameOver, dialog })
            {
                var captured = s;
                s.Closed += () => overlays.Remove(captured);
            }

            var fr = UIKit.NewRect("Fader", top).Stretch();
            fader = fr.gameObject.AddComponent<Image>();
            fader.color = new Color(0.02f, 0.03f, 0.08f, 1f);
            toast = UIKit.AddText(top, "", 34, Palette.Cream, TextAnchor.MiddleCenter, true).Shadow(2.5f);
            toast.rectTransform.Place(UIKit.TC, UIKit.TC, new Vector2(0f, -260f), new Vector2(1200f, 60f));
            toast.color = new Color(1f, 1f, 1f, 0f);
        }

        T Screen_<T>(Transform parent) where T : UiScreen
        {
            var s = gameObject.AddComponent<T>();
            s.Build(parent);
            return s;
        }

        void ApplyUiScale()
        {
            if (scaler == null) return;
            var r = new Vector2(1920f, 1080f) / UserSettings.UiScale;
            scaler.referenceResolution = r;
            overlayScaler.referenceResolution = r;
        }

        // =============================================================== navigation

        IEnumerator Transition(Action middle, bool skipFadeOut = false)
        {
            busy = true;
            if (!skipFadeOut) yield return FadeTo(1f, 0.22f);
            middle();
            yield return FadeTo(0f, 0.38f);
            busy = false;
        }

        IEnumerator FadeTo(float alpha, float duration)
        {
            fader.raycastTarget = true;
            float from = fader.color.a, t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                var c = fader.color; c.a = Mathf.Lerp(from, alpha, Mathf.Clamp01(t / duration)); fader.color = c;
                yield return null;
            }
            var f = fader.color; f.a = alpha; fader.color = f;
            fader.raycastTarget = alpha > 0.01f;
        }

        void Go(Action middle)
        {
            if (busy) return;
            StartCoroutine(Transition(middle));
        }

        void CloseAllOverlays()
        {
            foreach (var s in new List<UiScreen>(overlays)) s.Close(0f);
            overlays.Clear();
        }

        void ShowMainMenuNow()
        {
            LeaveMatchState();
            lobby.Close(0f);
            menu.Open();
            bg.SetDim(0f);
        }

        void ShowLobbyNow()
        {
            LeaveMatchState();
            menu.Close(0f);
            lobby.Open();
            bg.SetDim(0.38f);
        }

        void LeaveMatchState()
        {
            CloseAllOverlays();
            controller.EndMatch();
            playing = false;
            worldRoot.SetActive(false);
            vignette.SetActive(false);
            hud.Show(false);
            camCtrl.InputEnabled = false;
            bg.Show(true);
            AudioManager.PlayMusic("music_menu");
        }

        void BeginMatch(MatchConfig cfg)
        {
            CloseAllOverlays();
            lastConfig = cfg;
            UserSettings.SaveMatchConfig(cfg);
            menu.Close(0f);
            bg.Show(false);
            worldRoot.SetActive(true);
            vignette.SetActive(true);
            hud.Show(true);
            hud.SetInteractable(true);
            camCtrl.InputEnabled = true;
            controller.InputEnabled = true;
            playing = true;
            AudioManager.PlayMusic("music_game");
            controller.BeginMatch(PickConnection(), cfg);
        }

        IGameConnection PickConnection()
        {
            if (UserSettings.UseMock)
            {
                if (mock == null) mock = new GameObject("MockGameServer").AddComponent<MockGameServer>();
                return mock;
            }
            if (ws == null) ws = new GameObject("ServerConnection").AddComponent<WebSocketGameConnection>();
            ws.Url = UserSettings.ServerUrl;
            return ws;
        }

        // =============================================================== overlays

        void PushOverlay(UiScreen s)
        {
            if (!overlays.Contains(s)) overlays.Add(s);
            s.Open();
        }

        void OpenPause()
        {
            if (!playing || pause.IsOpen || gameOver.IsOpen || busy) return;
            camCtrl.InputEnabled = false;
            controller.InputEnabled = false;
            PushOverlay(pause);
        }

        void ClosePause()
        {
            pause.Close();
            camCtrl.InputEnabled = true;
            controller.InputEnabled = true;
        }

        IEnumerator ShowGameOver(StateMessage s)
        {
            float t = 0f;
            while (t < 1.6f) { t += Time.unscaledDeltaTime; yield return null; }
            if (!playing || s == null) yield break;
            camCtrl.InputEnabled = false;
            controller.InputEnabled = false;
            var ranking = new List<PlayerData>(s.players);
            ranking.Sort((a, b) => b.score.CompareTo(a.score));
            bool won = ranking[0].player_id == 0 && (ranking.Count < 2 || ranking[1].score < ranking[0].score);
            AudioManager.PlaySfx(won ? "victory" : "defeat", 1f, 0f);
            gameOver.Present(s, 0);
            PushOverlay(gameOver);
        }

        void OnConnectionProblem(string message, bool hadState)
        {
            if (dialog.IsOpen || !playing) return;
            overlays.Add(dialog);
            dialog.Present(hadState ? "Connection lost" : "The server is silent",
                message + "\n\nStart the Python server, or play offline against built-in opponents.",
                "Play Offline", () => { UserSettings.UseMock = true; Go(() => BeginMatch(lastConfig)); },
                "Back to Lobby", () => Go(() => ShowLobbyNow()));
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape) || busy) return;
            if (overlays.Count > 0) overlays[overlays.Count - 1].OnEscape();
            else if (playing) controller.HandleEscape();
            else if (lobby.IsOpen) lobby.OnEscape();
        }

        // =============================================================== helpers

        public void CheckServer(Action<bool, string> done) { StartCoroutine(CheckServerRoutine(done)); }

        IEnumerator CheckServerRoutine(Action<bool, string> done)
        {
            using (var req = UnityWebRequest.Get(ToHealthUrl(UserSettings.ServerUrl)))
            {
                req.timeout = 4;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var h = JsonUtility.FromJson<Health>(req.downloadHandler.text);
                    done(true, "Server online  -  rules v" + (h != null ? h.rules_version : "?"));
                }
                else
                    done(false, "Server not reachable. Start it, or enable offline mode in Settings.");
            }
        }

        static string ToHealthUrl(string wsUrl)
        {
            string u = wsUrl.Trim();
            if (u.StartsWith("wss://")) u = "https://" + u.Substring(6);
            else if (u.StartsWith("ws://")) u = "http://" + u.Substring(5);
            if (u.EndsWith("/ws")) u = u.Substring(0, u.Length - 3);
            return u.TrimEnd('/') + "/health";
        }

        void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
