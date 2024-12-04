namespace Game.Train.Train
{
    public class TrainCar
    {
        // 列車種類ID（動力車、貨車の区別などに利用）
        public TrainCarType CarType { get; private set; }

        // 駆動力 (動力車での推進力、貨車では0)
        public int TractionForce { get; private set; }

        // インベントリスロット数 (貨車での容量、動力車では0?)
        public int InventorySlots { get; private set; }

        public TrainCar(TrainCarType carType, int tractionForce, int inventorySlots)
        {
            CarType = carType;
            TractionForce = tractionForce;
            InventorySlots = inventorySlots;
        }

    }

    public enum TrainCarType
    {
        Locomotive, // 動力車
        Freight     // 貨車
    }
}
