using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface.Config;
using Core.Master;
using Game.Map.Interface.Config;
using Newtonsoft.Json;

namespace Game.Map.Config
{
    public class MapObjectConfig : IMapObjectConfig
    {
        private readonly Dictionary<string, MapObjectConfigInfo> _mapObjectConfigInfos = new();
        
        public MapObjectConfig(MasterJsonFileContainer masterPath, IItemConfig itemConfig)
        {
            foreach (var json in masterPath.SortedMapObjectConfigJsonList)
            {
                var mapObjects = LoadMapObject(json, itemConfig);
                foreach (var mapObject in mapObjects) _mapObjectConfigInfos.Add(mapObject.Type, mapObject);
            }
        }
        
        public MapObjectConfigInfo GetConfig(string type)
        {
            if (_mapObjectConfigInfos.TryGetValue(type, out var configInfo)) return configInfo;
            //TODo ログ基盤に入れる
            throw new KeyNotFoundException($"コンフィグに指定されたMapObjectのタイプが存在しません 当該タイプ: {type}");
        }
        
        private List<MapObjectConfigInfo> LoadMapObject(string json, IItemConfig itemConfig)
        {
            var mapObjectConfigJsons = JsonConvert.DeserializeObject<List<MapObjectConfigJson>>(json);
            return mapObjectConfigJsons.Select(c => new MapObjectConfigInfo(c, itemConfig)).ToList();
        }
    }
}