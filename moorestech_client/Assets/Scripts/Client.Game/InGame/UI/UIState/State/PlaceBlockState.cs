using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using Client.Game.InGame.UI.UIState.State.Hotbar;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.Skit;
using Client.Input;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class PlaceBlockState : IUIState, IApplicationFocusRestorer
    {
        private readonly SkitManager _skitManager;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly List<IDisposable> _blockPlacedDisposable = new();
        private readonly PlaceSystemStateController _placeSystemStateController;
        private readonly PlacementTargetPickService _placementTargetPickService;
        private readonly UiStateCameraPolicyService _cameraPolicyService;
        private readonly BuildUndoService _buildUndoService;
        private readonly IMapVeinRangeView _mapVeinRangeView;
        private readonly HotbarTapInputService _hotbarInputService;
        private readonly ReactiveProperty<int> _placementHeight = new(0);

        public IObservable<int> OnPlacementHeightChanged => _placementHeight;
        public int GetPlacementHeight() => _placementHeight.Value;

        public PlaceBlockState(
            SkitManager skitManager,
            BlockGameObjectDataStore blockGameObjectDataStore,
            PlaceSystemStateController placeSystemStateController,
            PlacementTargetPickService placementTargetPickService,
            UiStateCameraPolicyService cameraPolicyService,
            BuildUndoService buildUndoService,
            IMapVeinRangeView mapVeinRangeView,
            VeinRestrictedPlacementState veinRestrictedPlacementState,
            HotbarTapInputService hotbarInputService)
        {
            _skitManager = skitManager;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _placeSystemStateController = placeSystemStateController;
            _placementTargetPickService = placementTargetPickService;
            _cameraPolicyService = cameraPolicyService;
            _buildUndoService = buildUndoService;
            _mapVeinRangeView = mapVeinRangeView;
            _hotbarInputService = hotbarInputService;

            // 設置対象か制限が変わった時だけ表示種別と強調鉱脈をプッシュする。毎フレームの再導出はしない
            // Push the vein kind and the highlighted vein only when the target or the restriction changes; never re-derive per frame
            var veinViewPusher = new PlacementVeinViewPusher(mapVeinRangeView, veinRestrictedPlacementState);
            _placeSystemStateController.OnTargetChanged.Subscribe(veinViewPusher.Push);
            veinRestrictedPlacementState.OnChanged.Subscribe(_ => veinViewPusher.Push(_placeSystemStateController.CurrentTarget));
        }

        public void OnEnter(UITransitContext context)
        {
            // 他UIState滞在中は数字キーがpollされないため、復帰直後の古い押下状態を破棄する
            // Digit keys aren't polled while another UIState is active, so discard any stale press state on return
            _hotbarInputService.ResetKeyState();

            _placementHeight.Value = 0;
            // 遷移payloadから設置対象と由来を1組で受け取り所有者へ渡す（無ければEmptyに落ちる）
            // Take the placement target and its origin as one pair from the transition payload and hand them to the owner (falls back to Empty when absent)
            if (context.TryGetContext<PlacementSelection>(out var selection)) _placeSystemStateController.SetTarget(selection.Target, selection.Origin);

            // 視点別カーソル/回転ポリシーを適用
            // Apply the per-view-mode cursor/rotation policy
            _cameraPolicyService.EnterBuildMode();

            // ここが重くなったら近いブロックだけプレビューをオンにするなどする
            foreach (var blockGameObject in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                blockGameObject.EnablePreviewOnlyObjects(true, true);
            }
            _blockPlacedDisposable.Add(_blockGameObjectDataStore.OnBlockPlaced.Subscribe(OnPlaceBlock));

            #region Internal

            void OnPlaceBlock(BlockGameObject blockGameObject)
            {
                blockGameObject.EnablePreviewOnlyObjects(true, false);

                _blockPlacedDisposable.Add(blockGameObject.OnFinishedPlaceAnimation.Subscribe(_ =>
                {
                    blockGameObject.EnablePreviewOnlyObjects(true, true);
                }));
            }

            #endregion
        }

        public UITransitContext GetNextUpdate()
        {
            if (_skitManager.IsPlayingSkit) return new UITransitContext(UIStateEnum.Story);

            // TabはOpenInventoryと同キーだが、配置モード中はビルドメニュー再表示を優先する
            // Tab shares the OpenInventory binding, but reopening the build menu takes precedence while placing
            if (HybridInput.GetKeyDown(KeyCode.Tab)) return new UITransitContext(UIStateEnum.BuildMenu);
            if (InputManager.UI.BlockDelete.GetKeyDown) return new UITransitContext(UIStateEnum.DeleteBar);
            if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.GameScreen);

            // キー/Web選択を共通3分岐へ
            // Route a digit-key or web-originated tap into the shared 3-way branch (same slot / different slot / empty slot)
            var hotbarTapOutcome = _hotbarInputService.ResolveBuildModeTap(out var hotbarTapTransit);
            switch (hotbarTapOutcome)
            {
                case HotbarTapOutcome.ExitBuildMode: return hotbarTapTransit;
                // 持ち替えは同一画面で完結するため遷移しない
                // A swap completes within this screen, so no transition is returned
                case HotbarTapOutcome.SwappedTarget:
                case HotbarTapOutcome.None: break;
                // 建築モード中に建築モードへ入る分類は起こりえない
                // Entering build mode cannot be produced while already in build mode
                case HotbarTapOutcome.EnterBuildMode:
                default: throw new ArgumentOutOfRangeException(nameof(hotbarTapOutcome), hotbarTapOutcome, null);
            }

            // 長押しで設置対象を枠へ割当
            // A long press assigns the current placement target to that slot
            _hotbarInputService.ApplyLongPressAssign();

            // TPSのみ右ドラッグで設置照準回転
            // TPS rotates the placement aim only during right-drag
            _cameraPolicyService.UpdateRotationInput();
            if (_placementTargetPickService.TryPickTargetUnderCursor(out var pickedTarget)) _placeSystemStateController.SetTarget(pickedTarget, PlacementOrigin.NonHotbar);

            _placeSystemStateController.ManualUpdate();

            // カメラ追従の距離カリングだけを駆動する。表示種別は設置対象の購読がプッシュ済み
            // Drive only the camera-following distance culling; the vein kind was already pushed by the target subscription
            _mapVeinRangeView.ManualUpdate();

            // Ctrl+Z判定はサービス内部
            // Ctrl+Z detection lives inside the service
            _buildUndoService.ManualUpdate();

            // 実設置系と同じ入力でHUDの高さ表示を更新する
            // Update the HUD height from the same input used by placement systems
            if (HybridInput.GetKeyDown(KeyCode.Q)) _placementHeight.Value--;
            else if (HybridInput.GetKeyDown(KeyCode.E)) _placementHeight.Value++;

            return null;
        }

        public void OnExit()
        {
            _cameraPolicyService.ExitToNeutral();

            // 設置対象と由来枠はここで同時に落ちる。由来枠だけの明示リセットは持たない
            // 対象がnullになる通知で鉱脈範囲表示も畳まれる（表示種別のプッシュ元は購読1本に絞る）
            // The placement target and its origin drop together here; no separate origin reset is needed
            // The null-target notification also folds the vein range view (the vein kind has a single push source)
            _placeSystemStateController.Disable();

            // 離脱時点の押下状態を持ち越さない。復帰後の誤長押し判定を防ぐ
            // Discard the press state as of this exit so a later re-entry can't misfire a long press
            _hotbarInputService.ResetKeyState();

            foreach (var blockGameObject in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                blockGameObject.EnablePreviewOnlyObjects(false, false);
            }

            _blockPlacedDisposable.ForEach(d => d.Dispose());
            _blockPlacedDisposable.Clear();
        }

        public void RestoreAfterApplicationFocus()
        {
            _cameraPolicyService.RestoreAfterApplicationFocus();
        }

        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return PlaceBlockStateHints.Hints;
        }
    }

    internal static class PlaceBlockStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.SelectBlock),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.B, LocalizationKeys.Ui.KeyHint.Text.ExitPlaceMode),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.G, LocalizationKeys.Ui.KeyHint.Text.DeleteMode),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.R, LocalizationKeys.Ui.KeyHint.Text.Rotate),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Q, LocalizationKeys.Ui.KeyHint.Text.LowerHeight),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.E, LocalizationKeys.Ui.KeyHint.Text.RaiseHeight),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.MiddleClick, LocalizationKeys.Ui.KeyHint.Text.PickPlacedObject),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.CtrlZ, LocalizationKeys.Ui.KeyHint.Text.Undo),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.V, LocalizationKeys.Ui.KeyHint.Text.ToggleView),
        };
    }
}
