using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Client.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Client.Ui.Editor.Capture
{
    /// <summary>
    /// 本番 PanelSettings 複製に描いて画素を読む（A-74）
    /// Draws on a copy of the production PanelSettings and reads pixels back (A-74)
    /// </summary>
    /// <remarks>
    /// EditMode 専用
        /// PlayMode は ScreenCapture で撮る
        /// 倍率は複製の ScaleWithScreenSize が決める
    /// EditMode only
        /// PlayMode is captured with ScreenCapture
        /// The scale comes from the copy's ScaleWithScreenSize
    /// </remarks>
    public static class UiRuntimePanelCapture
    {
        private const string PanelSettingsPath = "Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset";
        private static readonly string[] PanelPassNames = { "UpdatePanels", "RepaintPanels", "RenderOffscreenPanels" };
        private const int PanelPassCount = 3;

        // 読める Texture2D を返す。破棄は呼び手
        // Returns a readable Texture2D; the caller disposes it
        public static Texture2D Render(VisualElement content, int width, int height, Color ground, UiCaptureLocalization localization)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("[UiRuntimePanelCapture] EditMode only; use ScreenCapture in PlayMode");
            var source = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (source == null) throw new InvalidOperationException($"[UiRuntimePanelCapture] missing {PanelSettingsPath}");

            // 動的フォントの atlas はここで空にしない。撮影の途中で空にすると先の字の描画が破棄済みの atlas を引いて落ちる（GameUiFontAtlasReset がドメインの読み込みで空にする）
            // Do not empty the dynamic font atlases here; emptying mid-session makes earlier text layouts hit destroyed atlases (GameUiFontAtlasReset empties them on domain load)

            // 実行時パネルの描画口は internal なので反射で引く。一時リソースを作る前に解決し、見つからなければ Unity の更新で口が変わったので止める
            // The runtime panel render entry points are internal, so resolve them via reflection before creating temp resources; stop if a Unity update renamed them
            var passMethods = ResolvePassMethods();

            // 束縛付きの部品が辞書を引けるよう初期化する（ギャラリー窓と同じ）。テストが足した辞書を保つときは呼び手が止める
            // Initialize the dictionary so bound components resolve text (same as the gallery window); callers keeping test-added entries opt out
            if (localization == UiCaptureLocalization.InitializeDictionary) Localize.Initialize();

            // 本番の複製に一時の UIDocument を載せる（本番のアセットは書き換えない）
            // Mount a temporary UIDocument on a copy of the production settings (the asset is never modified)
            var settings = Object.Instantiate(source);
            settings.hideFlags = HideFlags.DontSave;
            // 本番が sRGB の値のまま描く設定なら RT も sRGB 変換なしにし、読み戻した値が画面と同じ sRGB の値になるようにする（A-180）
            // When production renders as sRGB values, use an RT without sRGB conversion so the read-back values are the screen's sRGB values (A-180)
            var srgbValueRendering = source.forceGammaRendering;
            var readWrite = srgbValueRendering ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, readWrite) { hideFlags = HideFlags.DontSave };
            settings.targetTexture = target;
            var host = new GameObject("UiRuntimePanelCapture") { hideFlags = HideFlags.DontSave };
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.rootVisualElement.Add(CreateGround());

            // パネルの更新・描画を回して読み戻す
            // Run the panel update and repaint, then read back
            RunPanelPasses();
            var pixels = ReadBack();

            Object.DestroyImmediate(host);
            Object.DestroyImmediate(settings);
            target.Release();
            Object.DestroyImmediate(target);
            return pixels;

            #region Internal

            MethodInfo[] ResolvePassMethods()
            {
                var utility = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
                if (utility == null) throw new InvalidOperationException("[UiRuntimePanelCapture] UIElementsRuntimeUtility not found");
                return PanelPassNames.Select(FindPass).ToArray();

                MethodInfo FindPass(string passName)
                {
                    var method = utility.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(m => m.Name == passName);
                    if (method == null) throw new InvalidOperationException($"[UiRuntimePanelCapture] UIElementsRuntimeUtility.{passName} not found");
                    return method;
                }
            }

            VisualElement CreateGround()
            {
                var groundElement = new VisualElement();
                groundElement.style.flexGrow = 1;
                groundElement.style.backgroundColor = ground;
                groundElement.Add(content);
                return groundElement;
            }

            void RunPanelPasses()
            {
                for (var pass = 0; pass < PanelPassCount; pass++)
                {
                    foreach (var method in passMethods)
                    {
                        method.Invoke(null, method.GetParameters().Select(p => p.ParameterType == typeof(bool) ? (object)false : null).ToArray());
                    }
                }
            }

            Texture2D ReadBack()
            {
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, srgbValueRendering);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                return texture;
            }

            #endregion
        }

        // PNG に書いてパスを返す。上下は画面と同じ（C3-a）
        // Writes a PNG and returns its path; upright like the screen (C3-a)
        public static string CaptureToPng(VisualElement content, int width, int height, Color ground, UiCaptureLocalization localization, string pngPath)
        {
            var texture = Render(content, width, height, ground, localization);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pngPath)));
            File.WriteAllBytes(pngPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return pngPath;
        }
    }
}
