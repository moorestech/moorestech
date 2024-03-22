using System.Collections.Generic;
using System.Linq;
using Game.Block.BlockInventory;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.Block.Component.IOConnector
{
    public class IOConnectorUtil
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