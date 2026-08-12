using System;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Hotbar;

namespace Client.Game.InGame.UI.UIState.State.Hotbar
{
    /// <summary>
    ///     建築モード中の数字キー/Web由来ホットバー入力を持ち替え・長押し割当・画面遷移へ振り分ける
    ///     Routes digit-key/web-originated hotbar input during build mode to target swap, long-press assign, and screen transitions
    /// </summary>
    public class PlaceBlockHotbarInputService
    {
        private readonly ClientHotbarDatastore _clientHotbarDatastore;
        private readonly HotbarPlacementTargetResolver _hotbarPlacementTargetResolver;
        private readonly PlaceSystemStateController _placeSystemStateController;

        public PlaceBlockHotbarInputService(ClientHotbarDatastore clientHotbarDatastore, HotbarPlacementTargetResolver hotbarPlacementTargetResolver, PlaceSystemStateController placeSystemStateController)
        {
            _clientHotbarDatastore = clientHotbarDatastore;
            _hotbarPlacementTargetResolver = hotbarPlacementTargetResolver;
            _placeSystemStateController = placeSystemStateController;
        }

        // タップされたスロットを同一枠/別枠/空枠(・未解決枠)の3通りへ振り分ける
        // Routes a tapped slot into same-slot / different-slot / empty-or-unresolved-slot handling
        public bool TryGetTapTransit(out UITransitContext transit)
        {
            transit = null;
            var tapRequested = HotbarKeyInput.TryGetTappedSlot(out var slot) || _clientHotbarDatastore.TryConsumeSelectRequest(out slot);
            if (!tapRequested) return false;

            // 同じ枠→建築モードを終了する
            // The same slot exits build mode
            if (slot == _clientHotbarDatastore.SelectedSlot)
            {
                transit = new UITransitContext(UIStateEnum.GameScreen);
                return true;
            }

            var targetId = _clientHotbarDatastore.Assignments[slot];
            if (targetId != Guid.Empty && _hotbarPlacementTargetResolver.TryResolve(targetId, out var entry))
            {
                // 別の割当枠→画面遷移せず設置対象を持ち替える
                // A different assigned slot swaps the placement target in place without a screen transition
                _clientHotbarDatastore.SetSelectedSlot(slot);
                _placeSystemStateController.SetTarget(PlacementTargetFactory.Create(entry));
                return false;
            }

            // 空枠・未解決枠→建築モードを終了する
            // An empty or unresolved slot exits build mode
            transit = new UITransitContext(UIStateEnum.GameScreen);
            return true;
        }

        // 長押しは現在の設置対象をその枠へ割り当てる
        // A long press assigns the current placement target to that slot
        public void ApplyLongPressAssign()
        {
            if (HotbarKeyInput.TryGetLongPressedSlot(out var slot) && _placeSystemStateController.CurrentTarget != null)
                _clientHotbarDatastore.RequestAssign(slot, _placeSystemStateController.CurrentTarget.Id);
        }
    }
}
