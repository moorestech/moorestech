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
    /// 本番の PanelSettings の複製（Linear 合成・sRGB の RenderTexture）に要素を描いて画素を読む。色は Web とこれで比べる（設計書 A-74）
    /// Draws elements on a copy of the production PanelSettings (Linear blend, sRGB RenderTexture) and reads pixels back; colors are compared to the Web with this (design A-74)
    /// </summary>
    /// <remarks>
    /// EditMode 専用。PlayMode の画面は ScreenCapture（DSL の Screenshot）で撮る。倍率は複製の ScaleWithScreenSize が width×height から決める（実機と同じ）。
    /// EditMode only; PlayMode screens are taken with ScreenCapture (the DSL Screenshot). The scale comes from the copy's ScaleWithScreenSize for width x height (same as the game).
    /// </remarks>
    public static class UiRuntimePanelCapture
    {
        private const string PanelSettingsPath = "Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset";
        private static readonly string[] PanelPassNames = { "UpdatePanels", "RepaintPanels", "RenderOffscreenPanels" };
        private const int PanelPassCount = 3;

        // 読める Texture2D を返す。呼び手が Object.DestroyImmediate で捨てる
        // Returns a readable Texture2D; the caller disposes it with Object.DestroyImmediate
        public static Texture2D Render(VisualElement content, int width, int height, Color ground)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("[UiRuntimePanelCapture] EditMode only; use ScreenCapture in PlayMode");
            var source = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (source == null) throw new InvalidOperationException($"[UiRuntimePanelCapture] missing {PanelSettingsPath}");

            // 束縛付きの部品が辞書を引けるよう初期化する（ギャラリー窓と同じ）
            // Initialize the dictionary so bound components can resolve text (same as the gallery window)
            Localize.Initialize();

            // 本番の複製に一時の UIDocument を載せる（本番のアセットは書き換えない）
            // Mount a temporary UIDocument on a copy of the production settings (the asset is never modified)
            var settings = Object.Instantiate(source);
            settings.hideFlags = HideFlags.DontSave;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { hideFlags = HideFlags.DontSave };
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
                // 実行時パネルの描画口は internal なので反射で呼ぶ。見つからなければ Unity の更新で口が変わったので止める
                // The runtime panel render entry points are internal, so call them via reflection; stop if a Unity update renamed them
                var utility = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
                if (utility == null) throw new InvalidOperationException("[UiRuntimePanelCapture] UIElementsRuntimeUtility not found");
                var methods = PanelPassNames.Select(FindPass).ToArray();
                for (var pass = 0; pass < PanelPassCount; pass++)
                {
                    foreach (var method in methods)
                    {
                        method.Invoke(null, method.GetParameters().Select(p => p.ParameterType == typeof(bool) ? (object)false : null).ToArray());
                    }
                }

                MethodInfo FindPass(string passName)
                {
                    var method = utility.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(m => m.Name == passName);
                    if (method == null) throw new InvalidOperationException($"[UiRuntimePanelCapture] UIElementsRuntimeUtility.{passName} not found");
                    return method;
                }
            }

            Texture2D ReadBack()
            {
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                return texture;
            }

            #endregion
        }

        // PNG に書いてそのパスを返す。読み戻しの上下は画面と同じ向き（C3-a 実測）
        // Writes a PNG and returns its path; the read-back is upright like the screen (C3-a measurement)
        public static string CaptureToPng(VisualElement content, int width, int height, Color ground, string pngPath)
        {
            var texture = Render(content, width, height, ground);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pngPath)));
            File.WriteAllBytes(pngPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return pngPath;
        }
    }
}
