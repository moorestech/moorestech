using System;
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // セッション開始時に確定する文脈。終了時に組む record.json の土台になる
    // The context fixed at session start; the base for the record.json composed at the end
    public sealed class ProgressRecordHeader
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
        };

        public int SchemaVersion = 1;
        public string SteamId = "";
        public BuildInfo BuildInfo;
        public string SessionStart;
        public string WorldCreatedAt = "";
        public double TotalPlaySecondsAtStart;
        public List<string> BaselineChallenges = new();
        public List<string> BaselineResearch = new();

        // ヘッダを失った残骸から組んだ記録であることの印。steamId や worldCreatedAt が空なのは欠損のためだと読み手に伝える
        // Marks a record built from a leftover that lost its header, telling readers the empty steamId and worldCreatedAt come from that loss
        public bool HeaderMissing;

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Settings);
        }

        // 前回セッションの残骸を読む境界。壊れていたら null を返し、呼び出し側が理由付きで捨てる
        // The boundary that reads a leftover session; returns null when broken so the caller drops it with a reason
        public static ProgressRecordHeader FromJson(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<ProgressRecordHeader>(json, Settings);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"進行記録のヘッダを読めません: {exception.GetBaseException().Message}");
                return null;
            }
        }
    }
}
