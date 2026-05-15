using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Block
{
    // 貨物プラットフォーム系UIの共通基底: モード表示・トグル送信・StateDetail監視を担う
    // Common base for cargo platform UIs: mode display, toggle posting, and StateDetail watching
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

            toggleModeButton.onClick.AddListener(OnToggleClicked);
            UpdateModeText();
        }

        protected virtual void Update()
        {
            UpdateModeText();
        }

        private void UpdateModeText()
        {
            // サーバーから受信した現在モードを取得し、未受信時のみLoadToTrainで仮表示
            // Read the latest mode from StateDetail; fall back to LoadToTrain until the first packet arrives
            var state = BlockGameObject.GetStateDetail<TrainPlatformTransferStateDetail>(TrainPlatformTransferStateDetail.BlockStateDetailKey);
            var mode = state?.Mode ?? TrainPlatformTransferComponent.TransferMode.LoadToTrain;
            currentModeText.text = mode == TrainPlatformTransferComponent.TransferMode.LoadToTrain
                ? "ロード（ブロック→列車）"
                : "アンロード（列車→ブロック）";
        }

        private void OnToggleClicked()
        {
            if (_isSending) return;
            SendToggle().Forget();
        }

        private async UniTask SendToggle()
        {
            _isSending = true;
            toggleModeButton.interactable = false;

            // finallyで状態を必ず戻す
            // Always restore UI state via finally
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

                // 成功時はサーバーからのStateDetail配信でUIが追従するため、ここでは何もしない
                // On success, the StateDetail broadcast will update the UI automatically
                if (response == null || !response.Success)
                {
                    Debug.LogWarning($"TrainPlatform mode change failed. reason={response?.FailureReason}");
                }
            }
            finally
            {
                _isSending = false;
                toggleModeButton.interactable = true;
            }
        }
    }
}
