using System;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public interface IPlaceSystem
    {
        public void Enable();

        public void ManualUpdate(PlaceSystemUpdateContext context);

        public void Disable();
    }

    public struct PlaceSystemUpdateContext
    {
        public readonly ItemId HoldingItemId;

        public readonly bool IsSelectSlotChanged;
        public readonly int PreviousSelectHotbarSlotIndex;
        public readonly int CurrentSelectHotbarSlotIndex;

        // ビルドメニューで選択中のブロック（未選択はnull）
        // The block selected in the build menu (null when nothing is selected)
        public readonly BlockId? SelectedBlockId;

        // ビルドメニューの選択種別と車両・接続ツールの選択値、選択変化フラグ
        // The build-menu selection type, train car / connect tool value, and change flag
        public readonly PlacementSelectionType SelectionType;
        public readonly Guid SelectedTrainCarGuid;
        public readonly string SelectedConnectPlaceMode;
        public readonly bool IsSelectionChanged;

        public PlaceSystemUpdateContext(ItemId holdingItemId, bool isSelectSlotChanged, int previousSelectHotbarSlotIndex, int currentSelectHotbarSlotIndex, BlockId? selectedBlockId, PlacementSelectionType selectionType, Guid selectedTrainCarGuid, string selectedConnectPlaceMode, bool isSelectionChanged)
        {
            HoldingItemId = holdingItemId;
            IsSelectSlotChanged = isSelectSlotChanged;
            PreviousSelectHotbarSlotIndex = previousSelectHotbarSlotIndex;
            CurrentSelectHotbarSlotIndex = currentSelectHotbarSlotIndex;
            SelectedBlockId = selectedBlockId;
            SelectionType = selectionType;
            SelectedTrainCarGuid = selectedTrainCarGuid;
            SelectedConnectPlaceMode = selectedConnectPlaceMode;
            IsSelectionChanged = isSelectionChanged;
        }
    }
}