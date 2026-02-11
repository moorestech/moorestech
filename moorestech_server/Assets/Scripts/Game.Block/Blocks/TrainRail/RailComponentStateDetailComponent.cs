using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using MessagePack;
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
            // レール向きを既存StateDetail形式のまま保存する
            // Persist rail direction using the same StateDetail payload format
            var bytes = SerializeStateDetail();
            return Convert.ToBase64String(bytes);
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            var bytes = SerializeStateDetail();
            return new[] { new BlockStateDetail(RailBridgePierComponentStateDetail.StateDetailKey, bytes) };
        }

        public static Vector3 LoadRailDirection(Dictionary<string, string> componentStates)
        {
            // セーブ済みStateDetailを復元して向きを取り出す
            // Restore saved StateDetail and extract rail direction
            var bytes = Convert.FromBase64String(componentStates[SaveKeyStatic]);
            var detail = MessagePackSerializer.Deserialize<RailBridgePierComponentStateDetail>(bytes);
            return detail.RailBlockDirection;
        }

        #region Internal

        private byte[] SerializeStateDetail()
        {
            return MessagePackSerializer.Serialize(new RailBridgePierComponentStateDetail(_railComponent.RailDirection));
        }

        #endregion
    }
}
