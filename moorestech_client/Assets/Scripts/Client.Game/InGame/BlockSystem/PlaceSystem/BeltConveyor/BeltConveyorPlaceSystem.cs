using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Height;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Tooltip;
using Common.Debug;
using Game.Block.Interface;
using Game.Construction;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse;
using UnityEngine;
using static Client.Game.DebugConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor
{
    /// <summary>
    ///     2クリックでベルト設置。経路を毎フレーム表示
    ///     Two-click belt placement; the route is previewed every frame
    ///     世界は始点選び中に改訂番号で作り直し、始点確定後は確定の瞬間にだけ読み直して検査する（ADR 0100）
    ///     The world is rebuilt by revision during start selection; after the start is fixed it is re-read only at confirm to check (ADR 0100)
    /// </summary>
    public class BeltConveyorPlaceSystem : PlaceSystemBase<BlockPlacementTarget>
    {
        private readonly Camera _mainCamera;
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private readonly BlockGameObjectDataStore _blocks;
        private readonly ILocalPlayerInventory _localPlayerInventory;
        private readonly ConstructionWalletQuery _constructionWalletQuery;
        private readonly PlacementHeightOffset _placementHeightOffset;
        private readonly BeltRouteLiveWorld _world;
        private ulong _worldRevision;
        // 理由行は毎フレーム消えるので、始点選びに戻ってから 2 秒出し続ける（UI の表示時間なので実時間）
        // Tooltip lines clear every frame, so keep showing it for 2 s after returning to start selection (UI display time, so real time)
        private const float WorldChangedNoticeSeconds = 2f;
        private float _worldChangedNoticeUntil;

        private BeltConveyorHoldingBlock _holding;
        private BlockDirection _currentDirection = BlockDirection.North;
        private bool _branchUseAlternateExit;
        private BeltRouteSession _session;

        public BeltConveyorPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory localPlayerInventory, ConstructionWalletQuery constructionWalletQuery, PlacementHeightOffset placementHeightOffset)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _blocks = blockGameObjectDataStore;
            _localPlayerInventory = localPlayerInventory;
            _constructionWalletQuery = constructionWalletQuery;
            _placementHeightOffset = placementHeightOffset;
            _world = new BeltRouteLiveWorld(blockGameObjectDataStore);
            _worldRevision = blockGameObjectDataStore.OccupancyRevision;
        }

        // Q/Eで動かす設置高さを読む系
        // A system that reads the placement height moved by Q/E
        public override bool UsesPlacementHeight => true;

        // 持ち替え直後は北向きから始める。プレイテストの操作も同じ前提で回す
        // A fresh selection starts facing north; playtest operations rely on the same premise
        public override void Enable()
        {
            _session = null;
            _worldChangedNoticeUntil = 0;
            _currentDirection = BlockDirection.North;
            _branchUseAlternateExit = false;
        }

        // 右短押し/Escは始点だけ解除し、建築モードには留まる
        // A right short press or Esc drops only the start and stays in build mode
        public override bool TryCancelInProgressOperation()
        {
            if (_session == null) return false;
            _session = null;
            return true;
        }

        public override void Disable()
        {
            // デバッグモード時はプレビューを維持
            // Keep preview in debug mode
            if (!DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey)) _previewBlockController.SetActive(false);
            _session = null;
        }

        protected override void ManualUpdate(BlockPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)
        {
            // 手持ちはブロックIDが変わったときだけ解決し直す。持ち替えは始点の選び直しから
            // The held family is re-resolved only when the block id changes; a switch restarts from start selection
            if (isSelectionChanged || _holding == null || _holding.SelectedBlockId != target.BlockId)
            {
                _holding = BeltConveyorHoldingBlock.Resolve(target.BlockId);
                _currentDirection = target.ResolveDirectionOnSelection(_currentDirection, isSelectionChanged);
                _session = null;
            }
            PlacementHeightKeyInput.Apply(_placementHeightOffset);
            _previewBlockController.SetActive(false);
            if (_session == null) UpdateStartSelection(feedback);
            else UpdateRouting(feedback);
        }

        // 始点選び中の毎フレームと確定の直前だけ呼ぶ。始点確定後は作り直しも再探索もしない
        // Called every start-selection frame and right before confirm only; no rebuild or re-search after the start is fixed
        private bool RefreshWorldIfChanged()
        {
            var changed = _worldRevision != _blocks.OccupancyRevision;
            if (changed) _world.Rebuild();
            _worldRevision = _blocks.OccupancyRevision;
            return changed;
        }

        private void UpdateStartSelection(PlacementFeedback feedback)
        {
            RefreshWorldIfChanged();
            if (Time.unscaledTime < _worldChangedNoticeUntil) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltRouteWorldChanged));
            if (!BeltRouteCursorHit.TryResolve(_mainCamera, _placementHeightOffset.Value, _currentDirection, _holding.BlockMaster, _blocks, feedback, out var hit)) return;

            // Ctrlを押したまま既存ベルトを狙うと分岐の始点候補になる
            // Aiming at an existing belt with Ctrl held makes it a branch start candidate
            if (BeltRouteKeyInput.IsBranchModifierHeld && hit.HitBlock != null && BeltRouteBlockEdgeScanner.IsBelt(hit.HitBlock.BlockMasterElement))
            {
                UpdateBranchCandidate(hit.HitBlock, feedback);
                return;
            }

            if (BeltRouteKeyInput.IsRotatePressed) _currentDirection = _currentDirection.HorizonRotation();
            if (!PlaceSystemUtil.IsPlaceableFromPlayer(hit.Cell)) { feedback.AddTooFar(); return; }

            var reason = BeltRouteCellJudge.Judge(_world, hit.Cell);
            var placeable = reason == BeltRouteCellBlockReason.None;
            if (!placeable) feedback.Add(BeltRouteTooltipKey.CellLine(reason));
            ShowPreview(new List<PlaceInfo> { BeltRoutePlaceInfoBuilder.Single(hit.Cell, _currentDirection, _holding.BlockId, placeable) });

            if (placeable && BeltRouteKeyInput.IsConfirmPressed) _session = BeltRouteSession.CreateAtCell(hit.Cell, _currentDirection, _world);
        }

        private void UpdateBranchCandidate(BlockGameObject belt, PlacementFeedback feedback)
        {
            // R で既定の出口と残りを切替
            // R toggles between the default exit and the other one
            if (BeltRouteKeyInput.IsRotatePressed) _branchUseAlternateExit = !_branchUseAlternateExit;
            if (!PlaceSystemUtil.IsPlaceableFromPlayer(belt.BlockPosInfo.OriginalPos)) { feedback.AddTooFar(); return; }

            var reason = BeltRouteBranchOrigin.Judge(_blocks, belt, _holding, _branchUseAlternateExit, out var branch);
            if (reason != BeltRouteBranchBlockReason.None)
            {
                feedback.Add(BeltRouteTooltipKey.BranchLine(reason));
                ShowPreview(new List<PlaceInfo> { BeltRoutePlaceInfoBuilder.Single(belt.BlockPosInfo.OriginalPos, belt.BlockPosInfo.BlockDirection, _holding.BlockId, false) });
                return;
            }

            ShowPreview(BeltRoutePlaceInfoBuilder.BuildBranchCandidate(branch, _holding));
            if (!BeltRouteKeyInput.IsConfirmPressed) return;

            _session = BeltRouteSession.CreateBranch(branch, _world);
            _currentDirection = branch.ExitDirection;
        }

        private void UpdateRouting(PlacementFeedback feedback)
        {
            _worldChangedNoticeUntil = 0;
            if (BeltRouteKeyInput.IsRotatePressed) _currentDirection = _currentDirection.HorizonRotation();
            if (!BeltRouteCursorHit.TryResolve(_mainCamera, _placementHeightOffset.Value, _currentDirection, _holding.BlockMaster, _blocks, feedback, out var hit)) return;
            if (!PlaceSystemUtil.IsPlaceableFromPlayer(hit.Cell)) { feedback.AddTooFar(); return; }

            var infos = BeltRouteRoutingFrame.Resolve(_session, hit, _holding, feedback, out var placeable);
            if (placeable) placeable = BeltRouteCostGate.Apply(infos, _constructionWalletQuery, _localPlayerInventory, feedback);
            ShowPreview(infos);
            if (!placeable || !BeltRouteKeyInput.IsConfirmPressed) return;

            // 確定の瞬間に世界を読み直し、変わっていたら同じ終点で探索し直して照合し、置けなければ始点選びへ戻す
            // Re-read the world at confirm; if changed, re-search the same end and compare; if unplaceable, return to start selection
            var replanned = RefreshWorldIfChanged() ? BeltRouteRoutingFrame.ReplanForConfirm(_session, hit, _holding) : null;
            if (!BeltRouteConfirmCheck.IsStillPlaceable(_world, _blocks, _session, _holding, _branchUseAlternateExit, infos, replanned))
            {
                _worldChangedNoticeUntil = Time.unscaledTime + WorldChangedNoticeSeconds;
                _session = null;
                return;
            }

            if (_session.IsBranch) BeltRouteBranchReplaceSender.ReplaceAndPlace(_session.Branch, infos);
            else PlaceBlockProtocolSender.SendPlaceBlockProtocol(infos);
            _session = null;
        }

        private void ShowPreview(List<PlaceInfo> infos)
        {
            _previewBlockController.SetActive(true);
            _previewBlockController.SetPreview(infos, _holding.BlockMaster);
        }
    }
}
