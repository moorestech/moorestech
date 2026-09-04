using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util.AnchorRelative;
using Client.Game.InGame.Player;
using Mooresmaster.Model.ChallengesModule;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial.PlacementGuide
{
    /// <summary>
    ///     最寄りアンカー基準の相対セルへ複数追従表示
    ///     Pushes anchor-relative cells as targets, tracking multiple entries at once
    ///     ゴーストそのものの生成と破棄は BlockPlacePreviewTutorialManager が持つ
    ///     BlockPlacePreviewTutorialManager owns creating and tearing down the ghosts themselves
    /// </summary>
    public class RelativeBlockPlacePreviewTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        public string TutorialType => TutorialsElement.TutorialTypeConst.relativeBlockPlacePreview;
        
        private readonly Dictionary<Guid, RelativeBlockPlacePreviewEntry> _entries = new();
        private readonly List<Guid> _completedBuffer = new();
        
        private BlockGameObjectDataStore _blockGameObjectDataStore;
        private BlockPlacePreviewTutorialManager _blockPlacePreviewTutorialManager;
        private IDisposable _blockPlacedDisposable;
        
        [Inject]
        public void Construct(BlockGameObjectDataStore blockGameObjectDataStore, BlockPlacePreviewTutorialManager blockPlacePreviewTutorialManager)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _blockPlacePreviewTutorialManager = blockPlacePreviewTutorialManager;
        }
        
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var entry = new RelativeBlockPlacePreviewEntry(tutorial, this);
            _entries[entry.TutorialGuid] = entry;
            
            // 目標セルへの設置で該当分だけ完了する
            // One shared subscription completes only the entry whose target cell received its block
            // 目標セルはアンカー追従で毎フレーム動くため、購読時の値ではなく現在の entry.TargetCell と突き合わせる
            // Target cells move with anchor tracking every frame, so compare against the current entry.TargetCell, not a captured value
            _blockPlacedDisposable ??= _blockGameObjectDataStore.OnBlockPlaced.Subscribe(OnBlockPlaced);
            return entry;
        }
        
        public void Complete(Guid tutorialGuid)
        {
            if (!_entries.TryGetValue(tutorialGuid, out var entry)) return;
            
            _blockPlacePreviewTutorialManager.ClearTarget(entry.TutorialGuidString);
            _entries.Remove(tutorialGuid);
            
            if (_entries.Count != 0) return;
            _blockPlacedDisposable?.Dispose();
            _blockPlacedDisposable = null;
        }
        
        private void OnBlockPlaced(BlockGameObject block)
        {
            _completedBuffer.Clear();
            foreach (var entry in _entries.Values)
            {
                if (block.BlockId != entry.TargetBlockId) continue;
                if (entry.TargetCell == null || block.BlockPosInfo.OriginalPos != entry.TargetCell.Value) continue;

                // 向き違いでは繋がらないため残す
                // A mismatched direction never connects the gears, so the guide stays up
                if (block.BlockPosInfo.BlockDirection != entry.TargetDirection.Value) continue;
                _completedBuffer.Add(entry.TutorialGuid);
            }
            
            foreach (var guid in _completedBuffer) Complete(guid);
        }
        
        private void Update()
        {
            if (_entries.Count == 0) return;
            
            var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;
            _completedBuffer.Clear();
            foreach (var entry in _entries.Values)
            {
                // アンカーは撤去や増設で変わるため毎フレーム最寄りを取り直す（VeinPin と同じ追従）
                // The anchor can be removed or duplicated, so re-pick the nearest one every frame (same tracking as VeinPin)
                var anchor = _blockGameObjectDataStore.SearchNearestBlock(entry.AnchorBlockGuid, playerPosition);
                if (anchor == null)
                {
                    entry.SetTarget(null, null);
                    _blockPlacePreviewTutorialManager.ClearTarget(entry.TutorialGuidString);
                    continue;
                }

                // アンカー向きで回したローカル値を使う
                // Use the anchor-rotated local cell and direction (same conversion as gearConnects)
                var targetCell = AnchorRelativeOriginUtil.ResolveWorldOrigin(anchor.BlockPosInfo, entry.Offset, entry.LocalDirection, entry.TargetBlockSize);
                var worldDirection = AnchorRelativeDirectionUtil.RotateByAnchor(entry.LocalDirection, anchor.BlockPosInfo.BlockDirection);
                entry.SetTarget(targetCell, worldDirection);

                // アンカーが動いた先に既に同じ向きで対象ブロックがあれば、設置イベントは来ないのでここで完了させる
                // When the target block already sits with the same direction where the anchor moved to, no placement event will come, so complete here
                if (_blockGameObjectDataStore.TryGetBlockGameObject(targetCell, out var existing) && existing.BlockId == entry.TargetBlockId && existing.BlockPosInfo.BlockDirection == worldDirection)
                {
                    _completedBuffer.Add(entry.TutorialGuid);
                    continue;
                }

                _blockPlacePreviewTutorialManager.SetTargetCell(entry.TargetBlockId, targetCell, worldDirection, entry.TutorialGuidString);
            }
            
            foreach (var guid in _completedBuffer) Complete(guid);
        }
    }
}
