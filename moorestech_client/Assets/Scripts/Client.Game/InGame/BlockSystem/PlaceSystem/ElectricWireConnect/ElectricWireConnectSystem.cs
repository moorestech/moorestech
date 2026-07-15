using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Modes;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.Inventory.Main;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    /// 電線アイテム所持中のツール。起点選択・接続・切断・レール式延長の4状態を統括する
    /// The tool active while holding a wire item; orchestrates origin-select, connect, disconnect and rail-style extend
    /// </summary>
    public class ElectricWireConnectSystem : PlaceSystemBase<ConnectToolPlacementTarget>
    {
        private readonly ElectricWireToolContext _context;
        private readonly ElectricWireEditMode _editMode;
        private readonly ElectricWireExtendMode _extendMode;

        // 接続の起点ブロック。nullなら起点未選択状態
        // The connection origin block; null means no origin selected
        private BlockGameObject _sourceBlock;

        // ツール世代。Enable/Disableで進め、旧世代のin-flight応答を無視する
        // Tool epoch; advanced on Enable/Disable so stale in-flight responses are ignored
        private int _toolEpoch;

        public ElectricWireConnectSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, LocalPlayerInventoryController localPlayerInventory, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            var wirePreview = new ElectricWireExtendPreviewObject(mainCamera);
            _context = new ElectricWireToolContext(mainCamera, previewBlockController, localPlayerInventory.LocalPlayerInventory, blockGameObjectDataStore, wirePreview);
            _editMode = new ElectricWireEditMode(_context);
            _extendMode = new ElectricWireExtendMode(_context);
        }

        public override void Enable()
        {
            // 有効化のたびに起点選択をリセットし、世代を進める
            // Reset the origin selection and advance the epoch each time the tool is enabled
            _sourceBlock = null;
            _toolEpoch++;
        }

        protected override void ManualUpdate(ConnectToolPlacementTarget target, bool isSelectionChanged)
        {
            // 延長応答で設置された電柱をポーリング取り込みし、現世代のみ起点へ反映する
            // Poll the pole placed by an extend response and adopt it as origin only within the current epoch
            if (ElectricWireExtendRequestSender.TryConsumePlacedPole(_toolEpoch, out var placedPole)) _sourceBlock = placedPole;

            // 起点未選択なら選択・切断、選択済みなら接続・延長を処理する
            // No origin: select or disconnect; with origin: connect or extend
            if (_sourceBlock == null)
            {
                _sourceBlock = _editMode.Update();
                return;
            }

            // 延長送信が発生したら起点をクリアし、応答はポーリングで取り込む
            // Clear the origin when an extend request was sent; the response is adopted via polling
            var extendRequested = _extendMode.Update(new PlaceSystemUpdateContext(target, isSelectionChanged), _sourceBlock, _toolEpoch);
            if (extendRequested) _sourceBlock = null;
        }

        public override void Disable()
        {
            // ツール切替時のみ起点を解除し、世代を進めてプレビューを消す
            // Release the origin only on tool switch, advance the epoch and hide previews
            _sourceBlock = null;
            _toolEpoch++;
            _context.WirePreview.SetActive(false);
            _context.PreviewBlockController.SetActive(false);
        }
    }
}
