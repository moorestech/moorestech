using System;
using System.Collections.Generic;
using Client.Common.Asset;
using Game.MapGeneration.Surface;
using Mooresmaster.Model.MapModule;
using UnityEngine;

namespace Client.Game.InGame.Map.Outcrop
{
    internal sealed class OutcropPrefabCache
    {
        // 複数鉱脈が同じprefabを共有するので、ロードと接地契約の検査はアドレス単位で1度だけ行う
        // Several veins share one prefab, so loading and the grounding-contract check happen once per address
        private readonly Dictionary<string, OutcropPrefab> _prefabCacheByAddress = new();

        public OutcropPrefab Resolve(Guid veinGuid, MapVeinMasterElement element, TerrainSurfacePresentation presentation)
        {
            var address = element.OutcropAddressablePath;
            if (_prefabCacheByAddress.TryGetValue(address, out var cachedPrefab)) return cachedPrefab;

            // ロード失敗は例外にせずログで縮退する。1本の失敗で全鉱脈の露頭生成を巻き添えにしない
            // A load failure degrades with a log instead of throwing, so one failure never drags every vein's outcrop down with it
            // 同一アドレスの失敗も保存し再試行を抑える
            // Cache failed addresses too to avoid repeated loads
            var loaded = AddressableLoader.LoadDefault<GameObject>(address);
            if (loaded == null)
            {
                Debug.LogError($"[OutcropPrefabCache] 露頭プレハブをロードできません VeinGuid:{veinGuid} VeinName:{element.VeinName} Address:{address}");
                _prefabCacheByAddress[address] = null;
                return null;
            }

            var outcrop = OutcropPrefab.Create(loaded, presentation);
            _prefabCacheByAddress[address] = outcrop;
            return outcrop;
        }
    }
}
