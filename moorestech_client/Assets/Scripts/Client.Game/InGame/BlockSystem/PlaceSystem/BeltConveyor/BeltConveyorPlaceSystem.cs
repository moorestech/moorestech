using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Height;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Inventory.Main;
using Common.Debug;
using Game.Block.Interface;
using Game.Construction;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;
using static Client.Game.DebugConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor
{
    /// <summary>
    ///     ベルトの2クリック設置。1回目で始点を決め、カーソルのセルまでの最短経路を毎フレーム出し、2回目で置く
    ///     Two-click belt placement: the first click fixes the start, the shortest route to the cursor cell is previewed every frame, the second click places it
    /// </summary>
    public class BeltConveyorPlaceSystem : PlaceSystemBase<BlockPlacementTarget>
    {
        private readonly Camera _mainCamera;
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private readonly BlockGameObjectDataStore _blocks;
        private readonly ILocalPlayerInventory _localPlayerInventory;
        private readonly ConstructionWalletQuery _constructionWalletQuery;
        private readonly PlacementHeightOffset _placementHeightOffset;
        private readonly BeltRouteWorldScanner _scanner;

        private BeltConveyorHoldingBlock _holding;
        private BlockDirection _currentDirection = BlockDirection.North;
        private bool _branchSideIsRight = true;
        private BeltRouteStepPriority _priority = BeltRouteStepPriority.Default;
        private BeltRouteSession _session;
        private bool _obstaclesDirty;

        public BeltConveyorPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory localPlayerInventory, ConstructionWalletQuery constructionWalletQuery, PlacementHeightOffset placementHeightOffset)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _blocks = blockGameObjectDataStore;
            _localPlayerInventory = localPlayerInventory;
            _constructionWalletQuery = constructionWalletQuery;
            _placementHeightOffset = placementHeightOffset;
            _scanner = new BeltRouteWorldScanner(blockGameObjectDataStore);

            // 箱と重なる設置・撤去があれば次フレームで占有を作り直す
            // A placement or removal overlapping the box triggers an occupancy rebuild on the next frame
            blockGameObjectDataStore.OnBlockPlaced.Subscribe(block => MarkObstaclesDirty(block.BlockPosInfo.OriginalPos));
            blockGameObjectDataStore.OnBlockRemoved.Subscribe(MarkObstaclesDirty);
        }

        // Q/Eで動かす設置高さを読む系
        // A system that reads the placement height moved by Q/E
        public override bool UsesPlacementHeight => true;

        // 持ち替え直後は北向きから始める。プレイテストの操作も同じ前提で回す
        // A fresh selection starts facing north; playtest operations rely on the same premise
        public override void Enable()
        {
            _session = null;
            _currentDirection = BlockDirection.North;
            _branchSideIsRight = true;
            _priority = BeltRouteStepPriority.Default;
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
            RefreshObstaclesIfDirty();

            if (_session == null) UpdateStartSelection(feedback);
            else UpdateRouting(feedback);
        }

        private void MarkObstaclesDirty(Vector3Int blockOrigin)
        {
            if (_session != null && _session.IsNearBox(blockOrigin)) _obstaclesDirty = true;
        }

        private void RefreshObstaclesIfDirty()
        {
            if (!_obstaclesDirty) return;
            _obstaclesDirty = false;
            if (_session == null) return;
            _session.RebuildObstacles();
            if (_session.IsStartUsable(_blocks)) return;

            Debug.Log($"[BeltRoute] start at {_session.StartCell} became unusable after a block change; back to start selection");
            _session = null;
        }

        private void UpdateStartSelection(PlacementFeedback feedback)
        {
            if (!BeltRouteCursorHit.TryResolve(_mainCamera, _placementHeightOffset.Value, _currentDirection, _holding.BlockMaster, feedback, out var hit)) return;

            // Ctrlを押したまま既存ベルトを狙うと分岐の始点候補になる
            // Aiming at an existing belt with Ctrl held makes it a branch start candidate
            if (BeltRouteKeyInput.IsBranchModifierHeld && hit.HitBlock != null && BeltRouteBlockEdgeScanner.IsBelt(hit.HitBlock.BlockMasterElement))
            {
                UpdateBranchCandidate(hit.HitBlock, feedback);
                return;
            }

            if (BeltRouteKeyInput.IsRotatePressed) _currentDirection = _currentDirection.HorizonRotation();
            if (!PlaceSystemUtil.IsPlaceableFromPlayer(hit.Cell)) { feedback.AddTooFar(); return; }

            var reason = BeltRouteCellCheck.JudgeInWorld(_blocks, hit.Cell);
            var placeable = reason == BeltRouteCellBlockReason.None;
            if (!placeable) feedback.Add(BeltRouteTooltipKey.CellLine(reason));
            ShowPreview(new List<PlaceInfo> { BeltRoutePlaceInfoBuilder.Single(hit.Cell, _currentDirection, _holding.BlockId, placeable) });

            if (placeable && BeltRouteKeyInput.IsConfirmPressed) _session = BeltRouteSession.CreateAtCell(hit.Cell, _currentDirection, _scanner);
        }

        private void UpdateBranchCandidate(BlockGameObject belt, PlacementFeedback feedback)
        {
            if (BeltRouteKeyInput.IsRotatePressed) _branchSideIsRight = !_branchSideIsRight;
            if (!PlaceSystemUtil.IsPlaceableFromPlayer(belt.BlockPosInfo.OriginalPos)) { feedback.AddTooFar(); return; }

            var reason = BeltRouteBranchOrigin.Judge(_blocks, belt, _holding, _branchSideIsRight, out var branch);
            if (reason != BeltRouteBranchBlockReason.None)
            {
                feedback.Add(BeltRouteTooltipKey.BranchLine(reason));
                ShowPreview(new List<PlaceInfo> { BeltRoutePlaceInfoBuilder.Single(belt.BlockPosInfo.OriginalPos, belt.BlockPosInfo.BlockDirection, _holding.BlockId, false) });
                return;
            }

            // 分岐器と、横の出口の先に出る最初のセルを候補として見せる
            // Show the splitter and the first cell beyond the side exit as the candidate
            ShowPreview(new List<PlaceInfo> { BeltRoutePlaceInfoBuilder.SplitterInfo(branch, true), BeltRoutePlaceInfoBuilder.Single(branch.Cell + BeltRouteDirection.Forward(branch.SideDirection), branch.SideDirection, _holding.BlockId, true) });
            if (!BeltRouteKeyInput.IsConfirmPressed) return;

            _session = BeltRouteSession.CreateBranch(branch, _scanner);
            _currentDirection = branch.SideDirection;
        }

        private void UpdateRouting(PlacementFeedback feedback)
        {
            if (BeltRouteKeyInput.IsRotatePressed) _currentDirection = _currentDirection.HorizonRotation();
            if (BeltRouteKeyInput.IsCyclePriorityPressed) _priority = _priority.Next();
            if (!BeltRouteCursorHit.TryResolve(_mainCamera, _placementHeightOffset.Value, _currentDirection, _holding.BlockMaster, feedback, out var hit)) return;
            if (!PlaceSystemUtil.IsPlaceableFromPlayer(hit.Cell)) { feedback.AddTooFar(); return; }

            var infos = BeltRouteRoutingFrame.Resolve(_session, hit, _priority, _holding, feedback, out var placeable);
            if (placeable) placeable = BeltRouteCostGate.Apply(infos, _constructionWalletQuery, _localPlayerInventory, feedback);
            ShowPreview(infos);
            if (!placeable || !BeltRouteKeyInput.IsConfirmPressed) return;

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
