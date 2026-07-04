using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// GearChainPole接続システムのエントリポイントであり、状態（延長の起点ポール）の唯一の所有者。
    /// 毎フレーム「取込→集める→決める→映す→送る→反映」の一方向パイプラインを実行し、逆流やコールバックは存在しない。
    /// ポールアイテム所持時は手持ちポールの新規設置と連続延長、チェーンアイテム所持時は既存ポール同士の接続のみを行う。
    /// Entry point of the GearChainPole connection system and the sole owner of its state (the extension source pole).
    /// Runs the one-way per-frame pipeline consume → collect → decide → render → send → apply; there is no back-flow or callback.
    /// Holding a pole item allows placing and continuously extending that pole; holding a chain item only connects existing poles.
    /// </summary>
    public class GearChainPoleConnectSystem : IPlaceSystem
    {
        private readonly GearChainPoleFrameInputCollector _inputCollector;
        private readonly GearChainPoleExtendPreviewObject _previewObject;
        private readonly GearChainPoleExtendRequestSender _requestSender;

        // 延長の起点ポール。このシステムが持つ唯一の状態
        // Extension source pole: the only state this system owns
        private IGearChainPoleConnectAreaCollider _sourcePole;

        public GearChainPoleConnectSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, LocalPlayerInventoryController localPlayerInventory, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _previewObject = new GearChainPoleExtendPreviewObject(previewBlockController);
            _requestSender = new GearChainPoleExtendRequestSender(blockGameObjectDataStore);
            _inputCollector = new GearChainPoleFrameInputCollector(mainCamera, localPlayerInventory.LocalPlayerInventory, blockGameObjectDataStore, _previewObject);
        }

        public void Enable()
        {
            ResetState();
        }

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // 取込: 前フレームまでの設置応答を起点に反映する
            // Consume: apply placement responses up to the previous frame to the source
            if (_requestSender.TryConsumePlacedPole(out var placedPole)) _sourcePole = placedPole;

            // 集める→決める: 手持ちアイテムでモードを分岐し、入力スナップショットから結果を得る
            // Collect → decide: branch the mode by the holding item and map the input snapshot to a result
            GearChainPoleFrameResult result;
            if (GearChainPoleItemFinder.TryGetPoleBlockMaster(context.HoldingItemId, out var poleBlockMaster))
            {
                var input = _inputCollector.CollectPlaceExtend(context, _sourcePole, poleBlockMaster, _requestSender.IsAwaitingResponse);
                result = GearChainPolePlaceExtendMode.Decide(input);
            }
            else
            {
                var input = _inputCollector.CollectChainConnect(context, _sourcePole);
                result = GearChainPoleChainConnectMode.Decide(input);
            }

            // 映す: プレビュー表示指示を反映する
            // Render: apply the preview command
            _previewObject.Apply(result.Preview);

            // 送る: 無効化と送信指示を実行する
            // Send: execute invalidation and send commands
            if (result.InvalidatePendingRequest) _requestSender.Invalidate();
            if (result.ExtendSend.HasValue) _requestSender.Send(result.ExtendSend.Value);
            if (result.ChainConnectSend.HasValue)
            {
                var connect = result.ChainConnectSend.Value;
                ClientContext.VanillaApi.SendOnly.ConnectGearChain(connect.FromPos, connect.ToPos, connect.ChainItemId);
            }

            // 反映: 次の起点を確定する
            // Apply: fix the next source
            _sourcePole = result.NextSourcePole;
        }

        public void Disable()
        {
            ResetState();
        }

        private void ResetState()
        {
            // 起点・プレビュー・進行中の応答をすべてクリアする
            // Clear the source, preview and pending responses
            _sourcePole = null;
            _previewObject.Hide();
            _requestSender.Invalidate();
        }
    }
}
