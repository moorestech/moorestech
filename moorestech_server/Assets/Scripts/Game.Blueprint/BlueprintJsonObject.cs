using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Blueprint
{
    public class BlueprintJsonObject
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("blocks")] public List<BlueprintBlockJsonObject> Blocks;
        [JsonProperty("wires", Required = Required.Always)] public List<BlueprintLineJsonObject> Wires;
        [JsonProperty("chains", Required = Required.Always)] public List<BlueprintLineJsonObject> Chains;

        // BlockGuidStr/BlockGuidと同形式。識別子はGuidに一本化し、名前は表示専用
        // Same shape as BlockGuidStr/BlockGuid; the identity is unified to a GUID and the name is display-only
        [JsonProperty("guid")] public string BlueprintGuidStr { get; private set; }
        [JsonIgnore] public Guid BlueprintGuid => string.IsNullOrEmpty(BlueprintGuidStr) ? Guid.Empty : Guid.Parse(BlueprintGuidStr);

        public BlueprintJsonObject()
        {
            Blocks = new List<BlueprintBlockJsonObject>();
            Wires = new List<BlueprintLineJsonObject>();
            Chains = new List<BlueprintLineJsonObject>();
        }

        public BlueprintJsonObject(string name, List<BlueprintBlockJsonObject> blocks, List<BlueprintLineJsonObject> wires, List<BlueprintLineJsonObject> chains, Guid blueprintGuid)
        {
            Name = name;
            Blocks = blocks;
            Wires = wires;
            Chains = chains;
            BlueprintGuidStr = blueprintGuid.ToString();
        }
    }

    public class BlueprintBlockJsonObject
    {
        // 外接箱の最小角からの相対座標
        // Coordinates relative to the extent minimum corner.
        [JsonProperty("offsetX")] public int OffsetX;
        [JsonProperty("offsetY")] public int OffsetY;
        [JsonProperty("offsetZ")] public int OffsetZ;

        [JsonProperty("blockGuid")] public string BlockGuidStr;
        [JsonIgnore] public Guid BlockGuid => Guid.Parse(BlockGuidStr);

        [JsonProperty("direction")] public int Direction;

        // 設定キー→設定JSON（可読形式）。実行時状態は含まない
        // Settings key to readable settings JSON; runtime state excluded
        [JsonProperty("settings")] public Dictionary<string, string> Settings;

        [JsonIgnore] public Vector3Int Offset => new(OffsetX, OffsetY, OffsetZ);

        public BlueprintBlockJsonObject()
        {
            Settings = new Dictionary<string, string>();
        }

        public BlueprintBlockJsonObject(Vector3Int offset, string blockGuidStr, int direction, Dictionary<string, string> settings)
        {
            OffsetX = offset.x;
            OffsetY = offset.y;
            OffsetZ = offset.z;
            BlockGuidStr = blockGuidStr;
            Direction = direction;
            Settings = settings;
        }
    }

    public class BlueprintLineJsonObject
    {
        // 端点はBP内ブロックのindex。座標は貼り付け時に回転後の位置から解決する
        // Endpoints are block indices inside the blueprint; positions are resolved after rotation at paste time
        [JsonProperty("blockIndexA")] public int BlockIndexA;
        [JsonProperty("blockIndexB")] public int BlockIndexB;
        [JsonProperty("connectToolGuid")] public string ConnectToolGuidStr;
        [JsonIgnore] public Guid ConnectToolGuid => Guid.Parse(ConnectToolGuidStr);

        public BlueprintLineJsonObject() { }

        public BlueprintLineJsonObject(int blockIndexA, int blockIndexB, Guid connectToolGuid)
        {
            BlockIndexA = blockIndexA;
            BlockIndexB = blockIndexB;
            ConnectToolGuidStr = connectToolGuid.ToString();
        }
    }
}
