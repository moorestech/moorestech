using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    public class SpaceElevatorDetailDto
    {
        public List<int> SatelliteInputSlots;
        public List<int> OutputSlots;
        public bool Connected;
        public int CablePoolAmount;
        public int CablesPerElevator;
        public float ClimbProgress;
        public string Status;
        public List<InputSlotBindingDto> SlotBindings;
    }
}
