using System;
using System.Collections.Generic;
using Client.Game.InGame.Playtest.Progress;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.Playtest
{
    public class ProgressRecordComposerTest
    {
        private static readonly DateTime Start = new(2026, 9, 13, 10, 0, 0, DateTimeKind.Utc);

        private static ProgressRecordHeader CreateHeader()
        {
            return new ProgressRecordHeader
            {
                SteamId = "",
                BuildInfo = null,
                SessionStart = Start.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                WorldCreatedAt = "2026-09-10T09:00:00Z",
                TotalPlaySecondsAtStart = 100,
                BaselineChallenges = new List<string> { "11111111-1111-1111-1111-111111111111" },
                BaselineResearch = new List<string>(),
            };
        }

        [Test]
        public void イベントが0件でも必須キーが揃う()
        {
            var json = JObject.Parse(ProgressRecordComposer.Compose(CreateHeader(), new List<ProgressEventEntry>(), "quit", Start.AddSeconds(60)));

            foreach (var key in new[] { "schemaVersion", "steamId", "buildInfo", "sessionStart", "sessionEnd", "endReason", "playSeconds", "worldCreatedAt", "totalPlaySeconds", "reachedChallenges", "completedResearch", "placedBlockCount", "craftCount", "lastUiState", "events" })
            {
                Assert.IsTrue(json.ContainsKey(key), $"キー {key} が無い");
            }
            Assert.AreEqual(60d, (double)json["playSeconds"]);
            Assert.AreEqual(160d, (double)json["totalPlaySeconds"]);
            Assert.AreEqual("quit", (string)json["endReason"]);
            Assert.AreEqual(0, (int)json["placedBlockCount"]);
            Assert.AreEqual(1, ((JArray)json["reachedChallenges"]).Count);
        }

        [Test]
        public void 集計値はイベント列から導出される()
        {
            var events = new List<ProgressEventEntry>
            {
                ProgressEventEntry.Create(Start.AddSeconds(1), 10, ProgressEventType.BlockPlaced, new JObject { ["blockGuid"] = "b1" }),
                ProgressEventEntry.Create(Start.AddSeconds(2), 20, ProgressEventType.BlockPlaced, new JObject { ["blockGuid"] = "b2" }),
                ProgressEventEntry.Create(Start.AddSeconds(3), 30, ProgressEventType.ChallengeCompleted, new JObject { ["challengeGuid"] = "22222222-2222-2222-2222-222222222222" }),
                ProgressEventEntry.Create(Start.AddSeconds(4), 40, ProgressEventType.ResearchCompleted, new JObject { ["researchGuid"] = "33333333-3333-3333-3333-333333333333" }),
                ProgressEventEntry.Create(Start.AddSeconds(5), 50, "craftExecuted", new JObject { ["recipeGuid"] = "r1" }),
                ProgressEventEntry.Create(Start.AddSeconds(6), 60, ProgressEventType.UiStateChanged, new JObject { ["state"] = "BuildMenu" }),
            };

            var json = JObject.Parse(ProgressRecordComposer.Compose(CreateHeader(), events, "quit", Start.AddSeconds(10)));

            Assert.AreEqual(2, (int)json["placedBlockCount"]);
            Assert.AreEqual(1, (int)json["craftCount"]);
            Assert.AreEqual(2, ((JArray)json["reachedChallenges"]).Count);
            Assert.AreEqual(1, ((JArray)json["completedResearch"]).Count);
            Assert.AreEqual("BuildMenu", (string)json["lastUiState"]);
        }

        [Test]
        public void 設置せずにビルドモードを抜けたときだけキャンセルを合成する()
        {
            var cancelled = new List<ProgressEventEntry>
            {
                ProgressEventEntry.Create(Start.AddSeconds(1), 10, ProgressEventType.UiStateChanged, new JObject { ["state"] = "PlaceBlock" }),
                ProgressEventEntry.Create(Start.AddSeconds(2), 20, ProgressEventType.UiStateChanged, new JObject { ["state"] = "GameScreen" }),
            };
            var placed = new List<ProgressEventEntry>
            {
                ProgressEventEntry.Create(Start.AddSeconds(1), 10, ProgressEventType.UiStateChanged, new JObject { ["state"] = "PlaceBlock" }),
                ProgressEventEntry.Create(Start.AddSeconds(2), 15, ProgressEventType.BlockPlaced, new JObject()),
                ProgressEventEntry.Create(Start.AddSeconds(3), 20, ProgressEventType.UiStateChanged, new JObject { ["state"] = "GameScreen" }),
            };

            Assert.AreEqual(1, CountCancel(ProgressRecordComposer.WithSynthesizedBuildModeCancel(cancelled)));
            Assert.AreEqual(0, CountCancel(ProgressRecordComposer.WithSynthesizedBuildModeCancel(placed)));

            // 抜けないまま終わった滞在は合成しない（終了は別のイベントで表現される）
            // A stay that never ends is not synthesized; the exit is expressed by another event
            var stillInside = new List<ProgressEventEntry>
            {
                ProgressEventEntry.Create(Start.AddSeconds(1), 10, ProgressEventType.UiStateChanged, new JObject { ["state"] = "PlaceBlock" }),
            };
            Assert.AreEqual(0, CountCancel(ProgressRecordComposer.WithSynthesizedBuildModeCancel(stillInside)));
        }

        private static int CountCancel(List<ProgressEventEntry> events)
        {
            return events.FindAll(e => e.Type == ProgressEventType.BuildModeCancelled).Count;
        }
    }
}
