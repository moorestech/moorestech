namespace Game.Train.Train
{
    public class TrainCar
    {
        // 駆動力 (動力車での推進力、貨車では0)
        public int TractionForce { get; private set; }

        // インベントリスロット数 (貨車での容量、動力車では0?)
        public int InventorySlots { get; private set; }

        public int length { get; private set; }

        public TrainCar(int tractionForce, int inventorySlots)
        {
            TractionForce = tractionForce;
            InventorySlots = inventorySlots;
        }

    }

}
