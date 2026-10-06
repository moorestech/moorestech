using System;
using System.Collections.Generic;
using Client.Common.Asset;
using Mooresmaster.Model.MapModule;
using UnityEngine;

namespace Client.Game.InGame.Map.Outcrop
{
    public sealed class OutcropPrefabCache
    {
        private readonly Dictionary<string, GameObject> _prefabCacheByAddress = new();

        public GameObject Resolve(Guid veinGuid, MapVeinMasterElement element)
        {
            var address = element.OutcropAddressablePath;
            if (_prefabCacheByAddress.TryGetValue(address, out var cachedPrefab)) return cachedPrefab;

            // 同一アドレスの失敗も保存し再試行を抑える
            // Cache failed addresses too to avoid repeated loads
            var loaded = AddressableLoader.LoadDefault<GameObject>(address);
            if (loaded == null)
                Debug.LogError($"[OutcropGameObjectDatastore] 露頭プレハブをロードできません VeinGuid:{veinGuid} VeinName:{element.VeinName} Address:{address}");

            _prefabCacheByAddress[address] = loaded;
            return loaded;
        }
    }
}
