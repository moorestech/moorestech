using System.Collections.Generic;
using Client.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Client.UitkStage0Probe
{
    // S9: 単行・複数行のTextFieldを1画面に置き、IMEの状態とイベントを画面とログに出す。F8で表示を切り替える
    // S9: one screen with single- and multi-line TextFields, showing IME state and events on screen and in the log; F8 toggles it
    public class UitkImeProbeScreen : MonoBehaviour
    {
        private const int LogLines = 10;
        private readonly Queue<string> _eventLines = new();
        private UIDocument _document;
        private VisualElement _panel;
        private Label _stateLabel;
        private Label _eventLabel;
        private string _lastComposition = "";
        private bool _cefRunning;

        public static UitkImeProbeScreen Spawn(bool cefRunning, bool visible)
        {
            var screen = new GameObject("UitkStage0ImeProbe").AddComponent<UitkImeProbeScreen>();
            screen.Initialize(cefRunning, visible);
            return screen;
        }

        public void Initialize(bool cefRunning, bool visible)
        {
            _cefRunning = cefRunning;
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = ProbeAssets.CreatePanelSettings(20);
            _panel = UitkImeProbeViews.CreatePanel(cefRunning);
            _stateLabel = _panel.Q<Label>("ime-state");
            _eventLabel = _panel.Q<Label>("ime-events");
            RegisterField(_panel.Q<TextField>("ime-single"));
            RegisterField(_panel.Q<TextField>("ime-multi"));
            _document.rootVisualElement.Add(_panel);
            SetVisible(visible);
            Debug.Log($"[UitkIme] probe screen ready cefRunning={cefRunning} imeCompositionMode={UnityEngine.Input.imeCompositionMode}");
        }

        public void SetVisible(bool visible)
        {
            _panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            AddEvent($"visible={visible}");
        }

        private void RegisterField(TextField field)
        {
            field.RegisterCallback<FocusInEvent>(_ => OnFocusChanged(field, true));
            field.RegisterCallback<FocusOutEvent>(_ => OnFocusChanged(field, false));
            field.RegisterValueChangedCallback(evt => AddEvent($"{field.name} change len={evt.newValue.Length} \"{Tail(evt.newValue)}\""));
            field.RegisterCallback<KeyDownEvent>(evt => OnKeyDown(field, evt), TrickleDown.TrickleDown);
        }

        private void OnFocusChanged(TextField field, bool focused)
        {
            // 入力欄にフォーカス中はゲームのキー操作を止める（Webの textInputFocused と同じ口を使う）
            // While a field is focused, stop game keys through the same gate as the Web textInputFocused
            WebUiInputExclusivity.SetState(false, focused);
            AddEvent($"{field.name} {(focused ? "FocusIn" : "FocusOut")}");
        }

        private void OnKeyDown(TextField field, KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.None && evt.character == '\0') return;
            AddEvent($"{field.name} KeyDown key={evt.keyCode} char=U+{(int)evt.character:X4} mods={evt.modifiers}");

            // ESCはフォーカスを外すだけ（計画§3.8の方針どおり）
            // ESC only blurs the field, as planned in §3.8
            if (evt.keyCode != KeyCode.Escape) return;
            field.Blur();
            AddEvent($"{field.name} ESC -> Blur");
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame) SetVisible(_panel.style.display == DisplayStyle.None);

            // 変換中文字列はUITKのイベントに出ないので、毎フレーム読んで変化を記録する
            // The composition string is not surfaced as a UITK event, so read it every frame and record changes
            var composition = UnityEngine.Input.compositionString;
            if (composition != _lastComposition)
            {
                AddEvent($"composition \"{composition}\"");
                _lastComposition = composition;
            }
            var focused = _document.rootVisualElement.panel.focusController.focusedElement as VisualElement;
            _stateLabel.text = $"F{Time.frameCount} CEF={(_cefRunning ? "起動" : "停止")} imeCompositionMode={UnityEngine.Input.imeCompositionMode} composing=\"{composition}\" focus={focused?.name ?? "none"} textInputFocused={WebUiInputExclusivity.IsTextInputFocused}";
        }

        private void LateUpdate()
        {
            // 表示中はカーソルを解放し続ける（ゲームの状態遷移がロックし直すため）
            // Keep the cursor free while visible, since game state transitions re-lock it
            if (_panel.style.display == DisplayStyle.None) return;
            if (UnityEngine.Cursor.lockState != CursorLockMode.None) InputManager.MouseCursorVisible(true);
        }

        private void AddEvent(string line)
        {
            var stamped = $"F{Time.frameCount} {line}";
            Debug.Log($"[UitkIme] {stamped}");
            _eventLines.Enqueue(stamped);
            while (LogLines < _eventLines.Count) _eventLines.Dequeue();
            if (_eventLabel != null) _eventLabel.text = string.Join("\n", _eventLines);
        }

        private static string Tail(string value)
        {
            return value.Length <= 24 ? value : "…" + value.Substring(value.Length - 24);
        }
    }
}
