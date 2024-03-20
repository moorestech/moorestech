namespace Game.Block.Component.IOConnector
{
    /// <summary>
    ///     入力位置と出力位置を指定するクラス
    ///     北向きを基準として、入出力方向を指定する
    /// </summary>
    public class IOConnectionSetting
    {
        public readonly string[] ConnectableBlockType;
        public readonly ConnectDirection[] InputConnector;
        public readonly ConnectDirection[] OutputConnector;

        public IOConnectionSetting(ConnectDirection[] inputConnector, ConnectDirection[] outputConnector, string[] connectableBlockType)
        {
            InputConnector = inputConnector;
            OutputConnector = outputConnector;
            ConnectableBlockType = connectableBlockType;
        }
    }
}