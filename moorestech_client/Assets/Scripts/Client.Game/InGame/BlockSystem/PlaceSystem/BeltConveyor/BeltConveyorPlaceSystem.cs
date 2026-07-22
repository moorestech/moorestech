using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Control;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Common.Debug;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Server.Protocol.PacketResponse;
using UnityEngine;
using static Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlaceSystemUtil;
using static Client.Game.DebugConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor
{
    /// <summary>
    /// ベルトコンベアファミリー専用の1セル単位設置システム
    /// Dedicated per-cell placement system for belt-conveyor families
    /// </summary>
    public class BeltConveyorPlaceSystem : PlaceSystemBase<BlockPlacementTarget>
    {
        private const float PlaceableMaxDistance = 100f;
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private readonly ILocalPlayerInventory _localPlayerInventory;
        private readonly Camera _mainCamera;
        private readonly BeltConveyorPlacePointCalculator _blockPlacePointCalculator;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;

        private BlockDirection _currentBlockDirection = BlockDirection.North;
        private Vector3Int? _clickStartPosition;
        private int _clickStartHeightOffset;
        private bool? _isStartZDirection;
        private List<PlaceInfo> _currentPlaceInfos = new();
        private BlockId? _previousSelectedBlockId;
        private bool _isReplaceDrag;

        private int _heightOffset;

        public BeltConveyorPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory localPlayerInventory)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _localPlayerInventory = localPlayerInventory;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _blockPlacePointCalculator = new BeltConveyorPlacePointCalculator(blockGameObjectDataStore);
        }

        public override void Enable() => _clickStartHeightOffset = -1;

        public override void Disable()
        {
            // デバッグモード時はプレビューを維持
            // Keep preview in debug mode
            if (!DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey)) _previewBlockController.SetActive(false);

            // 連続設置状態をリセット
            _clickStartPosition = null;
            _isStartZDirection = null;
            _isReplaceDrag = false;
            _currentPlaceInfos.Clear();
        }

        protected override void ManualUpdate(BlockPlacementTarget target, bool isSelectionChanged)
        {
            _heightOffset = BeltConveyorInputControl.AdjustHeightOffset(_heightOffset);
            _currentBlockDirection = BeltConveyorInputControl.RotateDirection(_currentBlockDirection);
            GroundClickControl(target);
        }

        private void GroundClickControl(BlockPlacementTarget target)
        {
            // ビルドメニューの選択ブロックが変わったら連続設置状態をリセット
            // Reset the continuous placement state when the build-menu selected block changes
            if (_previousSelectedBlockId != target.BlockId)
            {
                _clickStartPosition = null;
                _clickStartHeightOffset = _heightOffset;
                _isReplaceDrag = false;
            }
            _previousSelectedBlockId = target.BlockId;

            //基本はプレビュー非表示
            _previewBlockController.SetActive(false);

            // ファミリー定義を解決し、非ファミリーブロックは対象外にする
            // Resolve the family definition and ignore non-family blocks
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(target.BlockId, out var family)) return;
            var holdingBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(family.StraightBlockId);

            // ブロック設置用のrayが当たっているか、当たっていたら設置位置を取得する
            if (!TryGetRayHitBlockPosition(_mainCamera, _heightOffset, _currentBlockDirection, holdingBlockMaster, out var placePoint, out _)) return;

            // 設置可能な距離かどうか
            if (!IsBlockPlaceableDistance(PlaceableMaxDistance)) return;

            _previewBlockController.SetActive(true);

            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi())
            {
                // 天面レイヒットで浮いた起点を直下の既存ファミリーブロックへ引き戻して判定する
                // Resolve the ray-floated start cell down to the family block below before judging
                var startCell = ReplacePlacementJudge.ResolveReplaceCell(_blockGameObjectDataStore, target.BlockId, placePoint);
                _isReplaceDrag = ReplacePlacementJudge.IsReplaceDragStart(_blockGameObjectDataStore, target.BlockId, startCell);

                // リプレースドラッグ時のみ解決後セルを起点にし、通常設置は従来通り生placePointを使う
                // Use the resolved cell only for replace drags; normal placement keeps the raw placePoint
                _clickStartPosition = _isReplaceDrag ? startCell : placePoint;
                _clickStartHeightOffset = _heightOffset;
            }

            //プレビュー表示と地面との接触を取得する
            //display preview and get collision with ground
            SetCurrentPlaceInfo();

            // リプレースドラッグ中は既存ファミリーブロック重なりセルを設置可へ復活させる
            // During a replace drag, revive cells overlapping an existing family block as placeable
            if (_isReplaceDrag) MarkReplaceCells();

            var blockGroundOverlapList = _previewBlockController.SetPreviewAndGroundDetect(_currentPlaceInfos, holdingBlockMaster);

            // 地面との接触でPlaceableを更新
            // Update placeable based on ground collision
            for (var i = 0; i < blockGroundOverlapList.Count; i++)
            {
                // 地面と接触していたら設置不可
                // if collision with ground, cannot place
                if (blockGroundOverlapList[i])
                {
                    _currentPlaceInfos[i].Placeable = false;
                }
            }

            // 地面フィルタ後にアイテム数チェック（地面に埋まったエンティティがアイテム枠を消費しないようにする）
            // Check item count after ground filtering (so ground-blocked entities don't consume item quota)
            BeltConveyorCostPreviewMarker.MarkInsufficientEntitiesAsNotPlaceable(_currentPlaceInfos, _localPlayerInventory);

            // 最終的なPlaceable状態でプレビュー色を更新
            // Update preview colors based on the final Placeable state
            _previewBlockController.UpdatePlaceableColors(_currentPlaceInfos);

            // 設置するブロックをサーバーに送信
            // send block place info to server
            PlaceBlock();

            #region Internal

            bool IsBlockPlaceableDistance(float maxDistance)
            {
                var placePosition = (Vector3)placePoint;
                var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;

                return Vector3.Distance(playerPosition, placePosition) <= maxDistance;
            }

            void SetCurrentPlaceInfo()
            {
                // リプレースドラッグ中は終点も直下の既存ファミリーブロックへ引き戻す（通常設置は生placePoint）
                // During a replace drag, also pull the endpoint down to the family block below (normal placement keeps raw placePoint)
                var endPoint = _isReplaceDrag ? ReplacePlacementJudge.ResolveReplaceCell(_blockGameObjectDataStore, target.BlockId, placePoint) : placePoint;

                List<PlaceInfo> cellInfos;
                if (_clickStartPosition.HasValue)
                {
                    if (_clickStartPosition.Value == endPoint)
                    {
                        _isStartZDirection = null;
                    }
                    else if (!_isStartZDirection.HasValue)
                    {
                        _isStartZDirection = Mathf.Abs(endPoint.z - _clickStartPosition.Value.z) > Mathf.Abs(endPoint.x - _clickStartPosition.Value.x);
                    }

                    cellInfos = _blockPlacePointCalculator.CalculatePoint(_clickStartPosition.Value, endPoint, _isStartZDirection ?? true, _currentBlockDirection, holdingBlockMaster, _isReplaceDrag);
                }
                else
                {
                    _isStartZDirection = null;
                    cellInfos = _blockPlacePointCalculator.CalculatePoint(endPoint, endPoint, true, _currentBlockDirection, holdingBlockMaster, _isReplaceDrag);
                }

                // セル列へ直線・坂ブロックを1対1で割り当てる
                // Assign straight and slope blocks to cells one-to-one
                _currentPlaceInfos = BeltConveyorCellBlockResolver.Resolve(cellInfos, family);
            }

            void MarkReplaceCells()
            {
                // 既存ブロック重なりが原因で不可のセルだけをリプレース判定にかける
                // Only run replace judgement on cells blocked by an overlapping existing block
                foreach (var info in _currentPlaceInfos)
                {
                    if (info.Placeable) continue;
                    ReplacePlacementJudge.TryMarkReplace(_blockGameObjectDataStore, info);
                }
            }

            void PlaceBlock()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return;

                // デバッグモード時は送信しない
                // Skip sending in debug mode
                if (DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey)) return;

                // 押下未登録の解放は無視する（ビルドメニュー選択クリックの解放がPlaceBlock遷移直後に漏れて
                // Enableのセンチネル-1を_heightOffsetへ書き込み、以後の設置が1段沈むのを防ぐ）
                // Ignore releases without a registered press (the build-menu selection click's release can leak in right
                // after entering PlaceBlock and write Enable's -1 sentinel into _heightOffset, sinking later placements)
                if (!_clickStartPosition.HasValue) return;

                // マウスを離したので連続設置状態は解除する（設置有無に関わらず）
                // Clear the continuous-placement state on mouse release (regardless of whether we place)
                _heightOffset = _clickStartHeightOffset;
                _clickStartPosition = null;

                if (UiPointerHitTest.IsPointerOverAnyUi()) return;
                SendPlaceBlockProtocol(_currentPlaceInfos.Where(info => info.Placeable).ToList());
            }

            #endregion
        }
    }
}
