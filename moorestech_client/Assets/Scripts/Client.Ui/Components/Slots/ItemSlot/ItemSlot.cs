using System;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// アイテム 1 マス（Web の ItemSlot）
    /// One item cell (the Web ItemSlot)
    /// </summary>
    public sealed class ItemSlot : VisualElement
    {
        private const string ShortageStateClass = "item-slot--shortage";
        private const string LackStateClass = "item-slot--lack";

        private readonly SlotFrame _frame;
        private readonly SlotContent _content;
        private readonly OutlinedCountLabel _shortage;

        public IObservable<SlotGesture> OnSlotGestured => _frame.OnSlotGestured;

        public ItemSlot(string frameTestid)
        {
            // 所持/必要は枠の外へはみ出し減光も受けないので、枠を包む外側の層に置く（Web の .shortageSlot）。testid は Web と同じく枠へ
            // Owned/required overflows the frame and escapes its dimming, so it lives in this wrapping layer (the Web .shortageSlot); the testid goes on the frame as on the Web
            pickingMode = PickingMode.Ignore;
            AddToClassList("item-slot");
            _frame = new SlotFrame(frameTestid);
            _content = new SlotContent();
            _frame.Add(_content);
            Add(_frame);
            _shortage = new OutlinedCountLabel(this, "item-slot__shortage");
        }

        public void SetContent(ItemSlotContent content)
        {
            var presentation = ItemSlotPresentation.From(content);
            _frame.SetState(new SlotFrameState(content.Marks.Selected, presentation.Filled, content.Marks.Catalog, content.Marks.Insufficient, presentation.GhostShown));
            ApplyShownContent();
            ApplyShortage(content.Shortage);

            #region Internal

            void ApplyShownContent()
            {
                // 空きマスは何も出さない（Web の disabled）
                // An empty cell shows nothing (the Web disabled)
                if (presentation.ShownIcon == null)
                {
                    _content.ClearIcon();
                    _content.ClearCount();
                    HoverTooltipMark.Unmark(_frame);
                    return;
                }
                _content.SetIcon(presentation.ShownIcon, GameIconFallback.IdText);
                if (presentation.ShowsBadge) _content.SetCount(presentation.BadgeCount);
                else _content.ClearCount();
                // 文の無い印を残さない。前に付けた印も外す
                // Never leave a mark without text; clear any earlier mark too
                // silent-ok: 名前を持たない表示素材は出す文が無い / 本番の UiIcon は GameContentPresentation が必ず名前を付け、名前なしの扱いは ItemSlotTest が固定する
                if (presentation.Tooltip == null) HoverTooltipMark.Unmark(_frame);
                else HoverTooltipMark.Mark(_frame, presentation.Tooltip);
            }

            void ApplyShortage(ItemSlotShortage shortage)
            {
                // 包みは常に同じで、所持/必要の有無はラベルの出し入れだけで切り替える（Web の再マウント防止）
                // The wrapper never changes; owned/required toggles only the label (the Web remount guard)
                EnableInClassList(ShortageStateClass, shortage != null);
                if (shortage == null) return;
                _shortage.SetText(SlotTexts.ItemCountSummary(shortage.OwnedCount, shortage.RequiredCount));
                EnableInClassList(LackStateClass, shortage.Lacking);
            }

            #endregion
        }
    }
}
