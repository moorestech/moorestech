using Client.Ui.Foundation.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Components
{
    /// <summary>
    /// パネル付随の副次アクション用の押しボタン。面と寸法だけを持ち、押下は Button.clicked で受ける（Web の shared/ui/PanelActionButton）
    /// Secondary action button attached to a panel; supplies only face and size, presses arrive via Button.clicked (Web shared/ui/PanelActionButton)
    /// </summary>
    public sealed class PanelActionButton : Button
    {
        public PanelActionButton(string testid, LocalizationKey label, StyleSheet sheet)
        {
            name = testid;
            styleSheets.Add(sheet);
            AddToClassList("panel-action-button");

            // GameUi は入力欄以外にフォーカスを持たせない
            // GameUi never lets anything but text fields hold focus
            focusable = false;

            var localizedLabel = new LocalizedLabel { pickingMode = PickingMode.Ignore };
            localizedLabel.AddToClassList("panel-action-button__label");
            localizedLabel.SetKey(label);
            Add(localizedLabel);
        }
    }
}
