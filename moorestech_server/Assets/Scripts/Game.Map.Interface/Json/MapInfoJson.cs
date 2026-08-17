using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Map.Interface.Json
{
    public class MapInfoJson
    {
        [JsonProperty("defaultSpawnPoint")] public SpawnPointJson DefaultSpawnPointJson;
        [JsonProperty("mapObjects")] public List<MapObjectInfoJson> MapObjects;
        [JsonProperty("mapVeins")] public List<MapVeinInfoJson> MapVeins;
    }

    public class MapObjectInfoJson
    {
        [JsonProperty("instanceId")] public int InstanceId;
        [JsonProperty("mapObjectGuid")] public string MapObjectGuidStr;
        [JsonIgnore] public Guid MapObjectGuid => new(MapObjectGuidStr);

        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;

        [JsonIgnore] public Vector3 Position => new(X, Y, Z);

        // 姿勢はクォータニオンの4成分で持つ。オイラー角に落とすと同じ向きに複数の表現ができて往復で値が動く
        // The rotation is kept as the quaternion's four components; euler angles would give one orientation several spellings and drift on a round trip
        //
        // 姿勢・スケール・クラスターの10キーはRequired.Always。欠けたまま0で読むと全個体がscale0の不可視かつ不正回転になり、
        // Import→Exportでその0が正本へ焼き付く。旧形式は無言で通さず読み込み時点で落とす
        // The ten rotation/scale/cluster keys are Required.Always: reading them as a missing 0 makes every instance invisible with an
        // invalid rotation, and an Import->Export bakes that 0 into the source file, so an older format must fail at load instead of passing silently
        [JsonProperty("rotationX", Required = Required.Always)] public float RotationX;
        [JsonProperty("rotationY", Required = Required.Always)] public float RotationY;
        [JsonProperty("rotationZ", Required = Required.Always)] public float RotationZ;
        [JsonProperty("rotationW", Required = Required.Always)] public float RotationW;

        [JsonIgnore] public Quaternion Rotation => new(RotationX, RotationY, RotationZ, RotationW);

        [JsonProperty("scaleX", Required = Required.Always)] public float ScaleX;
        [JsonProperty("scaleY", Required = Required.Always)] public float ScaleY;
        [JsonProperty("scaleZ", Required = Required.Always)] public float ScaleZ;

        [JsonIgnore] public Vector3 Scale => new(ScaleX, ScaleY, ScaleZ);

        // 岩クラスターの識別子と重心XZ。-1 は独立配置で、そのとき重心は (0,0) の未使用値
        // Rock cluster identifier plus its centroid XZ; -1 is an independent placement whose centroid stays an unused (0,0)
        [JsonProperty("clusterId", Required = Required.Always)] public int ClusterId;
        [JsonProperty("clusterCenterX", Required = Required.Always)] public float ClusterCenterX;
        [JsonProperty("clusterCenterZ", Required = Required.Always)] public float ClusterCenterZ;
    }

    // 鉱脈配置1件。item/fluidの区別はveinGuid→MapVeinMasterのveinTypeから導出し、jsonには保存しない
    // A single vein placement; item/fluid distinction is derived from MapVeinMaster via veinGuid, not stored here
    public class MapVeinInfoJson
    {
        [JsonProperty("veinGuid")] public string VeinGuidStr;
        [JsonIgnore] public Guid VeinGuid => Guid.Parse(VeinGuidStr);

        [JsonIgnore] public Vector3Int MinPosition => new(MinX, MinY, MinZ);
        [JsonProperty("minX")] public int MinX;
        [JsonProperty("minY")] public int MinY;
        [JsonProperty("minZ")] public int MinZ;

        [JsonIgnore] public Vector3Int MaxPosition => new(MaxX, MaxY, MaxZ);
        [JsonProperty("maxX")] public int MaxX;
        [JsonProperty("maxY")] public int MaxY;
        [JsonProperty("maxZ")] public int MaxZ;
    }

    public class SpawnPointJson
    {
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;

        [JsonIgnore] public Vector3 Position => new(X, Y, Z);
    }
}
