using System;
using System.Collections.Generic;
using System.Linq;
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
                SessionStart = ProgressUtcTime.ToIso(Start),
                WorldCreatedAt = "2026-09-10T09:00:00Z",
                TotalPlaySecondsAtStart = 100,
                TotalPlaySecondsCapturedAt = ProgressUtcTime.ToIso(Start),
                BaselineChallenges = new List<string> { "11111111-1111-1111-1111-111111111111" },
                BaselineResearch = new List<string>(),
            };
        }

        [Test]
        public void イベントが0件でも必須キーが揃う()
        {
            var json = JObject.Parse(ProgressRecordComposer.Compose(CreateHeader(), new List<ProgressEventEntry>(), ProgressEndReason.Quit, Start.AddSeconds(60)));

            foreach (var key in new[] { "schemaVersion", "steamId", "buildInfo", "sessionStart", "sessionEnd", "endReason", "playSeconds", "worldCreatedAt", "totalPlaySeconds", "reachedChallenges", "completedResearch", "placedBlockCount", "craftCount", "lastUiState", "missing", "events" })
            {
                Assert.IsTrue(json.ContainsKey(key), $"キー {key} が無い");
            }
            Assert.AreEqual(60d, (double)json["playSeconds"]);
            Assert.AreEqual(160d, (double)json["totalPlaySeconds"]);
            Assert.AreEqual(ProgressEndReason.Quit, (string)json["endReason"]);
            Assert.AreEqual(0, (int)json["placedBlockCount"]);
            Assert.AreEqual(1, ((JArray)json["reachedChallenges"]).Count);

            // UI状態が1件も無い記録は「空文字で離脱した」ではなく欠損として表明する
            // A record with no UI state declares a gap instead of claiming an exit at an empty state
            CollectionAssert.Contains(MissingItems(json), "lastUiState");
        }

        [Test]
        public void 集計値はイベント列から導出される()
        {
            var events = new List<ProgressEventEntry>
            {
                ProgressEvents.BlockPlaced(Start.AddSeconds(1), 10, 2),
                ProgressEvents.ChallengeCompleted(Start.AddSeconds(3), 30, "22222222-2222-2222-2222-222222222222"),
                ProgressEvents.ResearchCompleted(Start.AddSeconds(4), 40, "33333333-3333-3333-3333-333333333333"),
                ProgressEvents.CraftRequested(Start.AddSeconds(5), 50, Guid.NewGuid()),
                ProgressEvents.UiStateChanged(Start.AddSeconds(6), 60, "BuildMenu"),
            };

            var json = JObject.Parse(ProgressRecordComposer.Compose(CreateHeader(), events, ProgressEndReason.Quit, Start.AddSeconds(10)));

            Assert.AreEqual(2, (int)json["placedBlockCount"]);
            Assert.AreEqual(1, (int)json["craftCount"]);
            Assert.AreEqual(2, ((JArray)json["reachedChallenges"]).Count);
            Assert.AreEqual(1, ((JArray)json["completedResearch"]).Count);
            Assert.AreEqual("BuildMenu", (string)json["lastUiState"]);
        }

        // crash-recovered が無条件に設置数の欠損を名乗ると、建築と無縁のセッションの記録まで毎回欠損付きで届く
        // An unconditional placement gap on crash-recovered would put a gap on every record, including sessions that never built anything
        [Test]
        public void 建築モードへ入っていない異常終了は設置数の欠損を名乗らない()
        {
            var events = new List<ProgressEventEntry> { ProgressEvents.UiStateChanged(Start.AddSeconds(2), 20, "GameScreen") };

            var json = JObject.Parse(ProgressRecordComposer.Compose(CreateHeader(), events, ProgressEndReason.CrashRecovered, Start.AddSeconds(10)));

            CollectionAssert.DoesNotContain(MissingItems(json), "placedBlockCount");
        }

        // 建築モードに入った跡があれば、最後の遷移以降の設置は実際に失われている。そこは必ず表明する
        // Once there is a trace of build mode, placements after the last transition really are lost, and that is always declared
        [Test]
        public void 建築モードへ入った異常終了は設置数の欠損を名乗る()
        {
            var events = new List<ProgressEventEntry> { ProgressEvents.UiStateChanged(Start.AddSeconds(2), 20, "PlaceBlock") };

            var json = JObject.Parse(ProgressRecordComposer.Compose(CreateHeader(), events, ProgressEndReason.CrashRecovered, Start.AddSeconds(10)));

            CollectionAssert.Contains(MissingItems(json), "placedBlockCount");
        }

        // baseline に既に入っている到達がイベントでも届く（再ログイン直後の再送等）。二重に数えない
        // A reach already in the baseline can arrive as an event again; it must never be counted twice
        [Test]
        public void baselineと同じ到達はイベントで届いても二重計上しない()
        {
            var events = new List<ProgressEventEntry> { ProgressEvents.ChallengeCompleted(Start.AddSeconds(1), 10, "11111111-1111-1111-1111-111111111111") };
            var json = JObject.Parse(ProgressRecordComposer.Compose(CreateHeader(), events, ProgressEndReason.Quit, Start.AddSeconds(10)));

            Assert.AreEqual(1, ((JArray)json["reachedChallenges"]).Count);
        }

        // 時刻の逆転を無音で0へ丸めると「一瞬で終わったセッション」と区別できない
        // Silently rounding a reversed clock to 0 makes it indistinguishable from a session that really lasted no time
        [Test]
        public void 終了時刻が開始より前なら欠損として残る()
        {
            var json = JObject.Parse(ProgressRecordComposer.Compose(CreateHeader(), new List<ProgressEventEntry>(), ProgressEndReason.Quit, Start.AddSeconds(-30)));

            Assert.AreEqual(0d, (double)json["playSeconds"]);
            CollectionAssert.Contains(MissingItems(json), "playSeconds");
        }

        [Test]
        public void 設置せずにビルドモードを抜けたときだけキャンセルを合成する()
        {
            var cancelled = new List<ProgressEventEntry>
            {
                ProgressEvents.UiStateChanged(Start.AddSeconds(1), 10, "PlaceBlock"),
                ProgressEvents.UiStateChanged(Start.AddSeconds(2), 20, "GameScreen"),
            };
            var placed = new List<ProgressEventEntry>
            {
                ProgressEvents.UiStateChanged(Start.AddSeconds(1), 10, "PlaceBlock"),
                ProgressEvents.BlockPlaced(Start.AddSeconds(2), 15, 1),
                ProgressEvents.UiStateChanged(Start.AddSeconds(3), 20, "GameScreen"),
            };

            Assert.AreEqual(1, CountCancel(ProgressRecordComposer.WithSynthesizedBuildModeCancel(cancelled)));
            Assert.AreEqual(0, CountCancel(ProgressRecordComposer.WithSynthesizedBuildModeCancel(placed)));

            // 抜けないまま終わった滞在は合成しない（終了は別のイベントで表現される）
            // A stay that never ends is not synthesized; the exit is expressed by another event
            var stillInside = new List<ProgressEventEntry> { ProgressEvents.UiStateChanged(Start.AddSeconds(1), 10, "PlaceBlock") };
            Assert.AreEqual(0, CountCancel(ProgressRecordComposer.WithSynthesizedBuildModeCancel(stillInside)));
        }

        // 合成されたキャンセルは離脱を起こした遷移の時刻を引き継ぐ。回収時の現在時刻を焼き付けると数日ずれる
        // The synthesized cancel inherits the transition's own time; stamping the recovery moment would be days off
        [Test]
        public void 合成したキャンセルは遷移の時刻を引き継ぐ()
        {
            var transition = ProgressEvents.UiStateChanged(Start.AddSeconds(2), 20, "GameScreen");
            var events = new List<ProgressEventEntry> { ProgressEvents.UiStateChanged(Start.AddSeconds(1), 10, "PlaceBlock"), transition };

            var cancel = ProgressRecordComposer.WithSynthesizedBuildModeCancel(events).First(entry => entry.Type == ProgressEventType.BuildModeCancelled);

            Assert.AreEqual(transition.T, cancel.T);
            Assert.AreEqual(transition.Tick, cancel.Tick);
        }

        private static int CountCancel(List<ProgressEventEntry> events)
        {
            return events.FindAll(e => e.Type == ProgressEventType.BuildModeCancelled).Count;
        }

        private static List<string> MissingItems(JObject record)
        {
            return ((JArray)record["missing"]).Select(item => (string)item["item"]).ToList();
        }
    }
}
