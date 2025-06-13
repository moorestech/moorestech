using Game.Block.Interface;

namespace Game.Train.Train
{
    public class TrainCar
    {
        const int WHEIGHT_PER_SLOT = 40;
        const int FUEL_WEIGHT_PER_SLOT = 40;
        const int DEFAULT_WEIGHT = 120;
        const int DEFAULT_TRACTION = 100;
        // 駆動力 (動力車での推進力、貨車では0)
        public int TractionForce { get; private set; }

        // インベントリスロット数 (貨車での容量、動力車では0)
        public int InventorySlots { get; private set; }

        // 燃料のインベントリスロット数 (動力車での燃料容量、貨車では0)
        public int FuelSlots { get; private set; }

        public int Length { get; private set; }
        public IBlock dockingblock { get; set; }// このTrainCarがcargoやstation駅blockでドッキングしているときにのみ非nullになる

        public TrainCar(int tractionForce, int inventorySlots, int length)
        {
            TractionForce = tractionForce;
            InventorySlots = inventorySlots;
            Length = length;
            dockingblock = null;
        }

        //重さ、推進力を得る
        public (int,int) GetWeightAndTraction()
        {
            return (DEFAULT_WEIGHT +
                InventorySlots * WHEIGHT_PER_SLOT +
                FuelSlots * FUEL_WEIGHT_PER_SLOT
                , TractionForce * DEFAULT_TRACTION);
        }

    }

}
