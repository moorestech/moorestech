using Client.Localization;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud.Parts
{
    /// <summary>
    /// 削除モードの右上の高さ 1 行
    /// The delete mode's top-right height line
    /// </summary>
    public sealed class DeleteBoxHeightHud : VisualElement
    {
        private readonly LocalizedLabel _height;

        public DeleteBoxHeightHud()
        {
            name = "delete-box-height-hud";
            AddToClassList("mode-hud__corner");
            _height = new LocalizedLabel();
            _height.AddToClassList(UiTypographyClass.KeyHintText);
            _height.AddToClassList("mode-hud__detail");
            Add(_height);
        }

        public void SetHeight(int height)
        {
            _height.SetText(PlacementHeightText.Create(height));
        }
    }
}
