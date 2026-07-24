using System.Collections.Generic;
using Game.Map.Interface.Json;
using Game.MapGeneration.Pipeline;

namespace Game.MapGeneration.Export
{
    // 生成パイプライン出力をmap.jsonのDTO(MapInfoJson)へ変換する。instanceIdはここで0から連番採番する。
    // Converts pipeline output into the map.json DTO; instanceIds are assigned sequentially from 0 here.
    public static class MapInfoJsonBuilder
    {
        public static MapInfoJson Build(MapGenerationOutput output)
        {
            return new MapInfoJson
            {
                DefaultSpawnPointJson = BuildSpawnPoint(output),
                MapObjects = BuildMapObjects(output),
                MapVeins = BuildMapVeins(output),
            };

            #region Internal

            static SpawnPointJson BuildSpawnPoint(MapGenerationOutput output)
            {
                return new SpawnPointJson
                {
                    X = output.SpawnPoint.x,
                    Y = output.SpawnPoint.y,
                    Z = output.SpawnPoint.z,
                };
            }

            static List<MapObjectInfoJson> BuildMapObjects(MapGenerationOutput output)
            {
                var mapObjects = new List<MapObjectInfoJson>(output.MapObjects.Count);
                for (var i = 0; i < output.MapObjects.Count; i++)
                {
                    var placed = output.MapObjects[i];
                    mapObjects.Add(new MapObjectInfoJson
                    {
                        InstanceId = i,
                        MapObjectGuidStr = placed.MapObjectGuid,
                        X = placed.Position.x,
                        Y = placed.Position.y,
                        Z = placed.Position.z,
                    });
                }
                return mapObjects;
            }

            static List<MapVeinInfoJson> BuildMapVeins(MapGenerationOutput output)
            {
                var mapVeins = new List<MapVeinInfoJson>(output.ItemVeins.Count);
                foreach (var vein in output.ItemVeins)
                {
                    mapVeins.Add(new MapVeinInfoJson
                    {
                        VeinGuidStr = vein.VeinGuid,
                        MinX = vein.Min.x,
                        MinY = vein.Min.y,
                        MinZ = vein.Min.z,
                        MaxX = vein.Max.x,
                        MaxY = vein.Max.y,
                        MaxZ = vein.Max.z,
                    });
                }
                return mapVeins;
            }

            #endregion
        }
    }
}
