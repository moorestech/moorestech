using System;
using Core.Item.Config;
using Game.Base;
using Game.MapObject.Interface;

namespace Game.MapObject
{
    public class MapObjectFactory : IMapObjectFactory
    {
        private readonly IItemConfig _itemConfig;

        public MapObjectFactory(IItemConfig itemConfig)
        {
            _itemConfig = itemConfig;
        }
        public IMapObject Create(string type, ServerVector3 position)
        {
            //TODO mapのseed値に対応させる
            var id = new Random().Next();
            var itemId = GetItemIdFromType(type);
            
            return new VanillaStaticMapObject(id,type,false,position,itemId,1); 
        }

        public IMapObject Create(int instanceId, string type, ServerVector3 position, bool isDestroyed)
        {
            var itemId = GetItemIdFromType(type);
            var itemCount = GetItemCountFromType(type);
            return new VanillaStaticMapObject(instanceId,type,isDestroyed,position,itemId,itemCount);
        }
        
        //TODO コンフィグを参照するようにする
        private int GetItemIdFromType(string type)
        {
            return type switch
            {
                VanillaMapObjectType.VanillaTree => _itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "wood"),
                VanillaMapObjectType.VanillaStone => _itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "stone"),
                VanillaMapObjectType.VanillaBush => _itemConfig.GetItemId("sakastudio:moorestechAlphaMod", "cotton"),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
        
        private Random _random = new();
        private int GetItemCountFromType(string type)
        {
            return type switch
            {
                VanillaMapObjectType.VanillaTree => _random.Next(4,6),
                VanillaMapObjectType.VanillaStone => _random.Next(1,3),
                VanillaMapObjectType.VanillaBush => _random.Next(2,4),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}