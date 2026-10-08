using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DragonMMA
{
    // Lifecycle/input adapter; deterministic game rules live in DragonGameSession.
    [DisallowMultipleComponent]
    public sealed class DragonMmaGame : MonoBehaviour
    {
        private DragonGameSession session;
        [Header("Authored scene references")]
        [SerializeField] private DragonMmaView view;
        [SerializeField] private DragonGameConfig configuration;
        private DesktopOverlayWindow window;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>(16);
        private PointerEventData pointer;
        private float saveCountdown;
        private bool savePending;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int DragonWeb_IsVisible();
        private bool browserHidden;
        private bool browserStarted;
#endif
        public GameSaveData DebugData => session.Data;
        public DragonGameSession DebugSession => session;
        public DragonMmaView DebugView => view;
        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-dragonTestSave") DragonSaveSystem.OverridePath = args[i + 1];
#endif
            if (view == null || configuration == null)
            {
                Debug.LogError("Dragon MMA: assign the authored View and Game Config in the Bootstrap Inspector.", this);
                enabled = false;
                return;
            }
            session = new DragonGameSession(DragonSaveSystem.Load(configuration), null, configuration);
            window = GetComponent<DesktopOverlayWindow>();
            if (window != null) window.SetAlwaysOnTop(session.Data.overlayAlwaysOnTop);
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
#if UNITY_WEBGL && !UNITY_EDITOR
                camera.backgroundColor = new Color32(23, 37, 43, 255);
#else
                camera.backgroundColor = DesktopOverlayWindow.TransparencyKey;
#endif
                camera.allowHDR = false;
                camera.allowMSAA = false;
                var urp = camera.GetComponent<UniversalAdditionalCameraData>();
                if (urp != null) urp.renderPostProcessing = false;
            }
            view.Initialize(this, session, window);
            session.Notify += view.Toast;
            session.Changed += OnChanged;
            session.ProcessTimers(Now);
            view.Refresh(true);
        }

        private void Update()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!browserStarted) return;
            bool hidden = DragonWeb_IsVisible() == 0;
            if (hidden) { if (!browserHidden) Save(); browserHidden = true; return; }
            bool resumed = browserHidden; browserHidden = false;
            // Browser background time only completes facility timers, never hunting.
            float frameSeconds = resumed ? 0 : Mathf.Min(.1f, Time.unscaledDeltaTime);
#else
            float frameSeconds = Time.unscaledDeltaTime;
#endif
            // Read input BEFORE final timer tick: the last-frame cheer affects resolution.
            HandleInput();
            session.Tick(frameSeconds, Now);
            view.Tick(frameSeconds);
            saveCountdown -= frameSeconds;
            if (savePending && saveCountdown <= 0) Save();
        }

        private void OnChanged()
        {
            view.Refresh(true);
            savePending = true;
            saveCountdown = 0;
        }

        private void HandleInput()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                view.OpenPanel("");
            if (session.Data.phase != HuntPhase.Fighting || !Application.isFocused) return;
            int count = 0;
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) count++;
            bool touched = false;
            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (!touch.press.wasPressedThisFrame) continue;
                    touched = true;
                    if (CanCheerAt(touch.position.ReadValue())) count++;
                }
            }
            if (!touched && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                CanCheerAt(Mouse.current.position.ReadValue())) count++;
            if (count == 0) return;
            session.Cheer(count);
            view.CheerPulse();
        }

        public bool CanCheerAt(Vector2 screenPoint)
        {
            if (!view.IsBattlePoint(screenPoint)) return false;
            if (EventSystem.current == null) return true;
            if (pointer == null) pointer = new PointerEventData(EventSystem.current);
            pointer.position = screenPoint;
            uiHits.Clear();
            EventSystem.current.RaycastAll(pointer, uiHits);
            return uiHits.Count == 0;
        }

        public void ReturnCart()
        {
            List<DragonInstance> departing = session.ReturnCart();
            if (departing.Count > 0) view.AnimateReturn(departing);
            ClearSelection();
        }

        public void SetAlwaysOnTop(bool enabled)
        {
            session.Data.overlayAlwaysOnTop = enabled;
            if (window != null) window.SetAlwaysOnTop(enabled);
            OnChanged();
        }

        public void Save()
        {
            if (session == null) return;
            try { DragonSaveSystem.Save(session.Data, session.Config); savePending = false; }
            catch (Exception exception)
            {
                Debug.LogError("[Dragon MMA] Save failed: " + exception.Message);
                view?.Toast("저장 실패 · 디스크 공간과 폴더 권한을 확인하세요.");
                saveCountdown = 10f;
            }
        }

        public void Quit()
        {
            Save();
#if UNITY_WEBGL && !UNITY_EDITOR
            view.OpenPanel(""); view.Toast("저장했습니다. 이제 브라우저 탭을 닫아도 됩니다.");
#else
            Application.Quit();
#endif
        }
        // Called by the Web template before the browser suspends its rendering loop.
        public void OnBrowserHidden(string unused) => Save();

        public void OnBrowserStart(string unused)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            browserStarted = true;
#endif
        }
        public void DebugForceEncounter(DragonKind kind) { session.DebugForceEncounter(kind); view.Refresh(true); }
        public void DebugClosePanel() => view.OpenPanel("");
        public void DebugOpenPanel(string name) => view.OpenPanel(name);
        public string GetDebugSummary() => $"Phase={session.Data.phase}; Enemy={session.Data.currentEnemy}; Time={session.Data.phaseRemaining:0.0}; Cart={session.Data.cart.Count}; Storage={session.Data.storage.Count}; Coins={session.Data.coins}; Cores={session.Data.cores}; Power={session.Data.fighterPower}; Panel={view.ActivePanel}";
        public static void ClearSelection() { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); }
        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationQuit() => Save();
        private void OnDestroy()
        {
            if (session != null) { session.Changed -= OnChanged; if (view != null) session.Notify -= view.Toast; }
            view?.Dispose();
        }
    }
}
