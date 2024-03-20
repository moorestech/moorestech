using System.Collections.Generic;
using Game.Block;
using Game.Block.Component.IOConnector;

namespace Game.World.EventHandler.InventoryEvent
{
    /// <summary>
    ///     ベルトコンベアや機械などのインベントリのあるブロックが、どの方角にあるブロックとつながるかを指定するクラス
    ///     北向きを基準として、つながる方向を指定する
    /// </summary>
    public static class VanillaBlockInventoryConnectionData
    {
        public static readonly Dictionary<string, IOConnectionSetting> IOConnectionData = new()
        {
            {
                VanillaBlockType.Machine,
                new IOConnectionSetting(
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.Chest,
                new IOConnectionSetting(
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.Generator,
                new IOConnectionSetting(
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new ConnectDirection[] { },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.Miner,
                new IOConnectionSetting(
                    new ConnectDirection[] { },
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.BeltConveyor, new IOConnectionSetting(
                    // 南、西、東をからの接続を受け、アイテムをインプットする
                    new ConnectDirection[] { new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    //北向きに出力する
                    new ConnectDirection[] { new(1, 0, 0) },
                    new[]
                    {
                        VanillaBlockType.Machine, VanillaBlockType.Chest, VanillaBlockType.Generator,
                        VanillaBlockType.Miner, VanillaBlockType.BeltConveyor
                    })
            }
        };
    }
}