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
    /// 言語ボタンの列（Web の写し）
    /// Row of language buttons (mirrors Web)
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

            // ボタンを一覧順に並べる
            // Buttons in list order
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
