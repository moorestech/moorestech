using System;
using Core.Item.Config;
using Game.Base;
using Game.MapObject.Interface;

namespace Game.MapObject
{
    public class MapObjectFactory : IMapObjectFactory
    {
        private readonly IItemConfig _itemConfig;

        private readonly Random _random = new();

        public MapObjectFactory(IItemConfig itemConfig)
        {
            _itemConfig = itemConfig;
        }

        public IMapObject Create(int instanceId, string type, ServerVector3 position, bool isDestroyed)
        {
            var (itemId, itemCount) = GetItemIdAndCount(type);
            return new VanillaStaticMapObject(instanceId, type, isDestroyed, position, itemId, itemCount);
        }

        public IMapObject Create(string type, ServerVector3 position)
        {
            //TODO mapseed
            var id = new Random().Next();
            var (itemId, itemCount) = GetItemIdAndCount(type);

            return new VanillaStaticMapObject(id, type, false, position, itemId, 1);
        }

        //TODO 
        private (int itemId, int count) GetItemIdAndCount(string type)
        {
            return type switch
            {
                VanillaMapObjectType.VanillaTree => (_itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "wood"), _random.Next(2, 3)),
                VanillaMapObjectType.VanillaBigTree => (_itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "wood"), _random.Next(10, 15)),
                VanillaMapObjectType.VanillaStone => (_itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "stone"), _random.Next(1, 3)),
                VanillaMapObjectType.VanillaBush => (_itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "cotton"), _random.Next(1, 2)),
                VanillaMapObjectType.VanillaCray => (_itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "clay"), _random.Next(1, 2)),
                VanillaMapObjectType.VanillaCoal => (_itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "coal"), _random.Next(1, 2)),
                VanillaMapObjectType.VanillaIronOre => (_itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "iron ore"), _random.Next(1, 2)),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}