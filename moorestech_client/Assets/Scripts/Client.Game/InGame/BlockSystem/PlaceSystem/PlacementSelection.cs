using System;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public enum PlacementSelectionType
    {
        None,
        Block,
        TrainCar,
        ConnectTool,
        Blueprint,
        BlueprintCopy,
    }

    /// <summary>
    /// ビルドメニューで選択中の設置対象（ブロック・車両・接続ツール）
    /// The build-menu selection: a block, a train car, or a connect tool
    /// </summary>
    public class PlacementSelection
    {
        public PlacementSelectionType SelectionType { get; private set; } = PlacementSelectionType.None;
        public BlockId? SelectedBlockId { get; private set; }
        public Guid SelectedTrainCarGuid { get; private set; }
        public string SelectedConnectPlaceMode { get; private set; }
        public string SelectedBlueprintName { get; private set; }

        public void SetSelectedBlock(BlockId blockId)
        {
            ClearSelection();
            SelectionType = PlacementSelectionType.Block;
            SelectedBlockId = blockId;
        }

        public void SetSelectedTrainCar(Guid trainCarGuid)
        {
            ClearSelection();
            SelectionType = PlacementSelectionType.TrainCar;
            SelectedTrainCarGuid = trainCarGuid;
        }

        public void SetSelectedConnectTool(string placeMode)
        {
            ClearSelection();
            SelectionType = PlacementSelectionType.ConnectTool;
            SelectedConnectPlaceMode = placeMode;
        }

        public void SetSelectedBlueprint(string blueprintName)
        {
            ClearSelection();
            SelectionType = PlacementSelectionType.Blueprint;
            SelectedBlueprintName = blueprintName;
        }

        public void SetSelectedBlueprintCopyTool()
        {
            ClearSelection();
            SelectionType = PlacementSelectionType.BlueprintCopy;
        }

        public void ClearSelection()
        {
            SelectionType = PlacementSelectionType.None;
            SelectedBlockId = null;
            SelectedTrainCarGuid = Guid.Empty;
            SelectedConnectPlaceMode = null;
            SelectedBlueprintName = null;
        }
    }
}
