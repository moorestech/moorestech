using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.UI.Inventory.Main;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 電線ツールの各モードで共有する依存をまとめて渡すためのコンテキスト
    /// Context bundling shared dependencies passed to each electric-wire tool mode
    /// </summary>
    public class ElectricWireToolContext
    {
        public readonly Camera MainCamera;
        public readonly IPlacementPreviewBlockGameObjectController PreviewBlockController;
        public readonly ILocalPlayerInventory Inventory;
        public readonly BlockGameObjectDataStore BlockDataStore;
        public readonly ElectricWireExtendPreviewObject WirePreview;
        public readonly ElectricWireExtendRequestSender RequestSender;
        public readonly ElectricWirePoleSelection PoleSelection;

        // GhostPartはこのcontext自身を必要とするため、構築完了後にSetPoleGhostPartで注入する
        // GhostPart needs a reference back to this context, so it is injected via SetPoleGhostPart after construction
        public ElectricWirePoleGhostPart PoleGhostPart { get; private set; }

        public ElectricWireToolContext(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, ILocalPlayerInventory inventory, BlockGameObjectDataStore blockDataStore, ElectricWireExtendPreviewObject wirePreview, ElectricWireExtendRequestSender requestSender, ElectricWirePoleSelection poleSelection)
        {
            MainCamera = mainCamera;
            PreviewBlockController = previewBlockController;
            Inventory = inventory;
            BlockDataStore = blockDataStore;
            WirePreview = wirePreview;
            RequestSender = requestSender;
            PoleSelection = poleSelection;
        }

        public void SetPoleGhostPart(ElectricWirePoleGhostPart poleGhostPart)
        {
            PoleGhostPart = poleGhostPart;
        }
    }
}
