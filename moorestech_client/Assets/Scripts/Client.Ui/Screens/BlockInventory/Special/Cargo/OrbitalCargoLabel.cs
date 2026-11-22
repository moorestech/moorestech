using System;
using Client.Localization;
using Client.Ui.Content;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Cargo
{
    internal sealed class OrbitalCargoLabel : LocalizedLabel
    {
        private readonly GameContentPresentation _content;
        private readonly bool _isNextFlight;
        private OrbitalCargoChoice _choice;
        private IDisposable _languageSubscription;

        internal OrbitalCargoLabel(GameContentPresentation content, bool isNextFlight) : base(UiText.Empty)
        {
            _content = content;
            _isNextFlight = isNextFlight;

            // パネルに戻ったときも、品名と成果を現在の言語で組み立てる
            // Compose the item names and outcome in the current language on reattachment too
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _languageSubscription = Localize.OnLanguageChanged.Subscribe(_ => RefreshText());
                RefreshText();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => _languageSubscription.Dispose());
        }

        internal void SetChoice(OrbitalCargoChoice choice)
        {
            _choice = choice;
            RefreshText();
        }

        private void RefreshText()
        {
            // 詳細の到着前は見出しに積荷がない
            // The headline has no cargo before the detail arrives
            SetText(_choice == null ? UiText.Empty : _isNextFlight
                ? OrbitalCargoNames.NextFlight(_choice, _content)
                : OrbitalCargoNames.Row(_choice, _content));
        }
    }
}
