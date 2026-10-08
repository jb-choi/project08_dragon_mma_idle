using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace DragonMMA
{
    /// <summary>Windows window integration. UI owns hit regions; no global input hooks.</summary>
    [DisallowMultipleComponent]
    public sealed class DesktopOverlayWindow : MonoBehaviour
    {
        [SerializeField, Min(120)] private int referenceHeight = 520;
        [SerializeField, Min(240)] private int expandedReferenceHeight = 720;
        [SerializeField] private bool keepOnTop = true;

        // Reserve this RGB value in the art. Clear the camera to it with post-processing off.
        // A color key does not depend on URP preserving backbuffer alpha.
        public static Color TransparencyKey => new Color32(0, 0, 0, 255);
        public bool IsExpanded { get; private set; }
        public bool AlwaysOnTop => keepOnTop;

        private readonly List<Rect> interactiveRegions = new List<Rect>(16);
#if UNITY_EDITOR
        private int previousEditorFrameRate;
        private bool editorFrameRateApplied;
#endif
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private bool regionsRegistered;
#endif

        public void SetExpanded(bool expanded)
        {
            if (IsExpanded == expanded) return;
            IsExpanded = expanded;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            DockToWorkArea(true);
#endif
        }

        public void SetAlwaysOnTop(bool enabled)
        {
            if (keepOnTop == enabled) return;
            keepOnTop = enabled;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            DockToWorkArea(true);
#endif
        }

        /// <summary>
        /// Unity screen-pixel rectangles, origin bottom-left. Call after layout or resolution
        /// changes. The list is copied. Include the settings tab, combat area and visible UI.
        /// </summary>
        public void SetInteractiveRegions(IReadOnlyList<Rect> screenRects)
        {
            interactiveRegions.Clear();
            if (screenRects != null)
            {
                for (int i = 0; i < screenRects.Count; i++)
                {
                    Rect rect = screenRects[i];
                    if (rect.width > 0 && rect.height > 0) interactiveRegions.Add(rect);
                }
            }
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            regionsRegistered = true;
#endif
        }

        public static int CalculateDockHeight(int monitorHeight, int workAreaHeight,
            bool expanded, int collapsedHeight = 520, int expandedHeight = 720)
        {
            float scale = Mathf.Max(1, monitorHeight) / 1080f;
            int height = Mathf.RoundToInt((expanded ? expandedHeight : collapsedHeight) * scale);
            return Mathf.Clamp(height, 1, Mathf.Max(1, workAreaHeight));
        }

        private void Awake()
        {
            Application.runInBackground = true;
#if UNITY_EDITOR
            // The overlay's low-power cap must not make Editor Game View choppy.
            previousEditorFrameRate = Application.targetFrameRate;
            editorFrameRateApplied = true;
            Application.targetFrameRate = 60;
#else
            Application.targetFrameRate = 30;
#endif
        }

#if UNITY_EDITOR
        private void OnDestroy()
        {
            // Preserve a frame-rate override made by another owner during Play Mode.
            if (editorFrameRateApplied && Application.targetFrameRate == 60)
                Application.targetFrameRate = previousEditorFrameRate;
        }
#endif

        private void OnEnable()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            nextDockTime = Time.unscaledTime + .35f;
#endif
        }

        private void Update()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (windowHandle == IntPtr.Zero)
            {
                if (Time.unscaledTime < nextDockTime) return;
                InitializeWindow();
                nextDockTime = Time.unscaledTime + 1f;
                return;
            }
            UpdateClickThrough();
            if (Time.unscaledTime >= nextDockTime)
            {
                DockToWorkArea(false);
                nextDockTime = Time.unscaledTime + 1f;
            }
#endif
        }

        private void OnDisable()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            RestoreWindow();
#endif
        }

        public string GetWindowDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (windowHandle == IntPtr.Zero) return "Windows overlay is waiting for its player window.";
            GetWindowRect(windowHandle, out NativeRect rect);
            long exStyle = ReadWindowLong(windowHandle, GwlExStyle);
            bool colorKey = GetLayeredWindowAttributes(windowHandle, out uint key, out byte alpha, out uint flags)
                && (flags & LwaColorKey) != 0;
            return $"Overlay {rect.right - rect.left}x{rect.bottom - rect.top} at ({rect.left},{rect.top}); " +
                $"expanded={IsExpanded}; topmost={(exStyle & WsExTopmost) != 0}; " +
                $"layered={(exStyle & WsExLayered) != 0}; colorKey={colorKey}:{key:X6}; " +
                $"clickThrough={clickThrough}; touch={touchHardwareAvailable}; regions={interactiveRegions.Count}; dpi={lastDpi}; " +
                $"Unity={Screen.width}x{Screen.height}; graphics={SystemInfo.graphicsDeviceType}";
#else
            return $"Overlay preview only (native Windows behavior disabled); expanded={IsExpanded}; " +
                $"topmost={keepOnTop}; regions={interactiveRegions.Count}; Unity={Screen.width}x{Screen.height}";
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;
        private const long WsCaption = 0x00C00000L;
        private const long WsThickFrame = 0x00040000L;
        private const long WsPopup = 0x80000000L;
        private const long WsExLayered = 0x00080000L;
        private const long WsExAppWindow = 0x00040000L;
        private const long WsExToolWindow = 0x00000080L;
        private const long WsExTransparent = 0x00000020L;
        private const long WsExTopmost = 0x00000008L;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;
        private const uint SwpShowWindow = 0x0040;
        private const uint MonitorDefaultToPrimary = 1;
        private const uint LwaColorKey = 1;
        private const uint ColorKeyRgb = 0x00000000;
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private static readonly IntPtr HwndNotTopmost = new IntPtr(-2);

        private IntPtr windowHandle;
        private long originalStyle;
        private long originalExStyle;
        private NativeRect originalRect;
        private bool clickThrough;
        private bool touchHardwareAvailable;
        private float nextDockTime;
        private int targetWidth;
        private int targetHeight;
        private uint lastDpi;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int x, y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public uint size;
            public NativeRect monitor;
            public NativeRect work;
            public uint flags;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong32(IntPtr hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong32(IntPtr hWnd, int index, int value);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out NativeRect rect);
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint point);
        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref NativePoint point);
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint key, byte alpha, uint flags);
        [DllImport("user32.dll")]
        private static extern bool GetLayeredWindowAttributes(IntPtr hWnd, out uint key, out byte alpha, out uint flags);
        private delegate bool EnumWindowsCallback(IntPtr hWnd, IntPtr parameter);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

        private static long ReadWindowLong(IntPtr window, int index)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(window, index).ToInt64() : GetWindowLong32(window, index);
        }

        private static void WriteWindowLong(IntPtr window, int index, long value)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(window, index, new IntPtr(value));
            else SetWindowLong32(window, index, unchecked((int)value));
        }

        private void InitializeWindow()
        {
            using (Process process = Process.GetCurrentProcess())
            {
                windowHandle = process.MainWindowHandle;
                // A launcher may start the player hidden. MainWindowHandle excludes hidden
                // windows, so locate our own Unity player window by PID and class instead.
                if (windowHandle == IntPtr.Zero)
                {
                    uint ownPid = (uint)process.Id;
                    EnumWindows((candidate, parameter) =>
                    {
                        GetWindowThreadProcessId(candidate, out uint pid);
                        if (pid != ownPid) return true;
                        var className = new StringBuilder(128);
                        GetClassName(candidate, className, className.Capacity);
                        if (className.ToString() != "UnityWndClass") return true;
                        windowHandle = candidate;
                        return false;
                    }, IntPtr.Zero);
                }
            }
            if (windowHandle == IntPtr.Zero) return;
            originalStyle = ReadWindowLong(windowHandle, GwlStyle);
            originalExStyle = ReadWindowLong(windowHandle, GwlExStyle);
            GetWindowRect(windowHandle, out originalRect);
            WriteWindowLong(windowHandle, GwlStyle, (originalStyle & ~(WsCaption | WsThickFrame)) | WsPopup);
            WriteWindowLong(windowHandle, GwlExStyle,
                (originalExStyle & ~(WsExAppWindow | WsExTransparent)) | WsExLayered | WsExToolWindow);

            // Color-key pixels are transparent and natively pass input through Windows.
            if (!SetLayeredWindowAttributes(windowHandle, ColorKeyRgb, 255, LwaColorKey))
                UnityEngine.Debug.LogWarning("Overlay transparency setup failed: Win32 " + Marshal.GetLastWin32Error());
            DockToWorkArea(true);
            UnityEngine.Debug.Log(GetWindowDiagnostics());
        }

        private void DockToWorkArea(bool force)
        {
            if (windowHandle == IntPtr.Zero) return;
            // Desktop (0,0) belongs to the primary monitor. A later monitor picker can
            // replace this selection without changing the UI's screen-pixel contract.
            IntPtr monitor = MonitorFromPoint(new NativePoint(), MonitorDefaultToPrimary);
            var info = new MonitorInfo { size = (uint)Marshal.SizeOf(typeof(MonitorInfo)) };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return;
            int width = info.work.right - info.work.left;
            int height = CalculateDockHeight(info.monitor.bottom - info.monitor.top,
                info.work.bottom - info.work.top, IsExpanded, referenceHeight, expandedReferenceHeight);
            if (width <= 0 || height <= 0) return;

            uint dpi = GetDpiForWindow(windowHandle);
            // SM_DIGITIZER: integrated/external touch hardware. Checking only the mouse
            // position on these devices would swallow a first tap away from the cursor.
            touchHardwareAvailable = (GetSystemMetrics(94) & 3) != 0;
            bool sizeChanged = width != targetWidth || height != targetHeight;
            bool dpiChanged = dpi != lastDpi;
            lastDpi = dpi;
            if (sizeChanged)
            {
                targetWidth = width;
                targetHeight = height;
                Screen.SetResolution(width, height, FullScreenMode.Windowed);
            }

            GetWindowRect(windowHandle, out NativeRect current);
            if (!force && !sizeChanged && !dpiChanged && current.left == info.work.left &&
                current.bottom == info.work.bottom && current.right - current.left == width &&
                current.bottom - current.top == height) return;

            // Unity may reapply window styles during its deferred resolution change.
            long style = ReadWindowLong(windowHandle, GwlStyle);
            WriteWindowLong(windowHandle, GwlStyle, (style & ~(WsCaption | WsThickFrame)) | WsPopup);
            SetWindowPos(windowHandle, keepOnTop ? HwndTopmost : HwndNotTopmost,
                info.work.left, info.work.bottom - height, width, height,
                SwpNoActivate | SwpFrameChanged | SwpShowWindow);
        }

        private void UpdateClickThrough()
        {
            if (!GetCursorPos(out NativePoint cursor) || !ScreenToClient(windowHandle, ref cursor) ||
                !GetClientRect(windowHandle, out NativeRect client)) return;
            int clientWidth = client.right - client.left;
            int clientHeight = client.bottom - client.top;
            if (clientWidth <= 0 || clientHeight <= 0) return;

            // During DPI/resolution transitions native and Unity dimensions may differ.
            Vector2 point = new Vector2(cursor.x * Screen.width / (float)clientWidth,
                (clientHeight - cursor.y) * Screen.height / (float)clientHeight);
            bool inside = !regionsRegistered;
            for (int i = 0; !inside && i < interactiveRegions.Count; i++)
                inside = interactiveRegions[i].Contains(point);

            // Touch devices use Windows' color-key pixel hit test directly. Invisible
            // pixels still pass through, while the first tap on visible UI is delivered.
            bool passThrough = !inside && !touchHardwareAvailable;
            // Keep drag/release delivery if the pointer leaves its initial UI region.
            if (passThrough && !clickThrough && (GetAsyncKeyState(1) & 0x8000) != 0) return;
            if (passThrough == clickThrough) return;
            long exStyle = ReadWindowLong(windowHandle, GwlExStyle);
            exStyle = passThrough ? exStyle | WsExTransparent : exStyle & ~WsExTransparent;
            WriteWindowLong(windowHandle, GwlExStyle, exStyle);
            clickThrough = passThrough;
        }

        private void RestoreWindow()
        {
            if (windowHandle == IntPtr.Zero) return;
            WriteWindowLong(windowHandle, GwlStyle, originalStyle);
            WriteWindowLong(windowHandle, GwlExStyle, originalExStyle);
            SetWindowPos(windowHandle, (originalExStyle & WsExTopmost) != 0 ? HwndTopmost : HwndNotTopmost,
                originalRect.left, originalRect.top, originalRect.right - originalRect.left,
                originalRect.bottom - originalRect.top, SwpNoActivate | SwpFrameChanged);
            windowHandle = IntPtr.Zero;
            targetWidth = targetHeight = 0;
            clickThrough = false;
        }
#endif
    }
}
