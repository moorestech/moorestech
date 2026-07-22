using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Player;
using Client.Game.InGame.SoundEffect;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Common.Debug;
using Core.Master;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;
using static Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlaceSystemUtil;
using static Client.Game.DebugConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     マウスで地面をクリックしたときに発生するイベント
    /// </summary>
    public class CommonBlockPlaceSystem : PlaceSystemBase<BlockPlacementTarget>
    {
        private const float PlaceableMaxDistance = 100f;
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private readonly ILocalPlayerInventory _localPlayerInventory;
        private readonly Camera _mainCamera;
        private readonly CommonBlockPlacePointCalculator _blockPlacePointCalculator;
        private readonly ElectricWireAutoConnectPreview _autoConnectPreview;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;

        private BlockDirection _currentBlockDirection = BlockDirection.North;
        private Vector3Int? _clickStartPosition;
        private int _clickStartHeightOffset;
        private List<PlaceInfo> _currentPlaceInfos = new();
        private BlockId? _previousSelectedBlockId;
        private bool _isReplaceDrag;

        private int _heightOffset;

        public CommonBlockPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory localPlayerInventory)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _localPlayerInventory = localPlayerInventory;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _blockPlacePointCalculator = new CommonBlockPlacePointCalculator(blockGameObjectDataStore);
            _autoConnectPreview = new ElectricWireAutoConnectPreview(mainCamera, blockGameObjectDataStore);
        }
        
        public override void Enable()
        {
            _clickStartHeightOffset = -1;
        }
        public override void Disable()
        {
            // デバッグモード時はプレビューを維持
            // Keep preview in debug mode
            if (!DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey))
            {
                _previewBlockController.SetActive(false);
                _autoConnectPreview.Hide();
            }

            // 連続設置状態をリセット
            _clickStartPosition = null;
            _isReplaceDrag = false;
            _currentPlaceInfos.Clear();
        }
        
        protected override void ManualUpdate(BlockPlacementTarget target, bool isSelectionChanged)
        {
            ApplyPickedDirection();
            UpdateHeightOffset();
            BlockDirectionControl();
            GroundClickControl(target);

            #region Internal

            void ApplyPickedDirection()
            {
                // スポイトでピックした向きを選択変化時に反映する
                // Apply the eyedropped block direction when the selection changes
                if (isSelectionChanged && target.PickedDirection.HasValue) _currentBlockDirection = target.PickedDirection.Value;
            }

            void UpdateHeightOffset()
            {
                if (HybridInput.GetKeyDown(KeyCode.Q)) //TODO InputManagerに移す
                    _heightOffset--;
                else if (HybridInput.GetKeyDown(KeyCode.E)) _heightOffset++;
            }
            
            void BlockDirectionControl()
            {
                if (InputManager.Playable.BlockPlaceRotation.GetKeyDown)
                    // 東西南北の向きを変更する
                    _currentBlockDirection = _currentBlockDirection.HorizonRotation();
                
                //TODo シフトはインプットマネージャーに入れる
                if (HybridInput.GetKey(KeyCode.LeftShift) && InputManager.Playable.BlockPlaceRotation.GetKeyDown)
                    _currentBlockDirection = _currentBlockDirection.VerticalRotation();
            }
            
            #endregion
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

            // ブロック設置用のrayが当たっているか、当たっていたら設置位置を取得する
            var holdingBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(target.BlockId);
            if (!TryGetRayHitBlockPosition(_mainCamera, _heightOffset, _currentBlockDirection, holdingBlockMaster, out var placePoint, out var boundingBoxSurface)) { _autoConnectPreview.Hide(); return; }

            // 設置可能な距離かどうか
            if (!IsBlockPlaceableDistance(PlaceableMaxDistance)) { _autoConnectPreview.Hide(); return; }
            
            _previewBlockController.SetActive(true);
            
            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi())
            {
                // 天面レイヒットで浮いた起点を直下の既存ファミリーブロックへ引き戻して判定する（1x1のみ対象）
                // Resolve the ray-floated start cell down to the family block below before judging (1x1 blocks only)
                var startCell = ReplacePlacementJudge.ResolveReplaceCell(_blockGameObjectDataStore, placePoint);
                _isReplaceDrag = holdingBlockMaster.BlockSize == Vector3Int.one
                                 && ReplacePlacementJudge.IsReplaceDragStart(_blockGameObjectDataStore, target.BlockId, startCell);

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

            // 地面フィルタ後にアイテム数チェック（地面に埋まったブロックがアイテム枠を消費しないようにする）
            // Check item count after ground filtering (so ground-blocked cells don't consume item quota)
            MarkInsufficientItemPreviewsAsNotPlaceable();

            // 各セルの自動接続を評価し表示更新
            // Evaluate auto-connect per cell and update the preview
            var wirePlaceable = _autoConnectPreview.ApplyAutoConnect(_currentPlaceInfos, target.BlockId, _currentBlockDirection, _localPlayerInventory, placePoint);

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
                var startPoint = _clickStartPosition ?? placePoint;

                // リプレースドラッグ中は終点も直下の既存ファミリーブロックへ引き戻す（通常設置は生placePoint）
                // During a replace drag, also pull the endpoint down to the family block below (normal placement keeps raw placePoint)
                var endPoint = _isReplaceDrag ? ReplacePlacementJudge.ResolveReplaceCell(_blockGameObjectDataStore, placePoint) : placePoint;
                _currentPlaceInfos = _blockPlacePointCalculator.CalculatePoint(startPoint, endPoint, _currentBlockDirection, holdingBlockMaster);
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

                // マウスを離したので連続設置状態は解除する（設置有無に関わらず）
                // Clear the continuous-placement state on mouse release (regardless of whether we place)
                _heightOffset = _clickStartHeightOffset;
                _clickStartPosition = null;

                // UI上か電線不足なら設置しない
                // Do not place over UI or without enough wire
                if (UiPointerHitTest.IsPointerOverAnyUi() || !wirePlaceable) return;

                SendPlaceBlockProtocol(_currentPlaceInfos.Where(info => info.Placeable).ToList());

                // 設置でワールドとインベントリが変わるため、自動接続の評価キャッシュを破棄する
                // Placement changes the world and inventory, so drop the auto-connect evaluation cache
                _autoConnectPreview.Hide();
            }

            void MarkInsufficientItemPreviewsAsNotPlaceable()
            {
                // 無料設置モードでは所持数による制限をかけない
                // In free placement mode, do not limit by held item count
                if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)) return;

                // 建設コストで賄えるセル数まで設置可にする
                // Allow placement up to the affordable cell count
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(target.BlockId);
                var affordableCellCount = ConstructionCostPreviewCalculator.CalculateAffordableCellCount(blockMaster.RequiredItems, _localPlayerInventory);

                var placeableCount = 0;
                for (var i = 0; i < _currentPlaceInfos.Count; i++)
                {
                    if (!_currentPlaceInfos[i].Placeable) continue;
                    placeableCount++;
                    if (placeableCount > affordableCellCount)
                    {
                        _currentPlaceInfos[i].Placeable = false;
                    }
                }
            }

            #endregion
        }
    }
}
