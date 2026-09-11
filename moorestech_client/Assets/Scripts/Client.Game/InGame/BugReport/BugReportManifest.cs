using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Client.Game.InGame.BugReport
{
    // バンドルの唯一の契約。取得側と再現側はこの形だけで会話する（ADR 0057）
    // The bundle's single contract; capture and reproduction sides talk only through this shape (ADR 0057)
    public sealed class BugReportManifest
    {
        // Vector3のnormalized等が自己参照ループを起こすため専用converterでx/y/zのみ書く
        // Vector3's normalized etc. trigger reference loops; a scoped converter writes only x/y/z
        private static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            Converters = { new Vector3JsonConverter() },
        };

        public int SchemaVersion = 1;
        public string CreatedAt;
        public string Description;
        public string Platform;
        public bool IsEditor;
        public ulong ReportTick;
        public List<ulong> SnapshotTicks = new();
        public List<string> SnapshotFiles = new();
        public List<string> PacketLogFiles = new();
        public RepositoryState Repository;
        public RepositoryState MasterData;
        public ClientStateSnapshot ClientState;
        public List<MissingItem> Missing = new();
        public double VideoSeconds;

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Settings);
        }
    }

    public sealed class RepositoryState
    {
        public string Commit;
        public string Branch;
        public bool Dirty;
    }

    public sealed class MissingItem
    {
        public string Item;
        public string Reason;
    }

    // Vector3をx/y/zの3値だけへ明示的に詰め替えるconverter。循環参照設定に頼らない
    // Explicitly narrows a Vector3 down to x/y/z instead of relying on reference-loop settings
    public sealed class Vector3JsonConverter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WriteEndObject();
        }

        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            // マニフェストはクライアントから自動修正ランへの片方向出力のみで復元経路は無い
            // The manifest is a one-way output from client to auto-fix run; there is no read-back path
            throw new NotSupportedException("BugReportManifestのVector3は書き込み専用");
        }
    }
}
