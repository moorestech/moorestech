using System;
using Core.Item.Config;
using Game.Context;
using Game.Map.Interface;
using UnityEngine;
using Random = System.Random;

namespace Game.Map
{
    public class MapObjectFactory : IMapObjectFactory
    {
        private readonly Random _random = new();

        public IMapObject Create(int instanceId, string type, Vector3 position, bool isDestroyed)
        {
            var (itemId, itemCount) = GetItemIdAndCount(type);
            return new VanillaStaticMapObject(instanceId, type, isDestroyed, position, itemId, itemCount);
        }

        public IMapObject Create(string type, Vector3 position)
        {
            //TODO mapのseed値に対応させる
            var id = new Random().Next();
            var (itemId, itemCount) = GetItemIdAndCount(type);

            return new VanillaStaticMapObject(id, type, false, position, itemId, 1);
        }

        //TODO コンフィグを参照するようにする
        private (int itemId, int count) GetItemIdAndCount(string type)
        {
            var itemConfig = ServerContext.ItemConfig;
            return type switch
            {
                VanillaMapObjectType.VanillaTree => (itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "wood"),
                    _random.Next(2, 3)),
                VanillaMapObjectType.VanillaBigTree => (itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "wood"),
                    _random.Next(10, 15)),
                VanillaMapObjectType.VanillaStone => (itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "stone"),
                    _random.Next(1, 3)),
                VanillaMapObjectType.VanillaBush => (itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "cotton"),
                    _random.Next(1, 2)),
                VanillaMapObjectType.VanillaCray => (itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "clay"),
                    _random.Next(1, 2)),
                VanillaMapObjectType.VanillaCoal => (itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "coal"),
                    _random.Next(1, 2)),
                VanillaMapObjectType.VanillaIronOre => (
                    itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "iron ore"), _random.Next(1, 2)),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            };
        }
    }
}