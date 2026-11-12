using System;
using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Layout;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.EventMode.Parts
{
    /// <summary>
    /// 言語のボタンの列（Web EventLanguageGateBody の写し。待機中に出ない結末の 1 行は持たない）
    /// The row of language buttons (mirrors Web EventLanguageGateBody, without the outcome line that never shows while waiting)
    /// </summary>
    public sealed class EventLanguageGateBody : VisualElement
    {
        public const string OptionTestIdPrefix = "event-language-gate-option-";

        private readonly Subject<string> _onLanguageChosen = new();

        public IObservable<string> OnLanguageChosen => _onLanguageChosen;

        public EventLanguageGateBody(IReadOnlyList<LanguageInfo> languages)
        {
            AddToClassList("event-language-gate__body");
            pickingMode = PickingMode.Ignore;

            // 母国語表記のボタンを一覧の順に並べる（Web Group justify center gap lg・Button size xl）
            // Native-name buttons in list order (Web Group justify center gap lg, Button size xl)
            var group = new UiGroup(UiGap.Lg) { pickingMode = PickingMode.Ignore };
            group.AddToClassList(UiLayoutClass.GroupJustifyCenter);
            foreach (var language in languages) group.AddItem(CreateOption(language));
            Add(group);

            #region Internal

            FormButton CreateOption(LanguageInfo language)
            {
                var button = new FormButton(OptionTestIdPrefix + language.Code, UiText.FromVerbatim(language.DisplayName), FormButtonVariant.Filled);
                button.SetSize(FormButtonSize.ExtraLarge);
                var code = language.Code;
                button.OnClicked.Subscribe(_ => _onLanguageChosen.OnNext(code));
                return button;
            }

            #endregion
        }
    }
}
