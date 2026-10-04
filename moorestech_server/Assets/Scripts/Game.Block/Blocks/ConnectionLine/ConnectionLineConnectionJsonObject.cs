using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface.Component;
using Newtonsoft.Json;

namespace Game.Block.Blocks.ConnectionLine
{
    /// <summary>
    /// 電線・チェーンのセーブ上の接続1件。相手・引いた種類・払った素材を持つ
    /// One saved wire or chain connection: partner, drawn connect tool and paid materials
    /// </summary>
    public class ConnectionLineConnectionJsonObject
    {
        [JsonProperty("targetBlockInstanceId")] public int TargetBlockInstanceId { get; private set; }

        // 引いた種類はUndoの引き直しに要る。欠けた旧形はマイグレーションが埋めるので、ここでは必須で読む
        // The drawn tool is needed for undo re-drawing; the migration fills it for old saves, so it is read as required here
        [JsonProperty("connectToolGuid", Required = Required.Always)] public Guid ConnectToolGuid { get; private set; }
        [JsonProperty("materials")] public List<ConnectToolMaterialSaveJsonObject> Materials { get; private set; }

        public ConnectionLineConnectionJsonObject()
        {
            Materials = new List<ConnectToolMaterialSaveJsonObject>();
        }

        public ConnectionLineConnectionJsonObject(int targetBlockInstanceId, ConnectionLineRecord record)
        {
            TargetBlockInstanceId = targetBlockInstanceId;
            ConnectToolGuid = record.ConnectToolGuid;
            Materials = record.Materials == null
                ? new List<ConnectToolMaterialSaveJsonObject>()
                : record.Materials.Select(m => new ConnectToolMaterialSaveJsonObject(m)).ToList();
        }

        // ロード時に永続値から接続記録を復元する
        // Restore the connection record from persisted values on load
        public ConnectionLineRecord ToConnectionRecord()
        {
            var materials = (Materials ?? new List<ConnectToolMaterialSaveJsonObject>())
                .Select(m => m.ToMaterialCost()).ToList();
            return new ConnectionLineRecord(ConnectToolGuid, materials);
        }
    }
}
