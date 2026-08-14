using System;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Hotbar;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.Hotbar
{
    /// <summary>
    ///     数字キー/Web由来のホットバータップを消費し、建築モードの開始・持ち替え・終了へ振り分ける唯一の入口
    ///     The single entry point that consumes digit-key/web-originated hotbar taps and routes them to entering, swapping, or leaving build mode
    /// </summary>
    public class HotbarTapInputService
    {
        private readonly ClientHotbarDatastore _clientHotbarDatastore;
        private readonly PlacementTargetResolver _placementTargetResolver;
        private readonly PlaceSystemStateController _placeSystemStateController;
        private readonly HotbarKeyInput _hotbarKeyInput;

        public HotbarTapInputService(ClientHotbarDatastore clientHotbarDatastore, PlacementTargetResolver placementTargetResolver, PlaceSystemStateController placeSystemStateController, HotbarKeyInput hotbarKeyInput)
        {
            _clientHotbarDatastore = clientHotbarDatastore;
            _placementTargetResolver = placementTargetResolver;
            _placeSystemStateController = placeSystemStateController;
            _hotbarKeyInput = hotbarKeyInput;
        }

        // ゲーム画面のタップ。解決枠のみ建築モードへ
        // A tap on the game screen: only a resolved slot transits into build mode, empty and unresolved slots are ignored
        public bool TryGetEnterBuildTransit(out UITransitContext transit)
        {
            transit = null;
            if (!TryConsumeTappedTarget(out var slot, out var target)) return false;
            if (target == null) return false;

            transit = new UITransitContext(UIStateEnum.PlaceBlock, UITransitContextContainer.Create(new PlacementSelection(target, PlacementOrigin.FromHotbarSlot(slot))));
            return true;
        }

        // 建築モード中タップを同一/別/空枠へ振り分け
        // Routes a tap during build mode into same-slot / different-slot / empty-or-unresolved-slot handling
        public bool TryGetTapTransit(out UITransitContext transit)
        {
            transit = null;
            if (!TryConsumeTappedTarget(out var slot, out var target)) return false;

            // 同一/空/未解決枠→建築モード終了
            // The same slot, an empty slot, or an unresolved slot exits build mode; the origin slot is read from the placement target's owner
            var isOriginSlot = _placeSystemStateController.CurrentOrigin.TryGetHotbarSlot(out var originSlot) && originSlot == slot;
            if (isOriginSlot || target == null)
            {
                transit = new UITransitContext(UIStateEnum.GameScreen);
                return true;
            }

            // 別枠→遷移せず由来ごと持ち替え
            // A different assigned slot swaps the placement target together with its origin, without a screen transition
            _placeSystemStateController.SetTarget(target, PlacementOrigin.FromHotbarSlot(slot));
            return false;
        }

        // 長押しで設置対象を枠へ割当
        // A long press assigns the current placement target to that slot
        public void ApplyLongPressAssign()
        {
            PollKeyInput();
            if (_hotbarKeyInput.TryGetLongPressedSlot(out var slot) && _placeSystemStateController.CurrentTarget != null)
                _clientHotbarDatastore.RequestAssign(slot, _placeSystemStateController.CurrentTarget.Id);
        }

        // UIState遷移のたびに押下状態を捨てる。復帰直後の誤長押し判定を防ぐ
        // Drops the held-key state on every UIState transition to prevent a false long press right after returning
        public void ResetKeyState()
        {
            _hotbarKeyInput.Reset();
        }

        // タップされた枠と、そこから解決した設置対象を1度だけ取り出す。空枠・未解決枠はtargetがnullになる
        // Takes the tapped slot and the target resolved from it exactly once; target is null for an empty or unresolved slot
        private bool TryConsumeTappedTarget(out int slot, out IPlacementTarget target)
        {
            target = null;
            PollKeyInput();

            // 同フレームに両方来ても双方を消費する。短絡させるとWeb側の要求だけ次フレームへ残り遅れて発火する
            // Consume both sources even when they arrive on the same frame; short-circuiting leaves the web request queued and fires it late
            var keyTapped = _hotbarKeyInput.TryGetTappedSlot(out var keySlot);
            var webTapped = _clientHotbarDatastore.TryConsumeSelectRequest(out var webSlot);
            if (!keyTapped && !webTapped)
            {
                slot = default;
                return false;
            }

            // 同フレーム競合は手元の数字キーを優先する
            // A same-frame conflict resolves in favor of the local digit key
            slot = keyTapped ? keySlot : webSlot;

            var targetId = _clientHotbarDatastore.Assignments[slot];
            if (targetId == Guid.Empty) return true;

            _placementTargetResolver.TryResolve(targetId, out target);
            return true;
        }

        // 入力読取と実時刻というUnity依存だけをここで解決し、判別は状態機械へ値でプッシュする
        // Only the Unity-dependent input read and wall clock live here; the classification is pushed into the state machine as values
        private void PollKeyInput()
        {
            var rawValue = InputManager.UI.HotBar.ReadValue<int>();
            _hotbarKeyInput.ManualUpdate(rawValue == 0 ? (int?)null : rawValue - 1, Time.unscaledTime);
        }
    }
}
