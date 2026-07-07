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
    ///     BPを回転・プレビュー付きで貼り付ける設置系
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

        // 手編集セーブ等でSettingsがnullのブロックを空設定として扱うための共有インスタンス
        // Shared instance so blocks whose Settings is null (e.g. hand-edited saves) paste as settings-less
        private static readonly Dictionary<string, string> EmptySettings = new();

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

            // コピー側と同じ規約でセルへスナップ
            // Snap to the cursor cell with the convention shared with the copy side
            var anchor = PlaceSystemUtil.SnapHitPointToCell(hitPoint);

            var placements = BlueprintPasteCalculator.CalculatePlacements(_currentBlueprint, anchor, _rotationStep);
            var placeableFlags = placements.Select(IsPlaceable).ToList();
            _previewController.UpdatePreview(placements, placeableFlags);

            // 左クリックで設置可能セルのみ送信
            // Left click sends placeable cells only; server allows partial success
            if (InputManager.Playable.ScreenLeftClick.GetKeyUp && !EventSystem.current.IsPointerOverGameObject()) SendPlace(placements, placeableFlags);

            #region Internal

            void ResolveBlueprint(string blueprintName)
            {
                var pack = _library.Blueprints.FirstOrDefault(b => b.Name == blueprintName);
                _currentBlueprint = pack?.ToJsonObject();
            }

            bool IsPlaceable(BlueprintPlacementElement placement)
            {
                // 全占有セルで既存ブロックとの重なりをチェック
                // Check overlap against existing blocks over all occupied cells; server re-validates
                var blockSize = MasterHolder.BlockMaster.GetBlockMaster(placement.BlockId).BlockSize;
                var positionInfo = new BlockPositionInfo(placement.Position, placement.Direction, blockSize);
                return !_blockGameObjectDataStore.IsOverlapPositionInfo(positionInfo);
            }

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

            PlaceInfo ToPlaceInfo(BlueprintPlacementElement placement)
            {
                // 設定JSONをUTF8バイト化してCreateParamsへ載せる
                // Encode settings JSON as UTF8 bytes into CreateParams; null settings from edited saves are treated as empty
                var createParams = (placement.Settings ?? EmptySettings)
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

            #endregion
        }

        public void Disable()
        {
            _previewController?.Hide();
            _currentBlueprint = null;
        }
    }
}
