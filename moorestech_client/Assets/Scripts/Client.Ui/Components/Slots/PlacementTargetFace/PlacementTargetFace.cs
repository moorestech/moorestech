using Client.Ui.Foundation.Icons;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 絵があれば絵、無ければ名前（Web の同名部品）
    /// The picture when present, otherwise the name (the Web counterpart)
    /// </summary>
    public sealed class PlacementTargetFace : VisualElement
    {
        private const string IconStateClass = "placement-target-face--icon";

        private readonly GameIcon _icon;
        private LocalizedLabel _nameLabel;

        public PlacementTargetFace()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("placement-target-face");
            _icon = new GameIcon();
            _icon.AddToClassList("placement-target-face__icon");
            Add(_icon);
        }

        public void SetIconTarget(UiIcon icon, UiText displayName)
        {
            // 欠けた対象も名前で描く（Web の !iconUrl）
            // A missing texture also draws the name (the Web !iconUrl)
            if (icon.Texture == null)
            {
                SetNameTarget(displayName);
                return;
            }
            _icon.SetIcon(icon, GameIconFallback.None);
            EnableInClassList(IconStateClass, true);
        }

        public void SetNameTarget(UiText displayName)
        {
            _icon.ClearIcon();
            EnableInClassList(IconStateClass, false);

            // キーの無い LocalizedLabel をパネルに載せないよう初回に作る
            // Create on first use so a key-less LocalizedLabel never sits on the panel
            if (_nameLabel == null)
            {
                _nameLabel = new LocalizedLabel { pickingMode = PickingMode.Ignore };
                _nameLabel.AddToClassList("placement-target-face__label");
                Add(_nameLabel);
            }
            _nameLabel.SetText(displayName);
        }
    }
}
