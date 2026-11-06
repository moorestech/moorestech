using System;
using Client.Ui.Foundation.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// パネル付随の副次アクション用の押しボタン。面と寸法だけを持ち、押下は OnClicked で出す（Web の shared/ui/PanelActionButton）
    /// Secondary action button attached to a panel; supplies only face and size, reports presses through OnClicked (Web shared/ui/PanelActionButton)
    /// </summary>
    public sealed class PanelActionButton : Button
    {
        private readonly Subject<Unit> _onClicked = new();

        public IObservable<Unit> OnClicked => _onClicked;

        public PanelActionButton(string testid, LocalizationKey label)
        {
            name = testid;
            AddToClassList("panel-action-button");

            // GameUi は入力欄以外にフォーカスを持たせない
            // GameUi never lets anything but text fields hold focus
            focusable = false;

            // 押下は UniRx で外へ出す。Button.clicked はこの部品の中だけで受ける（AGENTS.md）
            // Presses leave through UniRx; Button.clicked is consumed only inside this component (AGENTS.md)
            clicked += () => _onClicked.OnNext(Unit.Default);

            var localizedLabel = new LocalizedLabel { pickingMode = PickingMode.Ignore };
            localizedLabel.AddToClassList("panel-action-button__label");
            localizedLabel.SetKey(label);
            Add(localizedLabel);
        }
    }
}
