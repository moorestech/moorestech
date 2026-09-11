using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Client.Game.InGame.BugReport
{
    // バンドルの唯一の契約。取得側と再現側はこの形だけで会話する（ADR 0057）
    // The bundle's single contract; capture and reproduction sides talk only through this shape (ADR 0057)
    public sealed class BugReportManifest
    {
        // Vector3 の normalized 等が自己参照ループを起こすため無視する。マニフェストが必要とするのは値だけ
        // Vector3's normalized etc. trigger reference loops; the manifest only needs the plain values
        private static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
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
}
