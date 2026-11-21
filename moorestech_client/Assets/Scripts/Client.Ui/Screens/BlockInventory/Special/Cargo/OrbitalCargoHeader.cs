using System;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 選んだ積荷の見出し（今の便があればその積荷と成果）と「次の便から ◯◯」の 1 行。押すと一覧へ戻る（前例 SelectedRecipeHeader）
    /// Selected cargo headline (the current flight's cargo and outcome when flying) plus one "from the next flight" line; pressing returns to the list (precedent SelectedRecipeHeader)
    /// </summary>
    public sealed class OrbitalCargoHeader : VisualElement
    {
        private readonly UiGroup _group = new(UiGap.Xs);
        private readonly ItemSlot _slot;
        private readonly OrbitalCargoLabel _name;
        private readonly OrbitalCargoLabel _nextFlight;
        private readonly Subject<Unit> _pressed = new();
        private readonly GameContentPresentation _content;
        private bool _hasShown;
        private ItemId? _shownHeadline;
        private ItemId? _shownNextFlight;

        public IObservable<Unit> OnPressed => _pressed;

        public OrbitalCargoHeader(string testPrefix, GameContentPresentation content, IUiOsCursor osCursor)
        {
            focusable = false;
            name = $"{testPrefix}-cargo-header";
            AddToClassList("orbital-cargo-header");
            _content = content;
            _slot = new ItemSlot($"{testPrefix}-cargo-header-item");
            _name = new OrbitalCargoLabel(content, false) { name = $"{testPrefix}-cargo-header-name", pickingMode = PickingMode.Ignore };
            _nextFlight = new OrbitalCargoLabel(content, true) { name = $"{testPrefix}-cargo-next-flight", pickingMode = PickingMode.Ignore };
            _nextFlight.AddToClassList(UiTypographyClass.TextSm);
            _nextFlight.AddToClassList(UiTypographyClass.TextDimmed);
            _group.pickingMode = PickingMode.Ignore;
            _group.AddToClassList(UiLayoutClass.GroupJustifyCenter);
            _group.AddItem(_slot);
            _group.AddItem(_name);
            Add(_group);
            Add(_nextFlight);

            // 押すと積荷を選び直す。押せる印の指は OS カーソルへ要素の層で要求する（前例 SelectedRecipeHeader）
            // Pressing reselects the cargo; the pressable pointer is requested on the element layer (precedent SelectedRecipeHeader)
            HoverTooltipMark.Mark(this, new UiText(LocalizationKeys.Ui.BlockInventory.ChangeOrbitalCargo));
            RegisterCallback<ClickEvent>(_ => _pressed.OnNext(Unit.Default));
            osCursor.CreateHoverRequest(this, UiOsCursorShape.Pointer, UiOsCursorLayer.Element);
        }

        public void Render(OrbitalCargoDetail cargo)
        {
            var hasHeadline = cargo.TryGetHeadline(out var headline);
            var hasNextFlight = cargo.TryGetNextFlight(out var nextFlight);
            ItemId? headlineId = hasHeadline ? headline.PayloadItemId : null;
            ItemId? nextFlightId = hasNextFlight ? nextFlight.PayloadItemId : null;
            // 見出しと次の便が前回と同じなら描き直さない（状態更新ごとに作り直さない。D-37）
            // Skip redrawing while the headline and next flight are unchanged (not rebuilt per state update; D-37)
            if (_hasShown && _shownHeadline == headlineId && _shownNextFlight == nextFlightId) return;
            _hasShown = true;
            _shownHeadline = headlineId;
            _shownNextFlight = nextFlightId;

            UiDisplay.SetShown(_group, hasHeadline);
            if (hasHeadline)
            {
                _slot.SetContent(SlotItemContent.ForItem(_content, headline.PayloadItemId, headline.CountPerLaunch));
                _name.SetChoice(headline);
            }
            UiDisplay.SetShown(_nextFlight, hasNextFlight);
            if (hasNextFlight) _nextFlight.SetChoice(nextFlight);
        }
    }
}
