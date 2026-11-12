using System;
using System.Collections.Generic;
using Client.Ui.Foundation.Text;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// 択一のボタン列（Web ModeSwitch の写し）
    /// Single-choice button row (copy of Web ModeSwitch)
    /// </summary>
    public sealed class ModeSwitch<TValue> : VisualElement
    {
        private const string SelectedClass = "mode-switch__option--selected";
        private const string OptionDisabledClass = "mode-switch__option--option-disabled";
        private const string DisabledClass = "mode-switch--disabled";

        private readonly IReadOnlyList<ModeSwitchOption<TValue>> _options;
        private readonly List<Button> _buttons = new();
        private readonly HashSet<int> _disabledOptionIndexes = new();
        private readonly Subject<TValue> _onOptionSelected = new();
        private bool _enabledAll = true;

        public IObservable<TValue> OnOptionSelected => _onOptionSelected;

        public ModeSwitch(string testid, ModeSwitchOrientation orientation, ModeSwitchRole role, IReadOnlyList<ModeSwitchOption<TValue>> options)
        {
            name = testid;
            _options = options;
            AddToClassList("mode-switch");
            AddToClassList(orientation == ModeSwitchOrientation.Vertical ? "mode-switch--vertical" : "mode-switch--horizontal");
            EnableInClassList("mode-switch--tablist", role == ModeSwitchRole.Tablist);

            for (var i = 0; i < options.Count; i++)
            {
                var button = CreateOptionButton(options[i], 0 < i);
                _buttons.Add(button);
                Add(button);
            }
        }

        public void SetSelected(TValue value)
        {
            SetSelectedNone();
            // silent-ok: 一致なしは無選択で、Web の ModeSwitch と同じ仕様（設定の読み込み前など） / ModeSwitchTest「UnknownValueAndSetSelectedNoneLeaveNothingSelected」が固定する
            if (!TryIndexOf(value, out var index)) return;
            _buttons[index].AddToClassList(SelectedClass);
        }

        public void SetSelectedNone()
        {
            foreach (var button in _buttons) button.RemoveFromClassList(SelectedClass);
        }

        // 全体の無効は根の状態と各ボタンの無効。選択肢ごとの無効は解かない
        // Whole-switch disabling sets the root state and every button; it never clears per-option disabling
        public void SetEnabledAll(bool enabled)
        {
            _enabledAll = enabled;
            EnableInClassList(DisabledClass, !enabled);
            ApplyButtonEnabled();
        }

        public void SetOptionEnabled(TValue value, bool enabled)
        {
            // 無い値は呼び出し側の取り違えなので、黙って無視せずログで知らせる
            // A missing value is a caller mix-up, so report it instead of ignoring it
            if (!TryIndexOf(value, out var index))
            {
                Debug.LogError($"[ModeSwitch] {name}: no option has the value {value}");
                return;
            }
            if (enabled) _disabledOptionIndexes.Remove(index);
            else _disabledOptionIndexes.Add(index);
            _buttons[index].EnableInClassList(OptionDisabledClass, !enabled);
            ApplyButtonEnabled();
        }

        private void ApplyButtonEnabled()
        {
            // 無効のボタンは clicked を出さない（Web の disabled と同じ）
            // Disabled buttons raise no clicked (same as the Web disabled)
            for (var i = 0; i < _buttons.Count; i++) _buttons[i].SetEnabled(_enabledAll && !_disabledOptionIndexes.Contains(i));
        }

        // 値の比較はこの 1 か所だけ（SetSelected・SetOptionEnabled が共有する）
        // The only place comparing values (shared by SetSelected and SetOptionEnabled)
        private bool TryIndexOf(TValue value, out int index)
        {
            var comparer = EqualityComparer<TValue>.Default;
            for (index = 0; index < _options.Count; index++)
            {
                if (comparer.Equals(_options[index].Value, value)) return true;
            }
            index = -1;
            return false;
        }

        private Button CreateOptionButton(ModeSwitchOption<TValue> option, bool following)
        {
            var button = new Button { name = option.Testid, focusable = false };
            button.AddToClassList("mode-switch__option");

            // 2 つ目以降に間隔のクラス
            // Spacing class from the second option on
            if (following) button.AddToClassList("mode-switch__option--following");

            // Web の box-shadow の輪は面の外へ出した子で描く（ModeSwitch.uss）
            // The Web box-shadow ring is a child pushed outside the face (ModeSwitch.uss)
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("mode-switch__ring");
            button.Add(ring);

            // 文は辞書の切替で解き直すラベルに載せる
            // The label re-resolves on dictionary switches
            var label = new LocalizedLabel { pickingMode = PickingMode.Ignore };
            label.AddToClassList("mode-switch__label");
            label.SetText(option.Label);
            button.Add(label);

            var value = option.Value;
            button.clicked += () => _onOptionSelected.OnNext(value);
            return button;
        }
    }
}
