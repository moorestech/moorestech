using System;
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
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
    ///     設置目標セルのゴーストをtutorialGuidごとに持ち、その生成・移動・破棄を引き受ける。どのセルを指すかは呼び手が決める
    ///     Owns one target-cell ghost per tutorialGuid with its creation, movement and teardown; which cell to point at is the caller's decision
    /// </summary>
    public class BlockPlacePreviewTutorialManager : MonoBehaviour, ITutorialView, ITutorialViewManager
    {
        public string TutorialType => TutorialsElement.TutorialTypeConst.blockPlacePreview;
        
        private readonly Dictionary<string, TutorialGhostEntry> _entries = new();
        
        private BlockGameObjectDataStore _blockGameObjectDataStore;
        private IDisposable _blockPlacedDisposable;
        
        // 絶対座標型（blockPlacePreview）として自分が適用された時のエントリキー
        // Entry key used when this manager itself is applied as the absolute blockPlacePreview type
        private string _ownTutorialGuid = "";
        
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
                entry = new TutorialGhostEntry(tutorialGuid);
                _entries.Add(tutorialGuid, entry);
            }
            
            if (entry.IsSameTarget(blockId, cell, direction)) return;
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
            
            foreach (var entry in _entries.Values)
            {
                if (entry.TargetCell == null || entry.PreviewObject == null) continue;
                
                var projection = WorldPinScreenProjection.Project(camera, entry.PreviewObject.transform.position);
                WorldPinStateStore.Instance.SetPin(entry.WebPinId, entry.TutorialGuid, projection);
            }
        }
        
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var param = (BlockPlacePreviewTutorialParam)tutorial.TutorialParam;
            var blockId = MasterHolder.BlockMaster.GetBlockId(param.BlockGuid);
            var direction = Enum.Parse<BlockDirection>(param.BlockDirection);
            
            // 既に目標ブロックが配置済みなら早期終了
            // Exit early when the target block already exists
            if (IsTargetBlockPlaced()) return null;
            
            _ownTutorialGuid = tutorial.TutorialGuid.ToString("D");
            SetTargetCell(blockId, param.Position, direction, _ownTutorialGuid);
            SubscribePlacementEvent();
            
            return this;
            
            #region Internal
            
            bool IsTargetBlockPlaced()
            {
                return _blockGameObjectDataStore.TryGetBlockGameObject(param.Position, out var block) && block.BlockId == blockId;
            }
            
            // 指定座標への対象ブロック設置で完了する
            // Completes when the target block lands on the specified position
            void SubscribePlacementEvent()
            {
                _blockPlacedDisposable?.Dispose();
                _blockPlacedDisposable = _blockGameObjectDataStore.OnBlockPlaced.Subscribe(block =>
                {
                    if (block.BlockId != blockId) return;
                    if (block.BlockPosInfo.OriginalPos != param.Position) return;
                    
                    CompleteTutorial();
                });
            }
            
            #endregion
        }
        
        public void CompleteTutorial()
        {
            _blockPlacedDisposable?.Dispose();
            _blockPlacedDisposable = null;
            if (_ownTutorialGuid != "") ClearTarget(_ownTutorialGuid);
        }
        
        private void OnDestroy()
        {
            foreach (var entry in _entries.Values)
            {
                entry.Destroy();
                WorldPinStateStore.Instance.RemovePin(entry.WebPinId);
            }
            _entries.Clear();
        }
    }
}
