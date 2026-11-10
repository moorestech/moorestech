using System;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// 研究ノードのカード1枚
    /// One research node card
    /// </summary>
    public sealed class ResearchNodeCard : VisualElement
    {
        private readonly LocalizedLabel _name;
        private readonly ItemSlot _icon;
        private readonly LocalizedLabel _state;

        public ResearchNodeCard(Guid researchGuid)
        {
            var key = researchGuid.ToString();
            name = $"research-node-{key}";
            AddToClassList("research-tree__card");

            // 名前・アイコン・状態を縦に積む
            // Stack name, icon and state
            _name = new LocalizedLabel();
            _name.AddToClassList("research-tree__card-name");
            _icon = new ItemSlot($"research-node-icon-{key}");
            _icon.AddToClassList("research-tree__card-icon");
            _state = new LocalizedLabel { name = $"research-node-state-{key}" };
            _state.AddToClassList("research-tree__card-state");
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("research-tree__card-ring");
            Add(_name);
            Add(_icon);
            Add(_state);
            Add(ring);

            // 文字は拾わない。アイコンは Web の ItemSlot と同じくホバーで名前を出すので拾い、押下は祖先のカードの包みへ浮かぶ（C4-d は対象の祖先から包みを探す）
            // Text does not pick; the icon picks for its hover name like the Web ItemSlot, and presses bubble to the card wrapper (C4-d finds it from the target's ancestors)
            UiPickingIgnore.ApplyToSubtree(_name);
            UiPickingIgnore.ApplyToSubtree(_state);
            TutorialAnchor.Mark(this, AnchorIds.ResearchNode(key));
        }

        public void Render(ResearchNodeCardViewModel card, GameContentPresentation content)
        {
            // 4状態と選択は修飾クラス
            // States and selection are modifier classes
            EnableInClassList("research-tree__card--completed", card.CardState.Completed);
            EnableInClassList("research-tree__card--ready", card.CardState.Ready);
            EnableInClassList("research-tree__card--locked", card.CardState.Locked);
            EnableInClassList("research-tree__card--selected", card.Selected);
            _name.SetText(UiText.FromContent(ContentLocalizationKeys.ResearchName(card.Guid)));
            // アイコン未設定のノードは空の ID なので null のアイコンにする（Item は空の ID で例外）
            // A node without an icon has the empty id, so pass a null icon (Item throws on it)
            var icon = card.IconItemId == ItemMaster.EmptyItemId ? null : content.Item(card.IconItemId);
            _icon.SetContent(new ItemSlotContent(icon, null, new ItemSlotMarks(false, false, false), null, null, null));
            _state.SetText(new UiText(card.StateLabelKey));
        }
    }
}
