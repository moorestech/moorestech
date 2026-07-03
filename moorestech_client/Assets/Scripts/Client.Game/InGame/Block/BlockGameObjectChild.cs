using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class BlockGameObjectChild : MonoBehaviour, IDeleteTarget
    {
        private const float RemoveDeniedReasonDisplaySeconds = 2f;

        public BlockGameObject BlockGameObject { get; private set; }
        private bool _isDeleteRequesting;
        private string _removeDeniedReason;
        private float _removeDeniedReasonUntil;
        
        public void Init(BlockGameObject blockGameObject)
        {
            BlockGameObject = blockGameObject;
        }
        
        public void SetRemovePreviewing()
        {
            BlockGameObject.SetRemovePreviewing();
        }
        
        public void ResetMaterial()
        {
            BlockGameObject.ResetMaterial();
        }
        
        public bool IsRemovable(out string reason)
        {
            if (!string.IsNullOrEmpty(_removeDeniedReason) && Time.time < _removeDeniedReasonUntil)
            {
                reason = _removeDeniedReason;
                return false;
            }

            reason = null;
            return true;
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

        private async UniTask DeleteAsync()
        {
            _isDeleteRequesting = true;
            var blockPosition = BlockGameObject.BlockPosInfo.OriginalPos;
            var response = await ClientContext.VanillaApi.Response.BlockRemove(blockPosition, this.GetCancellationTokenOnDestroy());
            _isDeleteRequesting = false;


            // TODO 基盤通知システムができたらそちらの方に移行する

            // 削除拒否理由を既存の削除UIツールチップに渡す
            // Pass the denial reason to the existing delete UI tooltip flow.
            if (response == null || response.Success) return;
            SetRemoveDeniedReason(response.FailureReason);
        }

        private void SetRemoveDeniedReason(RemoveBlockProtocol.RemoveBlockFailureReason failureReason)
        {
            var message = GetRemoveDeniedReasonMessage(failureReason);
            if (message == null) return;

            // 一定時間だけIsRemovableから理由を返して表示する
            // Return the reason from IsRemovable for a short display window.
            _removeDeniedReason = message;
            _removeDeniedReasonUntil = Time.time + RemoveDeniedReasonDisplaySeconds;
        }

        private static string GetRemoveDeniedReasonMessage(RemoveBlockProtocol.RemoveBlockFailureReason failureReason)
        {
            return failureReason switch
            {
                RemoveBlockProtocol.RemoveBlockFailureReason.NodeInUseByTrain => "レール上に車両があります。",
                RemoveBlockProtocol.RemoveBlockFailureReason.Unknown => "ブロックを削除できませんでした。",
                _ => null,
            };
        }
    }
}
