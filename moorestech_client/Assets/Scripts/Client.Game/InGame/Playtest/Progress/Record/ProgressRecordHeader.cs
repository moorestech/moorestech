using System;
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // セッション開始時に確定する文脈。終了時に組む record.json の土台になる
    // The context fixed at session start; the base for the record.json composed at the end
    internal sealed class ProgressRecordHeader
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

        // 累計プレイ時間をサーバーから受け取った瞬間。終了時刻との差だけを足すので、応答待ちの数秒が二重計上されない
        // The instant the total play time arrived from the server; only the span to the session end is added, so the wait is never counted twice
        public string TotalPlaySecondsCapturedAt = "";

        public List<string> BaselineChallenges = new();
        public List<string> BaselineResearch = new();

        // 埋められなかった値と、その理由。実データと同じ形の既定値で埋めず、欠損をこの1列で表明する（ADR 0060 裁定6）
        // What could not be filled and why; instead of defaults shaped like real data, every gap is declared in this one column (ADR 0060 adjudication 6)
        public List<MissingItem> Missing = new();

        // 欠損は必ず開発者ログと記録の両方へ積む。片方だけだと縮退した理由が誰にも届かない
        // Every gap lands in both the developer log and the record; one alone leaves the reason unreachable
        public void AddMissing(string item, string reason)
        {
            Debug.LogWarning($"進行記録の欠損 {item}: {reason}");
            Missing.Add(new MissingItem { Item = item, Reason = reason });
        }

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
