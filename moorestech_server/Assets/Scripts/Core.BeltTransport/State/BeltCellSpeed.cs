namespace Core.BeltTransport
{
    public readonly struct BeltCellSpeed
    {
        public readonly int CellId, Speed;
        public BeltCellSpeed(int cellId, int speed) { CellId = cellId; Speed = speed; }
    }
}
