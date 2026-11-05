using System;
using System.Collections.Generic;
using Client.Ui.Foundation.Text;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Components
{
    /// <summary>
    /// 任意の値を択一で選ぶボタン列。選択の表示は SetSelected で外から入れ、押下は OnSelected で流す（Web の shared/ui/ModeSwitch）
    /// Exclusive button row over arbitrary values; selection display is pushed in via SetSelected, presses flow out through OnSelected (Web shared/ui/ModeSwitch)
    /// </summary>
    public sealed class ModeSwitch<TValue> : VisualElement
    {
        private const string SelectedClass = "mode-switch__option--selected";

        private readonly IReadOnlyList<ModeSwitchOption<TValue>> _options;
        private readonly List<Button> _buttons = new();
        private readonly Subject<TValue> _onSelected = new();

        public IObservable<TValue> OnSelected => _onSelected;

        public ModeSwitch(string testid, ModeSwitchOrientation orientation, IReadOnlyList<ModeSwitchOption<TValue>> options, StyleSheet sheet)
        {
            name = testid;
            _options = options;
            styleSheets.Add(sheet);
            AddToClassList("mode-switch");
            AddToClassList(orientation == ModeSwitchOrientation.Vertical ? "mode-switch--vertical" : "mode-switch--horizontal");

            for (var i = 0; i < options.Count; i++)
            {
                var button = CreateOptionButton(options[i], 0 < i);
                _buttons.Add(button);
                Add(button);
            }
        }

        // 一致する選択肢が無ければ無選択にする（Web の value が options に無い場合と同じ）
        // With no matching option the switch shows no selection (same as a Web value outside the options)
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

        // 無効な Button は clicked を出さないので、押しても OnSelected は流れない
        // A disabled Button never raises clicked, so presses never reach OnSelected
        public void SetEnabledAll(bool enabled)
        {
            EnableInClassList("mode-switch--disabled", !enabled);
            foreach (var button in _buttons) button.SetEnabled(enabled);
        }

        private Button CreateOptionButton(ModeSwitchOption<TValue> option, bool following)
        {
            var button = new Button { name = option.Testid, focusable = false };
            button.AddToClassList("mode-switch__option");

            // USS に gap と :first-child が使えないので、2 つ目以降に間隔のクラスを付ける
            // USS lacks gap and :first-child is banned, so options after the first get the spacing class
            if (following) button.AddToClassList("mode-switch__option--following");

            // 翻訳しない値はそのまま、キーは言語変更で解決し直すラベルで出す
            // Untranslated values go in as-is; keys use a label that re-resolves on language change
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
