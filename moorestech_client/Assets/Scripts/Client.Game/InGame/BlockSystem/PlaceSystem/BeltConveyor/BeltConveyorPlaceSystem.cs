using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Common.Debug;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Server.Protocol.PacketResponse;
using UnityEngine;
using UnityEngine.EventSystems;
using static Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlaceSystemUtil;
using static Client.Game.DebugConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor
{
    /// <summary>
    /// ベルトコンベアファミリー専用の設置システム（1マス刻みの経路を長尺バリアントへ分解して設置する）
    /// Dedicated placement system for belt-conveyor families (decomposes the grid-step path into length variants)
    /// </summary>
    public class BeltConveyorPlaceSystem : IPlaceSystem
    {
        private const float PlaceableMaxDistance = 100f;
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private readonly ILocalPlayerInventory _localPlayerInventory;
        private readonly Camera _mainCamera;
        private readonly BeltConveyorPlacePointCalculator _blockPlacePointCalculator;

        private BlockDirection _currentBlockDirection = BlockDirection.North;
        private Vector3Int? _clickStartPosition;
        private int _clickStartHeightOffset;
        private bool? _isStartZDirection;
        private List<PlaceInfo> _currentPlaceInfos = new();
        private BlockId? _previousSelectedBlockId;

        private int _heightOffset;

        public BeltConveyorPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory localPlayerInventory)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _localPlayerInventory = localPlayerInventory;
            _blockPlacePointCalculator = new BeltConveyorPlacePointCalculator(blockGameObjectDataStore);
        }

        public void Enable() => _clickStartHeightOffset = -1;

        public void Disable()
        {
            // デバッグモード時はプレビューを維持
            // Keep preview in debug mode
            if (!DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey)) _previewBlockController.SetActive(false);

            // 連続設置状態をリセット
            _clickStartPosition = null;
            _isStartZDirection = null;
            _currentPlaceInfos.Clear();
        }

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            _heightOffset = BeltConveyorInputControl.AdjustHeightOffset(_heightOffset);
            _currentBlockDirection = BeltConveyorInputControl.RotateDirection(_currentBlockDirection);
            GroundClickControl(context);
        }

        private void GroundClickControl(PlaceSystemUpdateContext context)
        {
            // placeModeスイッチ経由(HoldingItemId駆動)で到達した場合はSelectedBlockIdが無いことがある
            // SelectedBlockId can be absent when reached via the placeMode switch (driven by HoldingItemId)
            if (!context.SelectedBlockId.HasValue) return;

            // ビルドメニューの選択ブロックが変わったら連続設置状態をリセット
            // Reset the continuous placement state when the build-menu selected block changes
            if (_previousSelectedBlockId != context.SelectedBlockId)
            {
                _clickStartPosition = null;
                _clickStartHeightOffset = _heightOffset;
            }
            _previousSelectedBlockId = context.SelectedBlockId;

            //基本はプレビュー非表示
            _previewBlockController.SetActive(false);

            // ファミリー定義を解決（代表・斜面・長尺バリアント）
            // Resolve the family definition (representative, slopes, length variants)
            BeltConveyorPlaceFamilyUtil.TryGetFamily(context.SelectedBlockId.Value, out var beltParam);
            var representativeBlockId = BeltConveyorPlaceFamilyUtil.GetRepresentativeBlockId(beltParam);
            var holdingBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(representativeBlockId);
            var variants = BeltConveyorPlaceFamilyUtil.GetStraightVariantsDesc(beltParam);
            var upBlockId = MasterHolder.BlockMaster.GetBlockId(beltParam.UpBlockGuid);
            var downBlockId = MasterHolder.BlockMaster.GetBlockId(beltParam.DownBlockGuid);

            // ブロック設置用のrayが当たっているか、当たっていたら設置位置を取得する
            if (!TryGetRayHitBlockPosition(_mainCamera, _heightOffset, _currentBlockDirection, holdingBlockMaster, out var placePoint, out _)) return;

            // 設置可能な距離かどうか
            if (!IsBlockPlaceableDistance(PlaceableMaxDistance)) return;

            _previewBlockController.SetActive(true);

            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !EventSystem.current.IsPointerOverGameObject())
            {
                _clickStartPosition = placePoint;
                _clickStartHeightOffset = _heightOffset;
            }

            //プレビュー表示と地面との接触を取得する
            //display preview and get collision with ground
            SetCurrentPlaceInfo();

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
                List<PlaceInfo> cellInfos;
                if (_clickStartPosition.HasValue)
                {
                    if (_clickStartPosition.Value == placePoint)
                    {
                        _isStartZDirection = null;
                    }
                    else if (!_isStartZDirection.HasValue)
                    {
                        _isStartZDirection = Mathf.Abs(placePoint.z - _clickStartPosition.Value.z) > Mathf.Abs(placePoint.x - _clickStartPosition.Value.x);
                    }

                    cellInfos = _blockPlacePointCalculator.CalculatePoint(_clickStartPosition.Value, placePoint, _isStartZDirection ?? true, _currentBlockDirection, holdingBlockMaster);
                }
                else
                {
                    _isStartZDirection = null;
                    cellInfos = _blockPlacePointCalculator.CalculatePoint(placePoint, placePoint, true, _currentBlockDirection, holdingBlockMaster);
                }

                // セル列を長尺バリアント・斜面エンティティ列へ分解
                // Decompose cells into length-variant and slope entities
                _currentPlaceInfos = BeltConveyorRunDecomposer.Decompose(cellInfos, variants, upBlockId, downBlockId);
            }

            void PlaceBlock()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return;

                // デバッグモード時は送信しない
                // Skip sending in debug mode
                if (DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey)) return;

                // マウスを離したので連続設置状態は解除する（設置有無に関わらず）
                // Clear the continuous-placement state on mouse release (regardless of whether we place)
                _heightOffset = _clickStartHeightOffset;
                _clickStartPosition = null;

                SendPlaceBlockProtocol(_currentPlaceInfos.Where(info => info.Placeable).ToList());
            }

            #endregion
        }
    }
}
