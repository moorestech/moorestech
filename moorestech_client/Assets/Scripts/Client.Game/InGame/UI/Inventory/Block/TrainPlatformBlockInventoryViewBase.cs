using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Server.Protocol.PacketResponse;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Block
{
    // 貨物プラットフォーム系UIの共通基底クラス
    // モード表示・トグルボタン・StateDetail監視・モード切替リクエスト送信を担う
    // Common base for cargo platform UIs: shows current mode, toggles via button, watches StateDetail and posts mode-change requests
    public abstract class TrainPlatformBlockInventoryViewBase : CommonBlockInventoryViewBase
    {
        [SerializeField] private Button toggleModeButton;
        [SerializeField] private TMP_Text currentModeText;

        protected BlockGameObject BlockGameObject;

        // 切替リクエスト送信中はボタンを無効化する
        // Disable the toggle button while a request is in flight to prevent double-fire
        private bool _isSending;

        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            BlockGameObject = blockGameObject;

            if (toggleModeButton != null)
            {
                toggleModeButton.onClick.AddListener(OnToggleClicked);
            }
            UpdateModeText(currentModeFallback: TrainPlatformTransferComponent.TransferMode.LoadToTrain);
        }

        protected virtual void Update()
        {
            UpdateModeText(currentModeFallback: TrainPlatformTransferComponent.TransferMode.LoadToTrain);
        }

        #region Internal

        private void UpdateModeText(TrainPlatformTransferComponent.TransferMode currentModeFallback)
        {
            // サーバーから受信した現在モードを取得し、未受信ならフォールバック
            // Read the latest mode from StateDetail; fall back when nothing has been received yet
            var state = BlockGameObject.GetStateDetail<TrainPlatformTransferStateDetail>(TrainPlatformTransferStateDetail.BlockStateDetailKey);
            var mode = state?.Mode ?? currentModeFallback;
            if (currentModeText != null)
            {
                currentModeText.text = mode == TrainPlatformTransferComponent.TransferMode.LoadToTrain
                    ? "ロード（ブロック→列車）"
                    : "アンロード（列車→ブロック）";
            }
        }

        private void OnToggleClicked()
        {
            if (_isSending) return;
            SendToggle().Forget();
        }

        private async UniTask SendToggle()
        {
            _isSending = true;
            if (toggleModeButton != null) toggleModeButton.interactable = false;

            try
            {
                // 現在モードの反対を要求
                // Request the opposite of the current mode
                var state = BlockGameObject.GetStateDetail<TrainPlatformTransferStateDetail>(TrainPlatformTransferStateDetail.BlockStateDetailKey);
                var current = state?.Mode ?? TrainPlatformTransferComponent.TransferMode.LoadToTrain;
                var next = current == TrainPlatformTransferComponent.TransferMode.LoadToTrain
                    ? TrainPlatformTransferComponent.TransferMode.UnloadToPlatform
                    : TrainPlatformTransferComponent.TransferMode.LoadToTrain;

                var ct = this.GetCancellationTokenOnDestroy();
                var response = await ClientContext.VanillaApi.Response.SetTrainPlatformTransferMode(
                    BlockGameObject.BlockPosInfo.OriginalPos, next, ct);

                if (response == null || !response.Success)
                {
                    Debug.LogWarning($"TrainPlatform mode change failed. reason={response?.FailureReason}");
                }
                // 成功時はサーバーからのStateDetail配信でUIが追従するため、ここでは何もしない
                // On success, the StateDetail broadcast will update the UI automatically
            }
            finally
            {
                _isSending = false;
                if (toggleModeButton != null) toggleModeButton.interactable = true;
            }
        }

        #endregion
    }
}
