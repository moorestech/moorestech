using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface.Component;
using Newtonsoft.Json;

namespace Game.Block.Blocks.ConnectionLine
{
    /// <summary>
    /// セーブ上の接続1件（相手・種類・払った素材）
    /// One saved connection: partner, tool and paid materials
    /// </summary>
    public class ConnectionLineConnectionJsonObject
    {
        [JsonProperty("targetBlockInstanceId")] public int TargetBlockInstanceId { get; private set; }

        // 引いた種類はUndoの引き直しに要る。旧形はマイグレーションが埋める。それでも欠けた接続は復元側がその1件だけ飛ばす
        // The drawn tool is needed for undo re-drawing; the migration fills old saves, and a connection still missing it is skipped alone by the restorer
        [JsonProperty("connectToolGuid")] public Guid? ConnectToolGuid { get; private set; }
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

        // ロード時に永続値から接続記録を復元する。種類が欠けていれば false
        // Restore the connection record from persisted values on load; false when the tool is missing
        public bool TryToConnectionRecord(out ConnectionLineRecord record)
        {
            record = default;
            if (!ConnectToolGuid.HasValue) return false;
            var materials = (Materials ?? new List<ConnectToolMaterialSaveJsonObject>())
                .Select(m => m.ToMaterialCost()).ToList();
            record = new ConnectionLineRecord(ConnectToolGuid.Value, materials);
            return true;
        }
    }
}
