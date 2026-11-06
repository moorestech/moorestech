using System;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// アイテム 1 マス（個数・所持/必要・ゴースト・ツールチップ）。Web の ItemSlot
    /// One item cell (count, owned/required, ghost and tooltip); the Web ItemSlot
    /// </summary>
    public sealed class ItemSlot : VisualElement
    {
        private const string ShortageStateClass = "item-slot--shortage";
        private const string LackStateClass = "item-slot__shortage--lack";

        private readonly SlotFrame _frame;
        private readonly SlotContent _content;
        private LocalizedLabel _shortageStroke;
        private LocalizedLabel _shortageLabel;

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
                // 中身の無いマスは絵も個数もツールチップも出さない（Web の disabled）
                // An empty cell shows no icon, count or tooltip (the Web disabled)
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
                HoverTooltipMark.Mark(_frame, presentation.Tooltip);
            }

            void ApplyShortage(ItemSlotShortage shortage)
            {
                // 包みは常に同じで、所持/必要の有無はラベルの出し入れだけで切り替える（Web の再マウント防止）
                // The wrapper never changes; owned/required toggles only the label (the Web remount guard)
                EnableInClassList(ShortageStateClass, shortage != null);
                if (shortage == null) return;

                // キーの無い LocalizedLabel をパネルに載せないよう初回に作る。白い縁の層を先に足し、字を上に描く
                // Create on first use so a key-less LocalizedLabel never sits on the panel; the white stroke layer goes first so the glyph draws on top
                if (_shortageLabel == null)
                {
                    _shortageStroke = CreateShortageLayer("item-slot__shortage-stroke");
                    _shortageLabel = CreateShortageLayer("item-slot__shortage");
                }
                var summary = SlotTexts.ItemCountSummary(shortage.OwnedCount, shortage.RequiredCount);
                _shortageStroke.SetText(summary);
                _shortageLabel.SetText(summary);
                _shortageLabel.EnableInClassList(LackStateClass, shortage.Lacking);
            }

            LocalizedLabel CreateShortageLayer(string layerClass)
            {
                var layer = new LocalizedLabel { pickingMode = PickingMode.Ignore };
                layer.AddToClassList("item-slot__shortage-layer");
                layer.AddToClassList(layerClass);
                Add(layer);
                return layer;
            }

            #endregion
        }
    }
}
