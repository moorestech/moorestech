using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Playtest.Progress;
using Client.Game.InGame.Playtest.Progress.Record;
using Client.Game.InGame.Playtest.Progress.Record.Events;
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
                SteamId = null,
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
            var json = Compose(new List<IProgressEvent>(), ProgressEndReason.Quit, Start.AddSeconds(60));

            foreach (var key in new[] { "schemaVersion", "steamId", "buildInfo", "sessionStart", "sessionEnd", "endReason", "playSeconds", "worldCreatedAt", "totalPlaySeconds", "reachedChallenges", "completedResearch", "placedBlockCount", "craftCount", "lastUiState", "missing", "events" })
            {
                Assert.IsTrue(json.ContainsKey(key), $"キー {key} が無い");
            }
            Assert.AreEqual(60d, (double)json["playSeconds"]);
            Assert.AreEqual(160d, (double)json["totalPlaySeconds"]);
            Assert.AreEqual("quit", (string)json["endReason"]);
            Assert.AreEqual(0, (int)json["placedBlockCount"]);
            Assert.AreEqual(1, ((JArray)json["reachedChallenges"]).Count);

            // 取れなかった SteamID と UI状態は空文字でなく null（F02）
            // An unavailable SteamID and UI state are null rather than empty strings (F02)
            Assert.AreEqual(JTokenType.Null, json["steamId"].Type);
            Assert.AreEqual(JTokenType.Null, json["lastUiState"].Type);
            CollectionAssert.Contains(MissingItems(json), "lastUiState");
        }

        // 契約値の綴りはJSON化の1箇所で決まる。enum名がそのまま漏れると集計側の分岐が全て外れる
        // The contract spelling is decided at the single serialization point; leaking the enum name would miss every branch on the digest side
        [Test]
        public void 終了理由は契約値の綴りで出る()
        {
            Assert.AreEqual("crash-recovered", (string)Compose(new List<IProgressEvent>(), ProgressEndReason.CrashRecovered, Start)["endReason"]);
            Assert.AreEqual("init-failed", (string)Compose(new List<IProgressEvent>(), ProgressEndReason.InitializationFailed, Start)["endReason"]);
        }

        [Test]
        public void 集計値はイベント列から導出される()
        {
            var events = new List<IProgressEvent>
            {
                new BlockPlacedEvent(Start.AddSeconds(1), 10, 2),
                new ChallengeCompletedEvent(Start.AddSeconds(3), 30, "22222222-2222-2222-2222-222222222222"),
                new ResearchCompletedEvent(Start.AddSeconds(4), 40, "33333333-3333-3333-3333-333333333333"),
                new CraftCompletedEvent(Start.AddSeconds(5), 50, Guid.NewGuid().ToString()),
                new UiStateChangedEvent(Start.AddSeconds(6), 60, "BuildMenu"),
            };

            var json = Compose(events, ProgressEndReason.Quit, Start.AddSeconds(10));

            Assert.AreEqual(2, (int)json["placedBlockCount"]);
            Assert.AreEqual(1, (int)json["craftCount"]);
            Assert.AreEqual(2, ((JArray)json["reachedChallenges"]).Count);
            Assert.AreEqual(1, ((JArray)json["completedResearch"]).Count);
            Assert.AreEqual("BuildMenu", (string)json["lastUiState"]);
        }

        // 設置数は区間ごとの集計なので、建築モードへ入っていない残骸でも最後の遷移以降の設置は失われている
        // The count is aggregated per interval, so even a leftover that never entered build mode lost whatever was placed after the last transition
        [Test]
        public void 建築モードへ入っていない異常終了も設置数の欠損を名乗る()
        {
            var events = new List<IProgressEvent> { new UiStateChangedEvent(Start.AddSeconds(2), 20, "GameScreen") };

            CollectionAssert.Contains(MissingItems(Compose(events, ProgressEndReason.CrashRecovered, Start.AddSeconds(10))), "placedBlockCount");
        }

        // 終了flushを通った記録の設置数は完全。ここで欠損を名乗ると読み手が全記録の設置数を疑い始める
        // A record that passed the shutdown flush has a complete count; a gap here would make every record's count suspect
        [Test]
        public void 正常終了は設置数の欠損を名乗らない()
        {
            var events = new List<IProgressEvent> { new UiStateChangedEvent(Start.AddSeconds(2), 20, "PlaceBlock") };

            CollectionAssert.DoesNotContain(MissingItems(Compose(events, ProgressEndReason.Quit, Start.AddSeconds(10))), "placedBlockCount");
        }

        // baseline に既に入っている到達がイベントでも届く（再ログイン直後の再送等）。二重に数えない
        // A reach already in the baseline can arrive as an event again; it must never be counted twice
        [Test]
        public void baselineと同じ到達はイベントで届いても二重計上しない()
        {
            var events = new List<IProgressEvent> { new ChallengeCompletedEvent(Start.AddSeconds(1), 10, "11111111-1111-1111-1111-111111111111") };

            Assert.AreEqual(1, ((JArray)Compose(events, ProgressEndReason.Quit, Start.AddSeconds(10))["reachedChallenges"]).Count);
        }

        // 時刻の逆転を0へ丸めると「一瞬で終わったセッション」と区別できない。null と理由で残す
        // Rounding a reversed clock to 0 is indistinguishable from a session that really lasted no time, so it stays as null with a reason
        [Test]
        public void 終了時刻が開始より前ならnullと欠損として残る()
        {
            var json = Compose(new List<IProgressEvent>(), ProgressEndReason.Quit, Start.AddSeconds(-30));

            Assert.AreEqual(JTokenType.Null, json["playSeconds"].Type);
            CollectionAssert.Contains(MissingItems(json), "playSeconds");
        }

        [Test]
        public void 設置せずにビルドモードを抜けたときだけキャンセルを合成する()
        {
            var cancelled = new List<IProgressEvent>
            {
                new UiStateChangedEvent(Start.AddSeconds(1), 10, "PlaceBlock"),
                new UiStateChangedEvent(Start.AddSeconds(2), 20, "GameScreen"),
            };
            var placed = new List<IProgressEvent>
            {
                new UiStateChangedEvent(Start.AddSeconds(1), 10, "PlaceBlock"),
                new BlockPlacedEvent(Start.AddSeconds(2), 15, 1),
                new UiStateChangedEvent(Start.AddSeconds(3), 20, "GameScreen"),
            };

            Assert.AreEqual(1, ProgressRecordComposer.WithSynthesizedBuildModeCancel(cancelled).OfType<BuildModeCancelledEvent>().Count());
            Assert.AreEqual(0, ProgressRecordComposer.WithSynthesizedBuildModeCancel(placed).OfType<BuildModeCancelledEvent>().Count());

            // 抜けないまま終わった滞在は合成しない（終了は別のイベントで表現される）
            // A stay that never ends is not synthesized; the exit is expressed by another event
            var stillInside = new List<IProgressEvent> { new UiStateChangedEvent(Start.AddSeconds(1), 10, "PlaceBlock") };
            Assert.AreEqual(0, ProgressRecordComposer.WithSynthesizedBuildModeCancel(stillInside).OfType<BuildModeCancelledEvent>().Count());
        }

        // 合成されたキャンセルは離脱を起こした遷移の時刻を引き継ぐ。回収時の現在時刻を焼き付けると数日ずれる
        // The synthesized cancel inherits the transition's own time; stamping the recovery moment would be days off
        [Test]
        public void 合成したキャンセルは遷移の時刻を引き継ぐ()
        {
            var transition = new UiStateChangedEvent(Start.AddSeconds(2), 20, "GameScreen");
            var events = new List<IProgressEvent> { new UiStateChangedEvent(Start.AddSeconds(1), 10, "PlaceBlock"), transition };

            var cancel = ProgressRecordComposer.WithSynthesizedBuildModeCancel(events).OfType<BuildModeCancelledEvent>().First();

            Assert.AreEqual(transition.T, cancel.T);
            Assert.AreEqual(transition.Tick, cancel.Tick);
        }

        private static JObject Compose(List<IProgressEvent> events, ProgressEndReason endReason, DateTime sessionEndUtc)
        {
            return JObject.Parse(ProgressRecordComposer.Compose(CreateHeader(), events, endReason, sessionEndUtc));
        }

        private static List<string> MissingItems(JObject record)
        {
            return ((JArray)record["missing"]).Select(item => (string)item["item"]).ToList();
        }
    }
}
