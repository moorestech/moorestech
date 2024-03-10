using System.Collections.Generic;
using Game.Map.Interface.Json;
using Game.Map.Interface.Vein;
using UnityEngine;

namespace Game.Map
{
    public class MapVeinDatastore : IMapVeinDatastore
    {
        private readonly Dictionary<int, IMapVein> _mapVeins = new();

        public MapVeinDatastore(MapInfoJson mapInfoJson)
        {
            //configからmap obejctを生成
            foreach (var configMapObject in mapInfoJson.MapObjects)
            {
                var mapObject = _mapObjectFactory.Create(configMapObject.InstanceId, configMapObject.Type, configMapObject.Position, false);
                _mapVeins.Add(mapObject.InstanceId, mapObject);
                mapObject.OnDestroy += () => OnDestroyMapObject?.Invoke(mapObject);
            }
        }
        
        public List<IMapVein> GetOverVeins(Vector2Int pos)
        {
            
        }
    }
}