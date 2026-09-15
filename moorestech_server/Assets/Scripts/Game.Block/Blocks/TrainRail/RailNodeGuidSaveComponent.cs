using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Block.Blocks.TrainRail
{
    /// <summary>
    /// ブロックが持つRailComponentのノードGUIDを保存するコンポーネント
    /// Component that persists the node guids of the RailComponents owned by a block
    /// </summary>
    public class RailNodeGuidSaveComponent : IBlockSaveState
    {
        private readonly RailComponent[] _railComponents;

        public static string SaveKeyStatic { get; } = typeof(RailNodeGuidSaveComponent).FullName;
        public string SaveKey { get; } = SaveKeyStatic;
        public bool IsDestroy { get; private set; }

        public RailNodeGuidSaveComponent(RailComponent[] railComponents)
        {
            _railComponents = railComponents;
        }

        public void Destroy()
        {
            IsDestroy = true;
        }

        // ノードGUIDはブロックが所有する識別子。保存しないと復元時に採番し直し、ロード中の乱数列が保存時とずれる
        // The node guids are identifiers the block owns; without them a restore re-draws and load desynchronizes the random stream
        public object GetSaveState()
        {
            var nodes = new List<RailNodeGuidPairJsonObject>(_railComponents.Length);
            foreach (var railComponent in _railComponents)
            {
                nodes.Add(new RailNodeGuidPairJsonObject { FrontNodeGuid = railComponent.FrontNode.Guid, BackNodeGuid = railComponent.BackNode.Guid });
            }
            return new RailNodeGuidSaveJsonObject { Nodes = nodes };
        }

        // 期待本数と食い違うセーブは復元しない。足りない分を採番で埋めるとロード中に乱数列が進む
        // A save whose count disagrees is not restored; filling the gap by drawing would advance the random stream during load
        public static List<RailNodeGuidPairJsonObject> LoadNodeGuids(Dictionary<string, object> componentStates, int expectedRailComponentCount)
        {
            var saveData = BlockComponentStateReader.Read<RailNodeGuidSaveJsonObject>(componentStates, SaveKeyStatic);
            var nodes = saveData.Nodes;
            if (nodes != null && nodes.Count == expectedRailComponentCount) return nodes;

            var reason = $"セーブのレールノードGUIDが期待本数と違います 期待:{expectedRailComponentCount} 実際:{nodes?.Count}。現在のマイグレーション連鎖は本数の食い違いを補わないため、この版のセーブはロードできません。補う ISaveMigrationStep を Game.SaveLoad/Migration/Steps へ足せば救えます";
            Debug.LogError(reason);
            throw new InvalidOperationException(reason);
        }
    }

    public class RailNodeGuidSaveJsonObject
    {
        [JsonProperty("nodes")] public List<RailNodeGuidPairJsonObject> Nodes;
    }

    public class RailNodeGuidPairJsonObject
    {
        [JsonProperty("frontNodeGuid")] public Guid FrontNodeGuid;
        [JsonProperty("backNodeGuid")] public Guid BackNodeGuid;
    }
}
