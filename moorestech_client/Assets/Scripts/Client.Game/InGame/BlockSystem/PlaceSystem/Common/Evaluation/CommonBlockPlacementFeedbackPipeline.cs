using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.GearConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.Block.Interface;
using Game.Construction;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.Evaluation
{
    /// <summary>
    ///     設置列の制限と接続プレビューを評価
    ///     Evaluates run restrictions and connection previews
    /// </summary>
    public class CommonBlockPlacementFeedbackPipeline
    {
        private readonly IPlacementPreviewBlockGameObjectController _preview;
        private readonly MapVeinAabbRegistry _veins;
        private readonly VeinRestrictedPlacementState _veinRestriction;
        private readonly ChainPlacePreviewState _chainState;
        private readonly CommonBlockPlacePointCalculator _calculator;
        private readonly IChainGroundQuery _chainGround;
        private readonly ConstructionWalletQuery _wallet;
        private readonly ILocalPlayerInventory _inventory;
        private readonly ElectricWireAutoConnectPreview _wires;
        private readonly GearConnectPreview _gears;
        private readonly ChainPlacementPreviewPart _chainPreview;

        public CommonBlockPlacementFeedbackPipeline(IPlacementPreviewBlockGameObjectController preview, MapVeinAabbRegistry veins, VeinRestrictedPlacementState veinRestriction, ChainPlacePreviewState chainState, CommonBlockPlacePointCalculator calculator, IChainGroundQuery chainGround, ConstructionWalletQuery wallet, ILocalPlayerInventory inventory, ElectricWireAutoConnectPreview wires, GearConnectPreview gears, ChainPlacementPreviewPart chainPreview)
        {
            _preview = preview;
            _veins = veins;
            _veinRestriction = veinRestriction;
            _chainState = chainState;
            _calculator = calculator;
            _chainGround = chainGround;
            _wallet = wallet;
            _inventory = inventory;
            _wires = wires;
            _gears = gears;
            _chainPreview = chainPreview;
        }

        public bool Apply(List<PlaceInfo> cells, BlockMasterElement master, BlockId blockId, BlockDirection direction, int cursorIndex, PlacementHitSurfaceKind surfaceKind, int heightOffset, PlacementFeedback feedback)
        {
            // 制限を資材判定より先に掛け、置けないセルが枠を消費しないようにする
            // Apply restrictions before material checks so blocked cells consume no quota
            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(cells, master, cursorIndex, _veins, _veinRestriction, feedback);
            ChainPlacementReporter.MarkChainBlockedCellsAsNotPlaceable(cells, master, cursorIndex, _chainState, _calculator, _chainGround, surfaceKind == PlacementHitSurfaceKind.Ground, heightOffset, feedback);
            ConstructionMaterialShortageReporter.ReportShortages(cells, blockId, _wallet, _inventory, feedback);
            ConstructionCostPreviewMarker.MarkUnaffordableCellsAsNotPlaceable(cells, blockId, _wallet, _inventory);

            // 接続評価を最終色へ反映
            // Apply connection results to final colors
            var wirePlaceable = _wires.ApplyAutoConnect(cells, blockId, direction, _inventory, cursorIndex, feedback);
            _gears.Apply(cells, blockId, cursorIndex);
            _chainPreview.Apply(cells[cursorIndex], master, surfaceKind == PlacementHitSurfaceKind.Ground, heightOffset);
            _preview.UpdatePlaceableColors(cells);
            return wirePlaceable;
        }
    }
}
