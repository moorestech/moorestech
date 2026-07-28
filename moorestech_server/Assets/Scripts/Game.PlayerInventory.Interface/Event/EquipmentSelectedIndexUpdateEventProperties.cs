namespace Game.PlayerInventory.Interface.Event
{
    public class EquipmentSelectedIndexUpdateEventProperties
    {
        public readonly int PlayerId;

        // -1は素手を表す
        // -1 means bare hands
        public readonly int SelectedEquipmentIndex;

        public EquipmentSelectedIndexUpdateEventProperties(int playerId, int selectedEquipmentIndex)
        {
            PlayerId = playerId;
            SelectedEquipmentIndex = selectedEquipmentIndex;
        }
    }
}
