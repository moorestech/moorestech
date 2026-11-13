using System;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// 副次アクション用の押しボタン（面と寸法のみ）
    /// Secondary action button (face and size only)
    /// </summary>
    public sealed class PanelActionButton : Button
    {
        private readonly Subject<Unit> _onClicked = new();

        public IObservable<Unit> OnClicked => _onClicked;

        public PanelActionButton(string testid, UiText label)
        {
            name = testid;
            AddToClassList("panel-action-button");

            // 入力欄以外はフォーカスなし
            // Only text fields hold focus
            focusable = false;

            // 押下はUniRxで出す
            // Presses leave through UniRx
            clicked += () => _onClicked.OnNext(Unit.Default);

            var localizedLabel = new LocalizedLabel { pickingMode = PickingMode.Ignore };
            localizedLabel.AddToClassList("panel-action-button__label");
            localizedLabel.SetText(label);
            Add(localizedLabel);
        }
    }
}
