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
            
            return new VanillaStaticMapObject(id,type,false,position,itemId); 
        }

        public IMapObject Create(int instanceId, string type, ServerVector3 position, bool isDestroyed)
        {
            var itemId = GetItemIdFromType(type);
            return new VanillaStaticMapObject(instanceId,type,isDestroyed,position,itemId);
        }
        
        //TODO コンフィグを参照するようにする
        private int GetItemIdFromType(string type)
        {
            return type switch
            {
                VanillaMapObjectType.VanillaTree => _itemConfig.GetItemId("moorestechBaseMod", "VanillaTree"),
                VanillaMapObjectType.VanillaPebble => _itemConfig.GetItemId("moorestechBaseMod", "VanillaPebble"),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}