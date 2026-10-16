using System;
using UniRx;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.Common;
using Client.Game.InGame.Block.Interact;
using Client.Game.InGame.Context;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Selection;
using Client.Game.InGame.UI.UIState.State;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class BlockGameObjectChild : MonoBehaviour, IDeleteTarget, IInteractRayTarget
    {
        private readonly Subject<DeleteCompletion> _deleteCompleted = new();
        public IObservable<DeleteCompletion> OnDeleteCompleted => _deleteCompleted;

        private const float RemoveDeniedReasonDisplaySeconds = 2f;

        public BlockGameObject BlockGameObject { get; private set; }

        // インタラクト面は開けるブロックにしか付かないので、開けないブロックはここでnullになる
        // The interact face exists only on openable blocks, so a non-openable block resolves to null here
        public IInteractable Interactable => BlockGameObject.Interactable;
        private bool _isDeleteRequesting;
        private LocalizationKey? _removeDeniedReason;
        private float _removeDeniedReasonUntil;
        
        public void Init(BlockGameObject blockGameObject)
        {
            BlockGameObject = blockGameObject;
        }
        
        public void SetRemovePreviewing()
        {
            // ブロックと巻き込まれる線を赤くする
            // Preview the block and its attached connections together
            BlockGameObject.SetRemovePreviewing();
            ClientDIContext.BlockAttachedConnectionResolver.RequestCascadePreview(BlockGameObject);
        }
        
        public void ResetMaterial()
        {
            BlockGameObject.ResetMaterial();
            ClientDIContext.BlockAttachedConnectionResolver.ReleaseCascadePreview(BlockGameObject);
        }
        
        public bool IsRemovable(out LocalizationKey? deniedReason)
        {
            if (_removeDeniedReason.HasValue && Time.time < _removeDeniedReasonUntil)
            {
                deniedReason = _removeDeniedReason;
                return false;
            }

            deniedReason = null;
            return true;
        }
        
        public void CollectRemovedObjects(RemovedObjectCollector collector)
        {
            // 本体と付随する線を同じUndoへ記録する
            // Record the block and cascaded connections in the same undo batch
            RemovedBlock.Capture(BlockGameObject, collector);
            ClientDIContext.BlockAttachedConnectionResolver.CollectRemovedConnections(BlockGameObject, collector);
        }

        public void Delete()
        {
            if (_isDeleteRequesting) return;

            DeleteAsync().Forget();
        }

        // 同一ブロックの全メッシュ子は同じBlockGameObjectを指す＝論理削除単位
        // All mesh children of a block share the same BlockGameObject = the logical delete unit
        public object GetDeleteTargetKey()
        {
            return BlockGameObject;
        }

        // ブロックマスタで定義された破壊カテゴリーを返す（未設定はdefault）
        // Return the destruction category defined in the block master (unset means default)
        public string GetDestructionCategory()
        {
            return BlockGameObject.BlockMasterElement.GetDestructionCategory();
        }

        private async UniTask DeleteAsync()
        {
            _isDeleteRequesting = true;
            var key = GetDeleteTargetKey();
            var block = BlockGameObject;
            // 応答寿命は表示体より長い。送信時の個体を指定する
            // The response outlives the view; address the instance captured at send time
            var response = await ClientContext.VanillaApi.Response.Block.BlockRemoveIfInstanceAwaitFinal(
                block.BlockPosInfo.OriginalPos, block.BlockInstanceId, Application.exitCancellationToken);
            _isDeleteRequesting = false;
            if (response == null)
            {
                Debug.LogWarning($"[Delete] block result unconfirmed: {key}");
                _deleteCompleted.OnNext(new DeleteCompletion(key, DeleteCompletionKind.Unconfirmed, LocalizationKeys.Ui.Delete.UnknownError));
                return;
            }
            var reason = response.Success ? (LocalizationKey?)null : GetReason(response.FailureReason);
            if (!response.Success)
            {
                Debug.Log($"[Delete] block rejected: {key} reason={response.FailureReason}");
                _removeDeniedReason = reason;
                _removeDeniedReasonUntil = Time.time + RemoveDeniedReasonDisplaySeconds;
            }
            _deleteCompleted.OnNext(new DeleteCompletion(key, response.Success ? DeleteCompletionKind.Succeeded : DeleteCompletionKind.Rejected, reason));
        }

        private static LocalizationKey GetReason(RemoveBlockProtocol.RemoveBlockFailureReason reason)
        {
            return reason switch
            {
                RemoveBlockProtocol.RemoveBlockFailureReason.NodeInUseByTrain => LocalizationKeys.Ui.Delete.RailHasVehicle,
                RemoveBlockProtocol.RemoveBlockFailureReason.InventoryFull => LocalizationKeys.Ui.Delete.InventoryFull,
                _ => LocalizationKeys.Ui.Delete.BlockDeleteFailed,
            };
        }
    }
}
