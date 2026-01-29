using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

public class TransparentScreenCapture : EditorWindow
{
    private ObjectField _cameraField;
    private IntegerField _widthField;
    private IntegerField _heightField;
    private Button _captureButton;
    private Label _statusLabel;

    [MenuItem("moorestech/Util/Transparent Screen Capture")]
    public static void ShowWindow()
    {
        var window = GetWindow<TransparentScreenCapture>();
        window.titleContent = new GUIContent("Transparent Screen Capture");
        window.minSize = new Vector2(300, 280);
    }

    public void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.paddingTop = 10;
        root.style.paddingBottom = 10;
        root.style.paddingLeft = 10;
        root.style.paddingRight = 10;

        // カメラ選択
        // Camera selection
        _cameraField = new ObjectField("Camera")
        {
            objectType = typeof(Camera),
            allowSceneObjects = true
        };
        root.Add(_cameraField);

        root.Add(new Label { style = { height = 10 } });

        // 解像度設定
        // Resolution settings
        root.Add(new Label("Resolution") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

        _widthField = new IntegerField("Width") { value = 1920 };
        _widthField.style.marginTop = 5;
        root.Add(_widthField);

        _heightField = new IntegerField("Height") { value = 1080 };
        root.Add(_heightField);

        root.Add(new Label { style = { height = 10 } });

        // プリセットボタン
        // Preset buttons
        root.Add(new Label("Presets:"));

        var presetContainer = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                marginTop = 5
            }
        };

        var preset1080 = new Button(() => SetResolution(1920, 1080)) { text = "1920x1080" };
        preset1080.style.flexGrow = 1;
        presetContainer.Add(preset1080);

        var preset1440 = new Button(() => SetResolution(2560, 1440)) { text = "2560x1440" };
        preset1440.style.flexGrow = 1;
        presetContainer.Add(preset1440);

        var preset4K = new Button(() => SetResolution(3840, 2160)) { text = "4K" };
        preset4K.style.flexGrow = 1;
        presetContainer.Add(preset4K);

        root.Add(presetContainer);

        root.Add(new Label { style = { height = 15 } });

        // 撮影ボタン
        // Capture button
        _captureButton = new Button(OnCaptureClicked) { text = "Capture" };
        _captureButton.style.height = 30;
        root.Add(_captureButton);

        root.Add(new Label { style = { height = 10 } });

        // ステータス表示
        // Status display
        _statusLabel = new Label("Last saved: (none)");
        _statusLabel.style.whiteSpace = WhiteSpace.Normal;
        root.Add(_statusLabel);

        root.Add(new Label { style = { height = 10 } });

        // 注意事項
        // Warning message
        var warningBox = new VisualElement
        {
            style =
            {
                backgroundColor = new Color(0.3f, 0.3f, 0.1f, 0.5f),
                borderTopLeftRadius = 5,
                borderTopRightRadius = 5,
                borderBottomLeftRadius = 5,
                borderBottomRightRadius = 5,
                paddingTop = 8,
                paddingBottom = 8,
                paddingLeft = 8,
                paddingRight = 8
            }
        };

        var warningLabel = new Label("Screen Space - Overlay UI is not captured.\nChange Canvas to Screen Space - Camera if needed.");
        warningLabel.style.whiteSpace = WhiteSpace.Normal;
        warningLabel.style.fontSize = 11;
        warningBox.Add(warningLabel);
        root.Add(warningBox);
    }

    private void SetResolution(int width, int height)
    {
        _widthField.value = width;
        _heightField.value = height;
    }

    private void OnCaptureClicked()
    {
        var camera = _cameraField.value as Camera;
        if (camera == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a camera.", "OK");
            return;
        }

        var width = _widthField.value;
        var height = _heightField.value;

        if (width <= 0 || height <= 0)
        {
            EditorUtility.DisplayDialog("Error", "Width and Height must be greater than 0.", "OK");
            return;
        }

        var filePath = EditorUtility.SaveFilePanel(
            "Save Transparent PNG",
            Application.dataPath,
            "capture",
            "png"
        );

        if (string.IsNullOrEmpty(filePath)) return;

        Capture(camera, width, height, filePath);
        _statusLabel.text = $"Last saved: {filePath}";
    }

    private static void Capture(Camera cam, int width, int height, string filePath)
    {
        // ディレクトリ作成
        // Create directory if needed
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // 透明を保持するためARGB32で作成
        // Create with ARGB32 to preserve transparency
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        // カメラ状態を退避
        // Save camera state
        var prevTarget = cam.targetTexture;
        var prevClearFlags = cam.clearFlags;
        var prevBg = cam.backgroundColor;
        var prevActive = RenderTexture.active;
        var prevAllowHDR = cam.allowHDR;

        // URP用の設定を退避
        // Save URP-specific settings
        var urpCameraData = cam.GetComponent<UniversalAdditionalCameraData>();
        var prevRenderPostProcessing = false;
        var prevAntialiasing = AntialiasingMode.None;
        if (urpCameraData != null)
        {
            prevRenderPostProcessing = urpCameraData.renderPostProcessing;
            prevAntialiasing = urpCameraData.antialiasing;
        }

        try
        {
            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.allowHDR = false;

            // ポストプロセスを無効化（アルファを破壊するため）
            // Disable post-processing (it destroys alpha)
            if (urpCameraData != null)
            {
                urpCameraData.renderPostProcessing = false;
                urpCameraData.antialiasing = AntialiasingMode.None;
            }

            // 手動でレンダリング
            // Render manually
            cam.Render();

            RenderTexture.active = rt;

            // RenderTextureからCPUへ読み戻し
            // Read pixels from RenderTexture to CPU
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            // PNGへエンコードして保存
            // Encode to PNG and save
            var png = tex.EncodeToPNG();
            File.WriteAllBytes(filePath, png);

            Debug.Log($"Transparent PNG saved: {filePath}");
        }
        finally
        {
            // 復元と解放
            // Restore and cleanup
            cam.targetTexture = prevTarget;
            cam.clearFlags = prevClearFlags;
            cam.backgroundColor = prevBg;
            cam.allowHDR = prevAllowHDR;
            RenderTexture.active = prevActive;

            if (urpCameraData != null)
            {
                urpCameraData.renderPostProcessing = prevRenderPostProcessing;
                urpCameraData.antialiasing = prevAntialiasing;
            }

            DestroyImmediate(rt);
            DestroyImmediate(tex);
        }
    }
}
