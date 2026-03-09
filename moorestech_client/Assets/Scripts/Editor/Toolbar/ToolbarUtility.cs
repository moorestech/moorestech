using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

namespace Client.Editor.Toolbar
{
    /// <summary>
    /// ツールバー用共通ヘルパー
    /// Common helper for toolbar elements
    /// </summary>
    public static class ToolbarUtility
    {
        // ビルトインアイコンを取得する
        // Get built-in icon texture
        public static Texture2D GetBuiltInIcon(string iconName)
        {
            var content = EditorGUIUtility.IconContent(iconName);
            if (content != null && content.image != null)
            {
                return content.image as Texture2D;
            }
            return null;
        }
    }

    /// <summary>
    /// ツールバーオーバーレイの位置を再生ボタン基準で配置する
    /// Position toolbar overlays relative to the play button
    /// </summary>
    [InitializeOnLoad]
    internal static class ToolbarOverlayPositioner
    {
        private const string OverlayInitVersionKey = "moorestech_ToolbarOverlayInitVersion";
        private const int CurrentOverlayInitVersion = 1;

        private static bool _positioned;

        static ToolbarOverlayPositioner()
        {
            _positioned = false;
            EditorApplication.update -= TryPosition;
            EditorApplication.update += TryPosition;
        }

        private static void TryPosition()
        {
            if (_positioned) return;

            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var mainToolbar = windows.FirstOrDefault(w => w.GetType().FullName == "UnityEditor.MainToolbarWindow");
            if (mainToolbar == null) return;

            // OverlayCanvasからオーバーレイ一覧を取得
            // Get overlay list from OverlayCanvas
            var canvasProp = typeof(EditorWindow).GetProperty("overlayCanvas", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (canvasProp == null) return;
            var canvas = canvasProp.GetValue(mainToolbar);
            if (canvas == null) return;

            var overlaysProp = canvas.GetType().GetProperty("overlays", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var overlayList = (overlaysProp?.GetValue(canvas) as System.Collections.IEnumerable)?.Cast<Overlay>().ToList();
            if (overlayList == null) return;

            // 初回起動時にmoorestechオーバーレイを自動表示する
            // Auto-show moorestech overlays on first launch
            AutoShowOverlaysIfNeeded(overlayList);

            Overlay playMode = null;
            Overlay timeScale = null;
            Overlay sceneReload = null;
            Overlay home = null;

            foreach (var o in overlayList)
            {
                var id = o.GetType().GetProperty("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(o)?.ToString() ?? "";
                if (id.Contains("Play Mode")) playMode = o;
                if (id.Contains("TimeScale")) timeScale = o;
                if (id.Contains("Scene Reload")) sceneReload = o;
                if (id.Contains("moorestech/Home")) home = o;
            }

            if (playMode == null || timeScale == null || sceneReload == null || home == null) return;

            var dockBefore = typeof(Overlay).GetMethod("DockBefore", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var dockAfter = typeof(Overlay).GetMethod("DockAfter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // 配置順: SceneReload → Home → PlayMode → TimeScale
            // Order: SceneReload → Home → PlayMode → TimeScale
            dockBefore?.Invoke(sceneReload, new object[] { playMode });
            dockBefore?.Invoke(home, new object[] { playMode });
            dockAfter?.Invoke(timeScale, new object[] { playMode });

            _positioned = true;
            EditorApplication.update -= TryPosition;
        }

        private static void AutoShowOverlaysIfNeeded(System.Collections.Generic.List<Overlay> overlayList)
        {
            // バージョンが一致する場合は初期化済みなのでスキップ
            // Skip if already initialized at current version
            if (EditorPrefs.GetInt(OverlayInitVersionKey, 0) >= CurrentOverlayInitVersion) return;

            var displayedProp = typeof(Overlay).GetProperty("displayed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (displayedProp == null) return;

            // moorestech/プレフィックスを持つ全オーバーレイを表示する
            // Show all overlays with moorestech/ prefix
            foreach (var overlay in overlayList)
            {
                var id = overlay.GetType().GetProperty("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(overlay)?.ToString() ?? "";
                if (!id.StartsWith("moorestech/")) continue;

                var currentDisplayed = (bool)(displayedProp.GetValue(overlay) ?? false);
                if (!currentDisplayed)
                {
                    displayedProp.SetValue(overlay, true);
                }
            }

            // 初期化バージョンを保存（次回以降はスキップ）
            // Save init version so subsequent launches skip auto-show
            EditorPrefs.SetInt(OverlayInitVersionKey, CurrentOverlayInitVersion);
        }
    }
}
