using Mooresmaster.Model.BlocksModule;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange
{
    /// <summary>
    /// 相手種別（電柱/機械）ごとの接続範囲ボックス寸法
    /// Connection range box sizes per target kind (pole / machine)
    /// </summary>
    public readonly struct ConnectionRangeProfile
    {
        public readonly int HorizontalAgainstPole;
        public readonly int HeightAgainstPole;
        public readonly int HorizontalAgainstMachine;
        public readonly int HeightAgainstMachine;

        public ConnectionRangeProfile(int horizontalAgainstPole, int heightAgainstPole, int horizontalAgainstMachine, int heightAgainstMachine)
        {
            HorizontalAgainstPole = horizontalAgainstPole;
            HeightAgainstPole = heightAgainstPole;
            HorizontalAgainstMachine = horizontalAgainstMachine;
            HeightAgainstMachine = heightAgainstMachine;
        }

        // 電柱: 対電柱と対機械で別ボックスを持つ
        // Pole: separate boxes against poles and against machines
        public static ConnectionRangeProfile CreatePole(ElectricPoleBlockParam param)
        {
            return new ConnectionRangeProfile(param.PoleConnectionRange, param.PoleConnectionHeightRange, param.MachineConnectionRange, param.MachineConnectionHeightRange);
        }

        // 機械: 相手種別によらず単一ボックス
        // Machine: a single box regardless of target kind
        public static ConnectionRangeProfile CreateUniform(int connectionRange, int connectionHeightRange)
        {
            return new ConnectionRangeProfile(connectionRange, connectionHeightRange, connectionRange, connectionHeightRange);
        }

        public (int Horizontal, int Height) GetRangeAgainst(bool targetIsPole)
        {
            return targetIsPole ? (HorizontalAgainstPole, HeightAgainstPole) : (HorizontalAgainstMachine, HeightAgainstMachine);
        }
    }
}
