using System;
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.PreviewGhost;
using Client.Game.InGame.UI.UIState;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.ChallengesModule;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial
{
    /// <summary>
    ///     ゴーストをguidごとに持ち生成・移動・破棄を担う
    ///     Owns one target-cell ghost per tutorialGuid with its creation, movement and teardown; which cell to point at is the caller's decision
    /// </summary>
    public class BlockPlacePreviewTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        public string TutorialType => TutorialsElement.TutorialTypeConst.blockPlacePreview;
        
        private readonly Dictionary<string, PlacementGhostEntry> _entries = new();
        
        // 絶対座標型として適用された分の設置検知購読
        // Placement-detection subscriptions for absolute blockPlacePreview applications, held independently per guid
        private readonly Dictionary<string, IDisposable> _placedSubscriptions = new();
        
        private BlockGameObjectDataStore _blockGameObjectDataStore;
        
        [Inject]
        public void Construct(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }
        
        /// <summary>
        ///     ゴーストを出す目標セルを差し替える。同じ目標なら何もしないので毎フレーム呼んでよい
        ///     Replaces the target cell the ghost points at; an unchanged target is a no-op, so it is safe to call every frame
        /// </summary>
        public void SetTargetCell(BlockId blockId, Vector3Int cell, BlockDirection direction, string tutorialGuid)
        {
            if (!_entries.TryGetValue(tutorialGuid, out var entry))
            {
                entry = new PlacementGhostEntry($"block-place-preview-pin-{tutorialGuid}");
                _entries.Add(tutorialGuid, entry);
            }
            
            entry.SetTarget(blockId, cell, direction, transform);
        }
        
        public void ClearTarget(string tutorialGuid)
        {
            if (!_entries.TryGetValue(tutorialGuid, out var entry)) return;
            
            entry.Destroy();
            _entries.Remove(tutorialGuid);
            
            // Webピンは冪等に除去（未配信でも安全）
            // Removing the web pin is idempotent, safe even when never published
            WorldPinStateStore.Instance.RemovePin(entry.WebPinId);
        }
        
        private void Update()
        {
            // Webへ射影配信する（3Dプレビュー自体はUnity側に残置し、矢印/ピンのみWeb化）
            // Project and publish to the web overlay (the 3D preview stays in Unity; only the arrow/pin moves to web)
            if (!WebUiScreenGate.IsWebUiMode || _entries.Count == 0) return;
            
            var camera = CameraManager.MainCamera.Camera;
            if (!camera) return;
            
            foreach (var pair in _entries)
            {
                var entry = pair.Value;
                if (entry.TargetCell == null || entry.PreviewObject == null) continue;
                
                var projection = WorldPinScreenProjection.Project(camera, entry.PreviewObject.transform.position);
                WorldPinStateStore.Instance.SetPin(entry.WebPinId, pair.Key, projection);
            }
        }
        
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var param = (BlockPlacePreviewTutorialParam)tutorial.TutorialParam;
            var blockId = MasterHolder.BlockMaster.GetBlockId(param.BlockGuid);
            var direction = Enum.Parse<BlockDirection>(param.BlockDirection);
            var tutorialGuid = tutorial.TutorialGuid.ToString("D");
            
            // 既に目標ブロックが配置済みなら早期終了
            // Exit early when the target block already exists
            if (IsTargetBlockPlaced()) return null;
            
            SetTargetCell(blockId, param.Position, direction, tutorialGuid);
            SubscribePlacementEvent();
            
            return new BlockPlacePreviewTutorialView(this, tutorialGuid);
            
            #region Internal
            
            bool IsTargetBlockPlaced()
            {
                return _blockGameObjectDataStore.TryGetBlockGameObject(param.Position, out var block) && block.BlockId == blockId;
            }
            
            // 指定座標への対象ブロック設置で該当guidだけを完了する
            // Completes only this guid when the target block lands on the specified position
            void SubscribePlacementEvent()
            {
                if (_placedSubscriptions.TryGetValue(tutorialGuid, out var previous)) previous.Dispose();
                _placedSubscriptions[tutorialGuid] = _blockGameObjectDataStore.OnBlockPlaced.Subscribe(block =>
                {
                    if (block.BlockId != blockId) return;
                    if (block.BlockPosInfo.OriginalPos != param.Position) return;
                    
                    Complete(tutorialGuid);
                });
            }
            
            #endregion
        }
        
        public void Complete(string tutorialGuid)
        {
            if (_placedSubscriptions.TryGetValue(tutorialGuid, out var subscription))
            {
                subscription.Dispose();
                _placedSubscriptions.Remove(tutorialGuid);
            }
            
            ClearTarget(tutorialGuid);
        }
        
        private void OnDestroy()
        {
            foreach (var entry in _entries.Values)
            {
                entry.Destroy();
                WorldPinStateStore.Instance.RemovePin(entry.WebPinId);
            }
            _entries.Clear();
            
            foreach (var subscription in _placedSubscriptions.Values) subscription.Dispose();
            _placedSubscriptions.Clear();
        }
    }
}
