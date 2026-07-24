using System;
using System.Collections.Generic;
using Core.Master.Validator;
using Mooresmaster.Loader.MapModule;
using Mooresmaster.Model.MapModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    // 鉱脈（アイテム鉱脈・流体鉱脈）のマスタ。map.jsonのmapVeins配列をロードしveinGuid索引を保持する
    // Master for veins (item / fluid veins); loads map.json's mapVeins array and holds a veinGuid index
    public class MapVeinMaster : IMasterValidator
    {
        public readonly MapVeinMasterElement[] MapVeins;

        // veinGuid→要素の索引
        // veinGuid → element index
        private Dictionary<Guid, MapVeinMasterElement> _elementByGuid;

        public MapVeinMaster(JToken jToken)
        {
            MapVeins = MapLoader.Load(jToken).MapVeins;
        }

        public bool Validate(out string errorLogs)
        {
            return MapVeinMasterUtil.Validate(MapVeins, out errorLogs);
        }

        public void Initialize()
        {
            _elementByGuid = new Dictionary<Guid, MapVeinMasterElement>();
            foreach (var element in MapVeins)
            {
                _elementByGuid.Add(element.VeinGuid, element);
            }
        }

        public IReadOnlyList<MapVeinMasterElement> All => MapVeins;

        public MapVeinMasterElement GetElementOrNull(Guid veinGuid)
        {
            return _elementByGuid.GetValueOrDefault(veinGuid);
        }
    }
}
