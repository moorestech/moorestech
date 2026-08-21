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
                        RotationX = placed.Rotation.x,
                        RotationY = placed.Rotation.y,
                        RotationZ = placed.Rotation.z,
                        RotationW = placed.Rotation.w,
                        ScaleX = placed.Scale.x,
                        ScaleY = placed.Scale.y,
                        ScaleZ = placed.Scale.z,
                        ClusterId = placed.ClusterId,
                        ClusterCenterX = placed.ClusterCenter.x,
                        ClusterCenterZ = placed.ClusterCenter.y,
                    });
                }
                return mapObjects;
            }

            // ItemVeins/FluidVeinsを合流させて1本のmapVeins配列にする。item/fluidの区別はveinGuidから
            // MapVeinMasterのveinTypeで導出するため、ここでは種別を持たない（MapVeinInfoJsonのコメント参照）。
            // Merge ItemVeins/FluidVeins into one mapVeins array. Item/fluid is derived from MapVeinMaster
            // via veinGuid, so no kind is carried here (see MapVeinInfoJson's comment).
            static List<MapVeinInfoJson> BuildMapVeins(MapGenerationOutput output)
            {
                var mapVeins = new List<MapVeinInfoJson>(output.ItemVeins.Count + output.FluidVeins.Count);
                AppendVeins(mapVeins, output.ItemVeins);
                AppendVeins(mapVeins, output.FluidVeins);
                return mapVeins;
            }

            static void AppendVeins(List<MapVeinInfoJson> mapVeins, List<PlacedVein> veins)
            {
                foreach (var vein in veins)
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
            }

            #endregion
        }
    }
}
