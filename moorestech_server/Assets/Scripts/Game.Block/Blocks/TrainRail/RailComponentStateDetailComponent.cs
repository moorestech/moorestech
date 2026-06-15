using System.Collections.Generic;
using Game.Block.Interface.Component;
using MessagePack;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Block.Blocks.TrainRail
{
    /// <summary>
    /// RailComponentのStateDetailとSaveStateを提供するコンポーネント
    /// Component that provides StateDetail and SaveState for RailComponent
    /// </summary>
    public class RailComponentStateDetailComponent : IBlockStateDetail, IBlockSaveState
    {
        private readonly RailComponent _railComponent;

        public static string SaveKeyStatic { get; } = typeof(RailComponentStateDetailComponent).FullName;
        public string SaveKey { get; } = SaveKeyStatic;
        public bool IsDestroy { get; private set; }

        public RailComponentStateDetailComponent(RailComponent railComponent)
        {
            _railComponent = railComponent;
        }

        public void Destroy()
        {
            IsDestroy = true;
        }

        public string GetSaveState()
        {
            // レール向きを意味の分かるkey-value(JSON)で保存する
            // Persist rail direction as a human-readable key-value (JSON) payload
            var direction = _railComponent.RailDirection;
            return JsonConvert.SerializeObject(new RailComponentSaveJsonObject { X = direction.x, Y = direction.y, Z = direction.z });
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            var bytes = SerializeStateDetail();
            return new[] { new BlockStateDetail(RailBridgePierComponentStateDetail.StateDetailKey, bytes) };
        }

        public static Vector3 LoadRailDirection(Dictionary<string, string> componentStates)
        {
            // セーブ済みJSONを復元して向きを取り出す
            // Restore saved JSON and extract rail direction
            var saveData = JsonConvert.DeserializeObject<RailComponentSaveJsonObject>(componentStates[SaveKeyStatic]);
            return new Vector3(saveData.X, saveData.Y, saveData.Z);
        }

        #region Internal

        private byte[] SerializeStateDetail()
        {
            return MessagePackSerializer.Serialize(new RailBridgePierComponentStateDetail(_railComponent.RailDirection));
        }

        #endregion
    }

    public class RailComponentSaveJsonObject
    {
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;
    }
}
