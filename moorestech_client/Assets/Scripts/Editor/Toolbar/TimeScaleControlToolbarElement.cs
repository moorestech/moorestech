using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Editor.Toolbar
{
    /// <summary>
    /// TimeScaleコントロールをツールバーに追加する（参考: UnityToolbarExtension）
    /// Add TimeScale control to the toolbar (ref: UnityToolbarExtension)
    /// </summary>
    public static class TimeScaleControlToolbarElement
    {
        private const string ElementPath = "moorestech/TimeScale Control";
        private const string SliderName = "moorestech-timescale-slider";
        private const float MaxTimeScale = 10f;
        private const float DefaultTimeScaleRange = 0.13f;

        private static Slider _slider;
        private static Label _currentValueLabel;
        private static float _lastTimeScale = 1f;

        private static string TimeScaleValueText => $"\u00d7{Time.timeScale:F1}";

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // 外部からのTimeScale変更を検知してUI更新
            // Detect external TimeScale changes and update UI
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (_slider == null || _currentValueLabel == null) return;

            // Time.timeScaleが外部から変更された場合にUIを更新
            // Update UI if Time.timeScale was changed externally
            if (Mathf.Abs(Time.timeScale - _lastTimeScale) > 0.001f)
            {
                _lastTimeScale = Time.timeScale;
                _slider.SetValueWithoutNotify(ConvertTimeScaleToSliderValue(_lastTimeScale));
                _currentValueLabel.text = TimeScaleValueText;
            }
        }

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 0)]
        public static MainToolbarElement CreateElement()
        {
            // ボタンとして登録（アイコンのみ）
            // Register as button (icon only)
            var icon = ToolbarUtility.GetBuiltInIcon("d_UnityEditor.AnimationWindow");
            var content = new MainToolbarContent(icon, "TimeScaleコントロール / TimeScale Control");
            var button = new MainToolbarButton(content, () =>
            {
                // TimeScaleを1.0にリセット
                // Reset TimeScale to 1.0
                Time.timeScale = 1f;
                _lastTimeScale = 1f;
                if (_slider != null) _slider.value = 0f;
                if (_currentValueLabel != null) _currentValueLabel.text = TimeScaleValueText;
            });
            return button;
        }

        // Overlayの中に直接スライダーUIを注入する
        // Inject slider UI directly into the overlay
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
                if (_slider != null && _slider.panel != null) return;

                var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                foreach (var w in windows)
                {
                    if (w.GetType().FullName != "UnityEditor.MainToolbarWindow") continue;

                    var overlayElement = FindByName(w.rootVisualElement, "moorestech/TimeScaleControl");
                    if (overlayElement == null) continue;

                    var toolbar = FindByClassName(overlayElement, "unity-toolbar-overlay");
                    if (toolbar == null) continue;

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
                container.tooltip = "ゲーム速度を変更します。左のボタンで×1.0にリセットできます。\nChange game speed. Click the left button to reset to ×1.0.";

                // スライダーコンテナ（中央線付き）
                // Slider container (with center line)
                var sliderContainer = new VisualElement();
                sliderContainer.style.position = Position.Relative;
                sliderContainer.style.flexGrow = 1;
                sliderContainer.style.height = 18;

                _slider = new Slider(-1f, 1f);
                _slider.style.width = 70;
                _slider.style.height = 18;

                // 中央線（TimeScale 1.0の位置を示す）
                // Center line (indicates TimeScale 1.0 position)
                var centerLine = new VisualElement();
                centerLine.style.position = Position.Absolute;
                centerLine.style.left = _slider.style.width.value.value / 2f + 3f;
                centerLine.style.top = 2;
                centerLine.style.bottom = 0;
                centerLine.style.width = 1;
                centerLine.style.backgroundColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
                centerLine.style.marginLeft = -0.5f;

                sliderContainer.Add(centerLine);
                sliderContainer.Add(_slider);

                // 値ラベル
                // Value label
                _currentValueLabel = new Label(TimeScaleValueText);
                _currentValueLabel.style.width = 36;
                _currentValueLabel.style.height = 18;
                _currentValueLabel.style.marginLeft = 2;
                _currentValueLabel.style.fontSize = 11;
                _currentValueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _currentValueLabel.style.color = new Color(0.8f, 0.8f, 0.8f);

                // スライダー値変更時のコールバック（非線形マッピング）
                // Slider value changed callback (non-linear mapping)
                _slider.RegisterValueChangedCallback(evt =>
                {
                    _lastTimeScale = ConvertSliderValueToTimeScale(evt.newValue);
                    Time.timeScale = _lastTimeScale;
                    _currentValueLabel.text = TimeScaleValueText;
                });
                _slider.value = ConvertTimeScaleToSliderValue(Time.timeScale);

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

        // スライダー値→TimeScale変換（参考実装と同じ非線形マッピング）
        // Slider value to TimeScale (same non-linear mapping as reference)
        private static float ConvertSliderValueToTimeScale(float sliderValue)
        {
            if (sliderValue > 0)
            {
                if (sliderValue <= DefaultTimeScaleRange) return 1f;
                var value = (sliderValue - DefaultTimeScaleRange) / (1f - DefaultTimeScaleRange);
                value = Mathf.Pow(value, 2);
                return 1f + value * (MaxTimeScale - 1f);
            }
            else
            {
                if (sliderValue >= -DefaultTimeScaleRange) return 1f;
                var value = (sliderValue + DefaultTimeScaleRange) / (1f - DefaultTimeScaleRange);
                return 1f + value;
            }
        }

        // TimeScale→スライダー値変換
        // TimeScale to slider value
        private static float ConvertTimeScaleToSliderValue(float timeScale)
        {
            if (Mathf.Approximately(timeScale, 1f)) return 0f;

            if (timeScale > 1f)
            {
                var normalizedValue = (timeScale - 1f) / (MaxTimeScale - 1f);
                var value = Mathf.Sqrt(normalizedValue);
                return DefaultTimeScaleRange + value * (1f - DefaultTimeScaleRange);
            }
            else
            {
                var value = timeScale - 1f;
                return value * (1f - DefaultTimeScaleRange) - DefaultTimeScaleRange;
            }
        }
    }
}
