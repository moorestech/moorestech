using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.UI.Inventory.Main;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    /// 電線アイテム所持中のツール。起点選択・接続・切断・レール式延長の4状態を統括する
    /// The tool active while holding a wire item; orchestrates origin-select, connect, disconnect and rail-style extend
    /// </summary>
    public class ElectricWireConnectSystem : IPlaceSystem
    {
        private readonly ElectricWireToolContext _context;
        private readonly ElectricWireEditMode _editMode;
        private readonly ElectricWireExtendMode _extendMode;

        // 接続の起点ブロック。nullなら起点未選択状態
        // The connection origin block; null means no origin selected
        private BlockGameObject _sourceBlock;

        public ElectricWireConnectSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, LocalPlayerInventoryController localPlayerInventory, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            var wirePreview = new ElectricWireExtendPreviewObject();
            _context = new ElectricWireToolContext(mainCamera, previewBlockController, localPlayerInventory.LocalPlayerInventory, blockGameObjectDataStore, wirePreview);
            _editMode = new ElectricWireEditMode(_context);
            _extendMode = new ElectricWireExtendMode(_context);
        }

        public void Enable()
        {
            // 有効化のたびに起点選択をリセットする
            // Reset the origin selection each time the tool is enabled
            _sourceBlock = null;
        }

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // 起点未選択なら選択・切断、選択済みなら接続・延長を処理する
            // No origin: select or disconnect; with origin: connect or extend
            if (_sourceBlock == null)
            {
                _sourceBlock = _editMode.Update();
                return;
            }

            _extendMode.Update(context, _sourceBlock, newSource => _sourceBlock = newSource);
        }

        public void Disable()
        {
            // ツール切替時のみ起点を解除し、プレビューを消す
            // Release the origin only on tool switch, and hide previews
            _sourceBlock = null;
            _context.WirePreview.SetActive(false);
            _context.PreviewBlockController.SetActive(false);
        }
    }
}
