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

        public BlueprintJsonObject()
        {
            Blocks = new List<BlueprintBlockJsonObject>();
        }

        public BlueprintJsonObject(string name, List<BlueprintBlockJsonObject> blocks)
        {
            Name = name;
            Blocks = blocks;
        }
    }

    public class BlueprintBlockJsonObject
    {
        // アンカー（選択ボックスXZ中心・ボックス最下段Y）からの相対オフセット
        // Offset relative to the anchor (rect XZ center, lowest Y)
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
}
