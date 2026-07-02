using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.UI.Inventory.Main;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// GearChainPoleの接続システム。手持ちアイテムで操作モードが決まる。
    /// ポールアイテム所持時は手持ちポールの新規設置と連続延長、チェーンアイテム所持時は既存ポール同士の接続のみを行う。
    /// GearChainPole connection system whose mode is decided by the holding item.
    /// Holding a pole item allows placing and continuously extending that pole; holding a chain item only connects existing poles.
    /// </summary>
    public class GearChainPoleConnectSystem : IPlaceSystem
    {
        private readonly GearChainPoleConnectModeContext _modeContext;
        private readonly GearChainPolePlaceExtendMode _placeExtendMode;
        private readonly GearChainPoleChainConnectMode _chainConnectMode;

        public GearChainPoleConnectSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, LocalPlayerInventoryController localPlayerInventory, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            var previewObject = new GearChainPoleExtendPreviewObject(previewBlockController);
            var requestSender = new GearChainPoleExtendRequestSender(blockGameObjectDataStore);
            _modeContext = new GearChainPoleConnectModeContext(mainCamera, localPlayerInventory.LocalPlayerInventory, blockGameObjectDataStore, previewObject, requestSender);
            _placeExtendMode = new GearChainPolePlaceExtendMode(_modeContext);
            _chainConnectMode = new GearChainPoleChainConnectMode(_modeContext);
        }

        public void Enable()
        {
            // 接続元の選択状態と進行中の応答をリセットする
            // Reset source selection state and pending responses
            _modeContext.Reset();
        }

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // 手持ちがポールアイテムかチェーンアイテムかでモードを分岐する
            // Branch mode by whether the holding item is a pole item or a chain item
            var hitPole = _modeContext.GetGearChainPoleCollider();
            if (GearChainPoleExtendPreviewCalculator.TryGetPoleBlockMaster(context.HoldingItemId, out var poleBlockMaster)) _placeExtendMode.ManualUpdate(context, hitPole, poleBlockMaster);
            else _chainConnectMode.ManualUpdate(context, hitPole);
        }

        public void Disable()
        {
            // 無効化時に状態・プレビュー・応答をクリア
            // Clear selection state, preview and pending responses on disable
            _modeContext.Reset();
        }
    }
}
