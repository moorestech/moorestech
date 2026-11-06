using System;
using System.Collections.Generic;
using Client.Ui.Foundation.Text;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// 択一ボタン列（SetSelectedで表示、OnSelectedで通知）
    /// Exclusive button row; SetSelected shows, OnSelected notifies
    /// </summary>
    public sealed class ModeSwitch<TValue> : VisualElement
    {
        private const string SelectedClass = "mode-switch__option--selected";

        private readonly IReadOnlyList<ModeSwitchOption<TValue>> _options;
        private readonly List<Button> _buttons = new();
        private readonly Subject<TValue> _onSelected = new();

        public IObservable<TValue> OnSelected => _onSelected;

        public ModeSwitch(string testid, ModeSwitchOrientation orientation, IReadOnlyList<ModeSwitchOption<TValue>> options)
        {
            name = testid;
            _options = options;
            AddToClassList("mode-switch");
            AddToClassList(orientation == ModeSwitchOrientation.Vertical ? "mode-switch--vertical" : "mode-switch--horizontal");

            for (var i = 0; i < options.Count; i++)
            {
                var button = CreateOptionButton(options[i], 0 < i);
                _buttons.Add(button);
                Add(button);
            }
        }

        // 一致なしは無選択
        // No match means no selection
        public void SetSelected(TValue value)
        {
            var comparer = EqualityComparer<TValue>.Default;
            for (var i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].EnableInClassList(SelectedClass, comparer.Equals(_options[i].Value, value));
            }
        }

        public void SetSelectedNone()
        {
            foreach (var button in _buttons) button.RemoveFromClassList(SelectedClass);
        }

        // 無効ボタンはclickedなし
        // Disabled buttons raise no clicked
        public void SetEnabledAll(bool enabled)
        {
            EnableInClassList("mode-switch--disabled", !enabled);
            foreach (var button in _buttons) button.SetEnabled(enabled);
        }

        private Button CreateOptionButton(ModeSwitchOption<TValue> option, bool following)
        {
            var button = new Button { name = option.Testid, focusable = false };
            button.AddToClassList("mode-switch__option");

            // 2つ目以降に間隔クラス
            // Spacing class after the first
            if (following) button.AddToClassList("mode-switch__option--following");

            // 値はそのまま、キーは再解決ラベル
            // Values as-is, keys via re-resolving label
            if (option.LabelKey.HasValue)
            {
                var label = new LocalizedLabel { pickingMode = PickingMode.Ignore };
                label.AddToClassList("mode-switch__label");
                label.SetKey(option.LabelKey.Value);
                button.Add(label);
            }
            else
            {
                button.text = option.LiteralLabel;
            }

            var value = option.Value;
            button.clicked += () => _onSelected.OnNext(value);
            return button;
        }
    }
}
