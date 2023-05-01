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

        public IMapObject Create(string type, ServerVector3 position,bool isDestroyed)
        {
            //TODO seedに対応させる
            var id = new Random().Next();
            
            //TODO コンフィグに対応させる
            var itemName = type switch
            {
                VanillaMapObjectType.VanillaTree => "",
                VanillaMapObjectType.VanillaPebble => "",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            var itemId = _itemConfig.GetItemId("moorestechBaseMod", itemName);
            
            return new VanillaStaticMapObject(id,type,isDestroyed,position,itemId);
        }
    }
}