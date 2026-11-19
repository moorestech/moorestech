using System;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Filter;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Text;
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
        private readonly PixelFilterGroup _group = new(PixelFilterGroupFlow.Flow);

        // 中身は群の中へ積む（ロックの淡さを Web と同じく面・枠・アイコンを 1 枚にして掛けるため）
        // Children go inside the group so locked fading applies to face, border and icon as one layer, as on the Web
        public override VisualElement contentContainer => _group.contentContainer;

        public ResearchNodeCard(Guid researchGuid)
        {
            var key = researchGuid.ToString();
            name = $"research-node-{key}";
            AddToClassList("research-tree__card");

            // 面と枠は群の中身の要素が持つ。要素の opacity は子ごとに掛かり、重なったアイコンの白面が明るく残るため（A-222）
            // The face and border live on the group's content; element opacity applies per child and leaves the overlapping icon face bright (A-222)
            _group.contentContainer.AddToClassList("research-tree__card-face");
            hierarchy.Add(_group);

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

            // 選択の輪は枠の外へはみ出すので群の外に置き、ロックの淡さは USS が輪自身の opacity で当てる（群の filter は自分の箱の内側だけを描く）
            // The ring sits outside the border, so it stays out of the group and USS fades it by its own opacity when locked (the group filter draws only inside its own box)
            hierarchy.Add(ring);

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
            // アイコン未設定のノードは空の ID なので空きマスになる
            // A node without an icon has the empty id, so it becomes an empty cell
            _icon.SetContent(SlotItemContent.ForOptionalItem(content, card.IconItemId));
            _state.SetText(new UiText(card.StateLabelKey));
        }
    }
}
