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
            BlockGameObject.SetRemovePreviewing();
        }
        
        public void ResetMaterial()
        {
            BlockGameObject.ResetMaterial();
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
            var blockPosition = BlockGameObject.BlockPosInfo.OriginalPos;
            var response = await ClientContext.VanillaApi.Response.BlockRemove(blockPosition, this.GetCancellationTokenOnDestroy());
            _isDeleteRequesting = false;


            // TODO 基盤通知システムができたらそちらの方に移行する

            // 削除拒否理由を既存の削除UIツールチップに渡す
            // Pass the denial reason to the existing delete UI tooltip flow.
            if (response == null || response.Success) return;
            SetRemoveDeniedReason(response.FailureReason);

            #region Internal

            void SetRemoveDeniedReason(RemoveBlockProtocol.RemoveBlockFailureReason failureReason)
            {
                var reasonKey = GetRemoveDeniedReasonKey(failureReason);
                if (!reasonKey.HasValue) return;

                // 一定時間だけIsRemovableから理由を返して表示する
                // Return the reason from IsRemovable for a short display window.
                _removeDeniedReason = reasonKey;
                _removeDeniedReasonUntil = Time.time + RemoveDeniedReasonDisplaySeconds;
            }

            static LocalizationKey? GetRemoveDeniedReasonKey(RemoveBlockProtocol.RemoveBlockFailureReason failureReason)
            {
                return failureReason switch
                {
                    RemoveBlockProtocol.RemoveBlockFailureReason.NodeInUseByTrain => LocalizationKeys.Ui.Delete.RailHasVehicle,
                    RemoveBlockProtocol.RemoveBlockFailureReason.Unknown => LocalizationKeys.Ui.Delete.BlockDeleteFailed,
                    _ => null,
                };
            }

            #endregion
        }
    }
}
