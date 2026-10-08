#if DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace DragonMMA
{
    // Opt-in development-build probe. Never runs in a normal player launch or user save.
    public sealed class DragonMmaPlayerProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-dragonProbe") < 0 || Array.IndexOf(args, "-dragonTestSave") < 0) return;
            new GameObject("Development overlay probe").AddComponent<DragonMmaPlayerProbe>();
        }

        private IEnumerator Start()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<DragonMmaGame>();
            var window = UnityEngine.Object.FindFirstObjectByType<DesktopOverlayWindow>();
            string directory = Path.GetDirectoryName(DragonSaveSystem.SavePath);
            if (game == null || window == null || string.IsNullOrEmpty(directory)) yield break;
            Directory.CreateDirectory(directory);
            yield return new WaitForSecondsRealtime(4);
            Debug.Log("[Dragon MMA Probe] COLLAPSED " + window.GetWindowDiagnostics());
            yield return new WaitForEndOfFrame();
            RecordFrame(Path.Combine(directory, "player-collapsed.png"));
            int collapsed = Screen.height;
            game.DebugOpenPanel("storage");
            yield return new WaitForSecondsRealtime(3);
            Debug.Log("[Dragon MMA Probe] EXPANDED " + window.GetWindowDiagnostics());
            if (Screen.height <= collapsed) Debug.LogError("[Dragon MMA Probe] Expansion failed.");
            yield return new WaitForEndOfFrame();
            RecordFrame(Path.Combine(directory, "player-expanded.png"));
            game.DebugClosePanel();
            yield return new WaitForSecondsRealtime(3);
            Debug.Log("[Dragon MMA Probe] REFOLDED " + window.GetWindowDiagnostics());
            if (Screen.height != collapsed) Debug.LogError("[Dragon MMA Probe] Refold height mismatch.");
            Debug.Log("[Dragon MMA Probe] COMPLETE " + game.GetDebugSummary());
            game.Quit();
        }

        private static void RecordFrame(string path)
        {
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            Color32 pixel = frame.GetPixel(frame.width / 2, frame.height - 3);
            Debug.Log($"[Dragon MMA Probe] backbuffer clear RGB={pixel.r},{pixel.g},{pixel.b}; frame={frame.width}x{frame.height}");
            if (pixel.r != 0 || pixel.g != 0 || pixel.b != 0)
                Debug.LogError("[Dragon MMA Probe] Backbuffer color does not match native transparency key.");
            File.WriteAllBytes(path, frame.EncodeToPNG());
            UnityEngine.Object.Destroy(frame);
        }
    }
}
#endif

