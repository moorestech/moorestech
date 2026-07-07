using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Input;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using Server.Protocol.PacketResponse;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    /// <summary>
    ///     保存済みBPを回転・プレビュー付きでワールドへ貼り付ける設置システム
    ///     Placement system that pastes a saved blueprint with rotation and ghost preview
    /// </summary>
    public class BlueprintPasteSystem : IPlaceSystem
    {
        private readonly ClientBlueprintLibrary _library;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly Camera _mainCamera;
        private BlueprintPastePreviewController _previewController;

        private BlueprintJsonObject _currentBlueprint;
        private int _rotationStep;

        public BlueprintPasteSystem(Camera mainCamera, ClientBlueprintLibrary library, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _mainCamera = mainCamera;
            _library = library;
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        public void Enable()
        {
            _rotationStep = 0;
            _previewController ??= new BlueprintPastePreviewController(new GameObject("BlueprintPastePreview").transform);
        }

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // 選択変更時にBP実データを解決する
            // Resolve blueprint data when the selection changes
            if (context.IsSelectionChanged) ResolveBlueprint(context.SelectedBlueprintName);
            if (_currentBlueprint == null)
            {
                // 未解決BP（キャッシュに無い等）は前回のゴーストを残さない
                // Hide stale ghosts when the blueprint could not be resolved (e.g. not cached)
                _previewController.Hide();
                return;
            }

            // Rキーで90度回転（0-3で正規化）
            // Rotate 90 degrees with the rotation key (normalized to 0-3)
            if (InputManager.Playable.BlockPlaceRotation.GetKeyDown) _rotationStep = (_rotationStep + 1) % 4;

            if (!PlaceSystemUtil.TryGetRayHitPosition(_mainCamera, out var hitPoint, out _))
            {
                _previewController.Hide();
                return;
            }

            // XZは床スナップ、Yは整数グリッド面のため丸めで誤差を吸収する
            // Snap XZ by floor; round Y to absorb float error on the integer grid face
            var anchor = new Vector3Int(Mathf.FloorToInt(hitPoint.x), Mathf.RoundToInt(hitPoint.y), Mathf.FloorToInt(hitPoint.z));

            var placements = BlueprintPasteCalculator.CalculatePlacements(_currentBlueprint, anchor, _rotationStep);
            var placeableFlags = placements.Select(IsPlaceable).ToList();
            _previewController.UpdatePreview(placements, placeableFlags);

            // 左クリックで設置可能セルのみ送信（サーバー側は部分成功を許容）
            // Left click sends placeable cells only; server allows partial success
            if (InputManager.Playable.ScreenLeftClick.GetKeyUp && !EventSystem.current.IsPointerOverGameObject()) SendPlace(placements, placeableFlags);

            #region Internal

            void SendPlace(List<BlueprintPlacementElement> allPlacements, List<bool> flags)
            {
                var placeInfos = new List<PlaceInfo>();
                for (var i = 0; i < allPlacements.Count; i++)
                {
                    if (!flags[i]) continue;
                    placeInfos.Add(ToPlaceInfo(allPlacements[i]));
                }

                if (placeInfos.Count == 0) return;
                PlaceSystemUtil.SendPlaceBlockProtocol(placeInfos);
            }

            #endregion
        }

        public void Disable()
        {
            _previewController?.Hide();
            _currentBlueprint = null;
        }

        private void ResolveBlueprint(string blueprintName)
        {
            var pack = _library.Blueprints.FirstOrDefault(b => b.Name == blueprintName);
            _currentBlueprint = pack?.ToJsonObject();
        }

        private bool IsPlaceable(BlueprintPlacementElement placement)
        {
            // 全占有セルで既存ブロックとの重なりをチェック（サーバー側でも再検証される）
            // Check overlap against existing blocks over all occupied cells; server re-validates
            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(placement.BlockId).BlockSize;
            var positionInfo = new BlockPositionInfo(placement.Position, placement.Direction, blockSize);
            return !_blockGameObjectDataStore.IsOverlapPositionInfo(positionInfo);
        }

        private static PlaceInfo ToPlaceInfo(BlueprintPlacementElement placement)
        {
            // 設定JSONをUTF8バイト化してCreateParamsへ載せる（Task 4の規約）
            // Encode settings JSON as UTF8 bytes into CreateParams per the Task 4 convention
            var createParams = placement.Settings
                .Select(kvp => new BlockCreateParam(kvp.Key, Encoding.UTF8.GetBytes(kvp.Value)))
                .ToArray();

            return new PlaceInfo
            {
                Position = placement.Position,
                Direction = placement.Direction,
                VerticalDirection = BlockVerticalDirection.Horizontal,
                BlockId = placement.BlockId,
                Placeable = true,
                CreateParams = createParams,
            };
        }
    }
}
