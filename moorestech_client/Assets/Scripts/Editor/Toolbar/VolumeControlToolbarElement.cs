using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Editor.Toolbar
{
    /// <summary>
    /// 音量コントロールをツールバーに追加する（参考: UnityToolbarExtension）
    /// Add volume control to the toolbar (ref: UnityToolbarExtension)
    /// </summary>
    public static class VolumeControlToolbarElement
    {
        private const string ElementPath = "moorestech/Volume Control";
        private const string SliderName = "moorestech-volume-slider";

        private static Slider _slider;
        private static Label _currentValueLabel;
        private static float _lastAudioVolume = 1f;

        private static string VolumeValueText => $"{AudioListener.volume * 100:0}";

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // 外部からの音量変更を検知してUI更新
            // Detect external volume changes and update UI
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (_slider == null || _currentValueLabel == null) return;

            // AudioListener.volumeが外部から変更された場合にUIを更新
            // Update UI if AudioListener.volume was changed externally
            if (Mathf.Abs(AudioListener.volume - _lastAudioVolume) > 0.001f)
            {
                _lastAudioVolume = AudioListener.volume;
                _slider.SetValueWithoutNotify(_lastAudioVolume);
                _currentValueLabel.text = VolumeValueText;
            }
        }

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Left)]
        public static MainToolbarElement CreateElement()
        {
            // ボタンとして登録し、VisualElementはcreateGUIで構築
            // Register as button, build VisualElement via createGUI
            var icon = ToolbarUtility.GetBuiltInIcon("d_Profiler.Audio");
            var content = new MainToolbarContent(icon, "音量コントロール / Volume Control");
            var button = new MainToolbarButton(content, () =>
            {
                // 音量を最大にリセット
                // Reset volume to maximum
                AudioListener.volume = 1f;
                _lastAudioVolume = 1f;
                if (_slider != null) _slider.value = 1f;
                if (_currentValueLabel != null) _currentValueLabel.text = VolumeValueText;
            });
            return button;
        }

        // MainToolbarElementが作成するVisualElementとは別に、Overlayの中に直接UIを注入する
        // Inject UI directly into the overlay, separate from the MainToolbarElement's VisualElement
        [InitializeOnLoad]
        private class SliderInjector
        {
            static SliderInjector()
            {
                EditorApplication.update -= TryInject;
                EditorApplication.update += TryInject;
            }

            private static void TryInject()
            {
                // 既にスライダーが存在し、ツリーに接続されている場合はスキップ
                // Skip if slider already exists and is connected to the tree
                if (_slider != null && _slider.panel != null) return;

                // MainToolbarWindowを探す
                // Find the MainToolbarWindow
                var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                foreach (var w in windows)
                {
                    if (w.GetType().FullName != "UnityEditor.MainToolbarWindow") continue;

                    // Overlay VisualElementを探す
                    // Find the overlay VisualElement
                    var overlayElement = FindByName(w.rootVisualElement, "moorestech/VolumeControl");
                    if (overlayElement == null) continue;

                    // OverlayToolbarを探してスライダーを注入
                    // Find OverlayToolbar and inject slider
                    var toolbar = FindByClassName(overlayElement, "unity-toolbar-overlay");
                    if (toolbar == null) continue;

                    // 既存のスライダーがあれば不要
                    // Skip if slider already injected
                    if (FindByName(toolbar, SliderName) != null) return;

                    InjectSlider(toolbar);
                    return;
                }
            }

            private static void InjectSlider(VisualElement toolbar)
            {
                // コンテナを作成
                // Create container
                var container = new VisualElement();
                container.name = SliderName;
                container.style.flexDirection = FlexDirection.Row;
                container.style.alignItems = Align.Center;
                container.style.height = 18;
                container.tooltip = "音量を変更します。左のボタンで100%にリセットできます。\nChange volume. Click the left button to reset to 100%.";

                // スライダー
                // Slider
                var sliderContainer = new VisualElement();
                sliderContainer.style.position = Position.Relative;
                sliderContainer.style.flexGrow = 1;
                sliderContainer.style.height = 18;

                _slider = new Slider(0f, 1f);
                _slider.style.width = 70;
                _slider.style.height = 18;
                sliderContainer.Add(_slider);

                // 値ラベル
                // Value label
                _currentValueLabel = new Label(VolumeValueText);
                _currentValueLabel.style.width = 16;
                _currentValueLabel.style.height = 18;
                _currentValueLabel.style.marginLeft = 4;
                _currentValueLabel.style.fontSize = 11;
                _currentValueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _currentValueLabel.style.color = new Color(0.8f, 0.8f, 0.8f);

                // スライダー値変更時のコールバック
                // Slider value changed callback
                _slider.RegisterValueChangedCallback(evt =>
                {
                    AudioListener.volume = evt.newValue;
                    _lastAudioVolume = evt.newValue;
                    _currentValueLabel.text = VolumeValueText;
                });
                _slider.value = AudioListener.volume;

                container.Add(sliderContainer);
                container.Add(_currentValueLabel);

                toolbar.Add(container);
            }

            private static VisualElement FindByName(VisualElement root, string name)
            {
                if (root.name == name) return root;
                foreach (var child in root.Children())
                {
                    var result = FindByName(child, name);
                    if (result != null) return result;
                }
                return null;
            }

            private static VisualElement FindByClassName(VisualElement root, string className)
            {
                if (root.ClassListContains(className)) return root;
                foreach (var child in root.Children())
                {
                    var result = FindByClassName(child, className);
                    if (result != null) return result;
                }
                return null;
            }
        }
    }
}
