using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.MapModule;
using Game.Block.Interface;
using Game.Map.Interface.MapObject;
using Game.Map.Interface.Vein;
using UnityEngine;

namespace Game.Block.Blocks.TrainRail.Naming
{
    // 駅の周辺から番号なしの既定名を決める（鉱脈→地物の採掘アイテム→代替）
    // Decide the unnumbered default name around a station (vein, then mined item of map objects, then fallback)
    public class TrainStationNameFeatureScanner
    {
        public const float ScanRadius = 32f;
        private const string FallbackName = "Station";

        private readonly IItemMapVeinDatastore _itemVeins;
        private readonly IFluidMapVeinDatastore _fluidVeins;
        private readonly IMapObjectDatastore _mapObjects;
        private readonly TrainStationFeatureEnglishNames _names;

        public TrainStationNameFeatureScanner(IItemMapVeinDatastore itemVeins, IFluidMapVeinDatastore fluidVeins, IMapObjectDatastore mapObjects, TrainStationFeatureEnglishNames names)
        {
            _itemVeins = itemVeins;
            _fluidVeins = fluidVeins;
            _mapObjects = mapObjects;
            _names = names;
        }

        public string ScanBaseName(BlockPositionInfo position)
        {
            var center = new Vector2((position.MinPos.x + position.MaxPos.x + 1) * 0.5f, (position.MinPos.z + position.MaxPos.z + 1) * 0.5f);
            var featureName = FindNearestVeinName() ?? FindMostMinedItemName();
            return featureName == null ? FallbackName : $"{featureName} {FallbackName}";

            #region Internal

            string FindNearestVeinName()
            {
                // 最も近い鉱脈。同距離は先に見つけた方（アイテム→液体の順）
                // Nearest vein; ties keep the first found (items before fluids)
                string best = null;
                var bestDistance = float.MaxValue;
                foreach (var vein in _itemVeins.Veins) Consider(_names.GetItemName(MasterHolder.ItemMaster.GetItemGuid(vein.VeinItemId)), vein.VeinRangeMin, vein.VeinRangeMax);
                foreach (var vein in _fluidVeins.Veins) Consider(_names.GetFluidName(MasterHolder.FluidMaster.GetFluidGuid(vein.VeinFluidId)), vein.VeinRangeMin, vein.VeinRangeMax);
                return best;

                void Consider(string name, Vector3Int min, Vector3Int max)
                {
                    var distance = HorizontalDistanceToCells(min, max);
                    if (ScanRadius < distance || bestDistance <= distance) return;
                    best = name;
                    bestDistance = distance;
                }
            }

            string FindMostMinedItemName()
            {
                // 採掘可能な地物を最初の採掘アイテムの名前で数え、最多（同数は名前の序数順で先）を選ぶ
                // Count minable map objects by their first earned item's name; pick the most frequent (ties by ordinal name)
                var counts = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
                var min = new Vector3(center.x - ScanRadius, 0, center.y - ScanRadius);
                var max = new Vector3(center.x + ScanRadius, 0, center.y + ScanRadius);
                foreach (var mapObject in _mapObjects.GetWithinBoundingBox(min, max))
                {
                    if (mapObject.IsDestroyed) continue;
                    if (ScanRadius < Vector2.Distance(center, new Vector2(mapObject.Position.x, mapObject.Position.z))) continue;
                    var element = MasterHolder.MapObjectMaster.GetMapObjectElement(mapObject.MapObjectGuid);
                    if (MapObjectMaster.IsDecoration(element) || element.MiningParam is not IMinableMapObjectParam minable || minable.EarnItems.items.Length == 0) continue;
                    var name = _names.GetItemName(minable.EarnItems.items[0].ItemGuid);
                    counts[name] = counts.TryGetValue(name, out var count) ? count + 1 : 1;
                }

                string best = null;
                var bestCount = 0;
                foreach (var (name, count) in counts)
                {
                    if (count <= bestCount) continue;
                    best = name;
                    bestCount = count;
                }
                return best;
            }

            float HorizontalDistanceToCells(Vector3Int min, Vector3Int max)
            {
                var dx = Mathf.Max(min.x - center.x, 0, center.x - (max.x + 1));
                var dz = Mathf.Max(min.z - center.y, 0, center.y - (max.z + 1));
                return Mathf.Sqrt(dx * dx + dz * dz);
            }

            #endregion
        }
    }
}
