using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using System;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Game.Train.RailGraph;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    public class DeleteTargetRail : MonoBehaviour, IDeleteTarget
    {       
        public BezierRailChain RailChain { get; private set; }
        private RailObjectIdCarrier _railObjectIdCarrier;
        private RailGraphClientCache _railGraphClientCache;
        
        public RailObjectIdCarrier RailObjectIdCarrier
        {
            get
            {
                if (_railObjectIdCarrier) return _railObjectIdCarrier;
                return _railObjectIdCarrier = GetComponent<RailObjectIdCarrier>();
            }
        }
        
        public void SetRailGraphCache(RailGraphClientCache cache)
        {
            _railGraphClientCache = cache;
        }
        
        public void SetParentBezierRailChain(BezierRailChain parent)
        {
            RailChain = parent;
        }

        public void SetRemovePreviewing()
        {
            // 撤去アニメ後に照準が残っても破棄済みの表示体へ触れない
            // A lingering aim must not access a rail view destroyed after the removal animation
            if (RailChain == null) return;
            RailChainRemovePreview.Of(RailChain).RequestRemovePreview(this);
        }
        public void ResetMaterial()
        {
            // 破棄済み表示体の赤解除は不要で、GetComponentも呼べない
            // A destroyed rail view needs no reset and cannot be queried for components
            if (RailChain == null) return;
            RailChainRemovePreview.Of(RailChain).ReleaseRemovePreview(this);
        }
        
        public bool IsRemovable(out LocalizationKey? deniedReason)
        {
            // 削除済みは表示すべき理由が無い拒否なのでnullを返す（default(LocalizationKey)は辞書引きで落ちる）
            // A removed rail is a denial without a displayable reason, so return null (default(LocalizationKey) breaks the lookup)
            var canDelete = CanDelete();
            deniedReason = canDelete switch
            {
                DeleteDeniedReason.None => null,
                DeleteDeniedReason.StationInternalEdge => LocalizationKeys.Ui.Delete.StationInternalRail,
                DeleteDeniedReason.NodeInUseByTrain => LocalizationKeys.Ui.Delete.RailHasVehicle,
                DeleteDeniedReason.UnknownError => LocalizationKeys.Ui.Delete.UnknownError,
                DeleteDeniedReason.Removed => null,
                _ => throw new ArgumentOutOfRangeException(),
            };
            return canDelete == DeleteDeniedReason.None;
        }
        
        public void CollectRemovedObjects(RemovedObjectCollector collector)
        {
            // レールIDはcanonical区間なのでそのまま記録へ写す。無償区間は設計上記録せず、未同期・駅内部は記録不能として数える
            // The rail object id is already canonical; free segments are skipped by design, unsynced/station-internal count as unrecordable
            var (fromId, toId) = RailObjectIdCodec.Decode(RailObjectIdCarrier.GetRailObjectId());
            var result = RemovedRail.Create(_railGraphClientCache, fromId, toId);
            switch (result.Outcome)
            {
                case RemovedRailCreateOutcome.Created:
                    collector.Add(result.Rail);
                    break;
                case RemovedRailCreateOutcome.FreeSegment:
                    break;
                default:
                    collector.AddUnrecordable($"rail edge {fromId}->{toId}: {result.Outcome}");
                    break;
            }
        }

        public void Delete()
        {
            var carrier = RailObjectIdCarrier;
            var railObjectId = carrier.GetRailObjectId();
            var (fromId, toId) = RailObjectIdCodec.Decode(railObjectId);
            
            // 未同期の端点で切断できない理由を残す
            // Report which unsynced endpoint prevents the disconnect request
            if (!_railGraphClientCache.TryGetNode(fromId, out var fromNode))
            {
                Debug.LogWarning($"[RailDelete] endpoint node not found: node={fromId} edge={fromId}->{toId}");
                return;
            }
            if (!_railGraphClientCache.TryGetNode(toId, out var toNode))
            {
                Debug.LogWarning($"[RailDelete] endpoint node not found: node={toId} edge={fromId}->{toId}");
                return;
            }
            
            ClientContext.VanillaApi.SendOnly.DisconnectRail(fromNode.NodeId, fromNode.NodeGuid, toNode.NodeId, toNode.NodeGuid);
        }

        // 削除はedge単位なのでrailObjectId（edge）を論理キーにする。同一edgeの重複だけ排除
        // Delete is per-edge, so the railObjectId is the logical key; only same-edge duplicates are deduped
        public object GetDeleteTargetKey()
        {
            return RailObjectIdCarrier.GetRailObjectId();
        }

        // レールはカテゴリー対象外なのでdefault扱い
        // Rails are not categorized, so treat them as default
        public string GetDestructionCategory()
        {
            return BlockMasterElementExtension.DefaultDestructionCategory;
        }
        
        private DeleteDeniedReason CanDelete()
        {
            if (RailChain.IsRemoving) return DeleteDeniedReason.Removed;
            
            var railObjectId = RailObjectIdCarrier.GetRailObjectId();
            var (fromId, toId) = RailObjectIdCodec.Decode(railObjectId);
            
            if (!_railGraphClientCache.TryGetNode(fromId, out var fromNode)) return DeleteDeniedReason.UnknownError;
            if (!_railGraphClientCache.TryGetNode(toId, out var toNode)) return DeleteDeniedReason.UnknownError;
            
            if (RailEdgeClassifier.IsStationInternalEdge(fromNode, toNode)) return DeleteDeniedReason.StationInternalEdge;
            
            return DeleteDeniedReason.None;
        }
        
        
        public enum DeleteDeniedReason
        {
            None,
            StationInternalEdge,
            NodeInUseByTrain,
            Removed,
            UnknownError,
        }
    }
}
