namespace Game.PlayerInventory.Interface.Event
{
    public class EquipmentSelectedIndexUpdateEventProperties
    {
        public readonly int PlayerId;

        public readonly int SelectedEquipmentIndex;

        public EquipmentSelectedIndexUpdateEventProperties(int playerId, int selectedEquipmentIndex)
        {
            PlayerId = playerId;
            SelectedEquipmentIndex = selectedEquipmentIndex;
        }
    }
}
