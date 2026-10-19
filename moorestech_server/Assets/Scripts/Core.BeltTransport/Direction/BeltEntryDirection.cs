namespace Core.BeltTransport
{
    // 現在マスから見た搬入元。前+Z・後-Z・左-X・右+X、Aboveは1マス上(+Y)、Belowは1マス下(-Y)
    // 値0～11は保存・通信時に4bitへ収まる。真上・真下だけの移動は含めない
    // Source cell seen from the current cell. Front=+Z, Back=-Z, Left=-X, Right=+X, Above=+Y one cell, Below=-Y one cell
    // Values 0..11 fit in 4 bits for save/network. Pure vertical moves are not represented
    public enum BeltEntryDirection : byte
    {
        FromFront = 0,
        FromBack = 1,
        FromLeft = 2,
        FromRight = 3,
        FromFrontAbove = 4,
        FromBackAbove = 5,
        FromLeftAbove = 6,
        FromRightAbove = 7,
        FromFrontBelow = 8,
        FromBackBelow = 9,
        FromLeftBelow = 10,
        FromRightBelow = 11
    }
}
