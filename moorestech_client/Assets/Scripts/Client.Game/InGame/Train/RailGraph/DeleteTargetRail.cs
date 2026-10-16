using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using UniRx;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
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
        private readonly Subject<DeleteCompletion> _deleteCompleted = new();
        public IObservable<DeleteCompletion> OnDeleteCompleted => _deleteCompleted;
       
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
            // 撤去後に破棄済み表示体へ触れない
            // Do not touch a rail view destroyed after removal
            if (RailChain == null) return;
            RailChainRemovePreview.Of(RailChain).RequestRemovePreview(this);
        }
        public void ResetMaterial()
        {
            // 破棄済みの表示体には触れられない
            // A destroyed rail view cannot be reset or queried
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
            // 直接撤去では無償区間も復元不能件数へ含める
            // Count directly deleted free segments among unrestorable items
            var (fromId, toId) = RailObjectIdCodec.Decode(RailObjectIdCarrier.GetRailObjectId());
            RemovedRail.Capture(_railGraphClientCache, fromId, toId, RemovedRailCaptureContext.Direct, collector);
        }

        public void Delete() => DeleteAsync().Forget();

        private async UniTask DeleteAsync()
        {
            var key = GetDeleteTargetKey();
            var carrier = RailObjectIdCarrier;
            var railObjectId = carrier.GetRailObjectId();
            var (fromId, toId) = RailObjectIdCodec.Decode(railObjectId);
            
            // 未同期の端点で切断できない理由を残す
            // Report which unsynced endpoint prevents the disconnect request
            if (!_railGraphClientCache.TryGetNode(fromId, out var fromNode))
            {
                Debug.LogWarning($"[RailDelete] endpoint node not found: node={fromId} edge={fromId}->{toId}");
                _deleteCompleted.OnNext(new DeleteCompletion(key, DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.UnknownError));
                return;
            }
            if (!_railGraphClientCache.TryGetNode(toId, out var toNode))
            {
                Debug.LogWarning($"[RailDelete] endpoint node not found: node={toId} edge={fromId}->{toId}");
                _deleteCompleted.OnNext(new DeleteCompletion(key, DeleteCompletionKind.Rejected, LocalizationKeys.Ui.Delete.UnknownError));
                return;
            }
            
            // 既存応答を待ち、描画体の破棄には待機を連動させない
            // Await the existing response independently of view destruction
            var response = await ClientContext.VanillaApi.Response.Removal.DisconnectRail(fromNode.NodeId, fromNode.NodeGuid, toNode.NodeId, toNode.NodeGuid, Application.exitCancellationToken);
            var kind = response == null ? DeleteCompletionKind.Unconfirmed : response.Success ? DeleteCompletionKind.Succeeded : DeleteCompletionKind.Rejected;
            LocalizationKey? reason = kind == DeleteCompletionKind.Succeeded ? null : LocalizationKeys.Ui.Delete.UnknownError;
            if (response != null && !response.Success)
            {
                reason = response.FailureReason switch
                {
                    RailConnectionEditProtocol.RailConnectionEditFailureReason.NodeInUseByTrain => LocalizationKeys.Ui.Delete.RailHasVehicle,
                    RailConnectionEditProtocol.RailConnectionEditFailureReason.StationInternalEdge => LocalizationKeys.Ui.Delete.StationInternalRail,
                    RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughInventorySpace => LocalizationKeys.Ui.Delete.InventoryFull,
                    _ => LocalizationKeys.Ui.Delete.UnknownError,
                };
            }
            if (kind != DeleteCompletionKind.Succeeded) Debug.LogWarning($"[RailDelete] {key}: {kind} reason={reason}");
            _deleteCompleted.OnNext(new DeleteCompletion(key, kind, reason));
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
            
            if (fromNode.StationRef.IsSameStation(toNode.StationRef)) return DeleteDeniedReason.StationInternalEdge;
            
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
