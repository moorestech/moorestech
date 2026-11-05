using System.Collections.Generic;
using System.Text;
using Client.Playtest.Input;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// キーを 1 つずつ注入し、GameUi の規則より前と後で NavigationMove・Submit が何件来たかを数える
    /// Injects keys one at a time and counts NavigationMove/Submit seen before and after the GameUi root rules
    /// </summary>
    public sealed class NavigationEventCounter
    {
        public static readonly Key[] MeasuredKeys =
        {
            Key.W, Key.A, Key.S, Key.D,
            Key.UpArrow, Key.LeftArrow, Key.DownArrow, Key.RightArrow,
            Key.Space, Key.Enter,
        };

        private readonly VisualElement _panelTop;
        private readonly VisualElement _focusedElement;

        private int _moveBeforeRules;
        private int _submitBeforeRules;
        private int _moveAfterRules;
        private int _submitAfterRules;

        private NavigationEventCounter(VisualElement panelTop, VisualElement focusedElement)
        {
            _panelTop = panelTop;
            _focusedElement = focusedElement;
        }

        // 規則はルートの TrickleDown で伝播を止めるので、その上（panel.visualTree）の TrickleDown で先に数える
        // The rules stop propagation in the root's TrickleDown, so count first in the TrickleDown of panel.visualTree above it
        public static NavigationEventCounter Attach(VisualElement panelTop, VisualElement focusedElement)
        {
            var counter = new NavigationEventCounter(panelTop, focusedElement);
            panelTop.RegisterCallback<NavigationMoveEvent>(counter.CountMoveBeforeRules, TrickleDown.TrickleDown);
            panelTop.RegisterCallback<NavigationSubmitEvent>(counter.CountSubmitBeforeRules, TrickleDown.TrickleDown);
            focusedElement.RegisterCallback<NavigationMoveEvent>(counter.CountMoveAfterRules);
            focusedElement.RegisterCallback<NavigationSubmitEvent>(counter.CountSubmitAfterRules);
            return counter;
        }

        public void Detach()
        {
            _panelTop.UnregisterCallback<NavigationMoveEvent>(CountMoveBeforeRules, TrickleDown.TrickleDown);
            _panelTop.UnregisterCallback<NavigationSubmitEvent>(CountSubmitBeforeRules, TrickleDown.TrickleDown);
            _focusedElement.UnregisterCallback<NavigationMoveEvent>(CountMoveAfterRules);
            _focusedElement.UnregisterCallback<NavigationSubmitEvent>(CountSubmitAfterRules);
        }

        public async UniTask<Dictionary<Key, KeyNavigationCount>> MeasureEachKey(string label)
        {
            // 背面の Editor はキーボードを無効化し入力を捨てるので、測る間だけ背景でも届く設定にする（InteractKeyBlockOpener と同じ）
            // A background Editor disables the keyboard and drops input, so let it through only while measuring (same as InteractKeyBlockOpener)
            var inputSettings = InputSystem.settings;
            var (savedBackground, savedEditorInput) = (inputSettings.backgroundBehavior, inputSettings.editorInputBehaviorInPlayMode);
            inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            SemanticInput.EnsureDevices();
            var keyboardWasEnabled = Keyboard.current.enabled;
            InputSystem.EnableDevice(Keyboard.current);
            await UniTask.DelayFrame(1);

            var results = new Dictionary<Key, KeyNavigationCount>();
            var summary = new StringBuilder();
            foreach (var key in MeasuredKeys)
            {
                ResetCounts();

                // 押して離す。押下は 2 フレームなので移動の連打（既定 0.5 秒）は始まらない
                // Press and release; the 2-frame hold never reaches the move repeat (0.5 s by default)
                await SemanticInput.TapKey(key);
                await UniTask.DelayFrame(2);

                var count = new KeyNavigationCount(_moveBeforeRules, _submitBeforeRules, _moveAfterRules, _submitAfterRules);
                results.Add(key, count);
                Debug.Log($"[S4] phase={label} key={key} move={count.MoveBeforeRules} submit={count.SubmitBeforeRules} moveAfterRules={count.MoveAfterRules} submitAfterRules={count.SubmitAfterRules}");
                summary.Append($"{key}:move={count.MoveBeforeRules},submit={count.SubmitBeforeRules} ");
            }
            Debug.Log($"[S4] phase={label} summary {summary}");

            if (!keyboardWasEnabled) InputSystem.DisableDevice(Keyboard.current);
            inputSettings.backgroundBehavior = savedBackground;
            inputSettings.editorInputBehaviorInPlayMode = savedEditorInput;
            return results;
        }

        private void ResetCounts()
        {
            _moveBeforeRules = 0;
            _submitBeforeRules = 0;
            _moveAfterRules = 0;
            _submitAfterRules = 0;
        }

        private void CountMoveBeforeRules(NavigationMoveEvent evt) => _moveBeforeRules++;
        private void CountSubmitBeforeRules(NavigationSubmitEvent evt) => _submitBeforeRules++;
        private void CountMoveAfterRules(NavigationMoveEvent evt) => _moveAfterRules++;
        private void CountSubmitAfterRules(NavigationSubmitEvent evt) => _submitAfterRules++;
    }

    /// <summary>
    /// 1 キー分の計数。BeforeRules はルート規則より前、AfterRules は規則を通ったあと要素自身で受けた件数
    /// Counts for one key; BeforeRules is seen above the root rules, AfterRules is what reached the element itself past them
    /// </summary>
    public readonly struct KeyNavigationCount
    {
        public readonly int MoveBeforeRules;
        public readonly int SubmitBeforeRules;
        public readonly int MoveAfterRules;
        public readonly int SubmitAfterRules;

        public KeyNavigationCount(int moveBeforeRules, int submitBeforeRules, int moveAfterRules, int submitAfterRules)
        {
            MoveBeforeRules = moveBeforeRules;
            SubmitBeforeRules = submitBeforeRules;
            MoveAfterRules = moveAfterRules;
            SubmitAfterRules = submitAfterRules;
        }
    }
}
