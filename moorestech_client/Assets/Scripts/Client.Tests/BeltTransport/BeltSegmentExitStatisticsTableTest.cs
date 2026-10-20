using System.Text.RegularExpressions;
using Client.Game.InGame.BeltTransport.Statistics;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BeltTransport
{
    // リングと層で持つ搬出数が、窓ちょうどの個数を返し、リングは5tick・層は半窓ごとに古い分を落とすか。期待値は手計算
    // Whether the ring and layered exit counts answer exactly the window's count, with the ring dropping old blocks every 5 ticks and the layers every half window; expected values are hand-computed
    public class BeltSegmentExitStatisticsTableTest
    {
        [Test]
        public void 窓が埋まるまでは計測中で埋まった瞬間から正確な個数を返す()
        {
            var table = new BeltSegmentExitStatisticsTable();
            table.Reset(2);

            // segment0は奇数tickに搬出、segment1は搬出なし
            // Segment 0 outputs on odd ticks, segment 1 never
            for (var tick = 1; tick <= 89; tick++)
            {
                if (tick % 2 == 1) table.OnExited(0);
                table.EndTick();
                Assert.IsFalse(table.TryGetExitCount(0, 90, out _), $"tick {tick} is still measuring");
            }
            table.OnExited(0);
            table.EndTick();

            Assert.IsTrue(table.TryGetExitCount(0, 90, out var count));
            Assert.AreEqual(45, count, "odd ticks 1..89 of the first 90 ticks");
            Assert.IsTrue(table.TryGetExitCount(1, 90, out var idle));
            Assert.AreEqual(0, idle);
            Assert.IsFalse(table.TryGetExitCount(0, 180, out _), "the 180 window needs 180 ticks");
        }

        [Test]
        public void リングの窓は5tickごとに前へ進み古いブロックを落とす()
        {
            var table = new BeltSegmentExitStatisticsTable();
            table.Reset(1);
            for (var tick = 1; tick <= 90; tick++)
            {
                if (tick % 2 == 1) table.OnExited(0);
                table.EndTick();
            }

            // 94tickまでは窓が動かない(ブロック19が未完成)
            // The window stays until tick 94 (block 19 incomplete)
            for (var tick = 91; tick <= 94; tick++) table.EndTick();
            table.TryGetExitCount(0, 90, out var before);
            Assert.AreEqual(45, before);

            // 95tickで窓は6..95になり、落ちたtick1・3・5の3個ぶん減る
            // At tick 95 the window becomes 6..95, losing the outputs of ticks 1, 3 and 5
            table.EndTick();
            table.TryGetExitCount(0, 90, out var after);
            Assert.AreEqual(42, after);
        }

        [Test]
        public void 毎tick搬出でも各層の桁が溢れず全窓が埋まる()
        {
            var table = new BeltSegmentExitStatisticsTable();
            table.Reset(1);
            for (var tick = 1; tick <= 5760; tick++)
            {
                table.OnExited(0);
                table.EndTick();
            }

            foreach (var window in BeltSegmentExitWindows.Ticks)
            {
                Assert.IsTrue(table.TryGetExitCount(0, window, out var count));
                Assert.AreEqual(window, count, $"one exit per tick over a {window}-tick window");
            }
        }

        [Test]
        public void 層の窓は下の2ブロックの和で窓の半分ごとに更新される()
        {
            var table = new BeltSegmentExitStatisticsTable();
            table.Reset(1);

            // 最初の90tickだけ毎tick搬出し、その後は止める
            // Output every tick for the first 90 ticks only, then stop
            for (var tick = 1; tick <= 180; tick++)
            {
                if (tick <= 90) table.OnExited(0);
                table.EndTick();
            }
            table.TryGetExitCount(0, 180, out var at180);
            Assert.AreEqual(90, at180);
            table.TryGetExitCount(0, 90, out var recent);
            Assert.AreEqual(0, recent, "the 90 window already moved past the outputs");

            // リングの180窓は5tickごとに滑る: 269tickでは86..265の5個、270tickで91..270の0個
            // The ring's 180 window slides every 5 ticks: 5 outputs (86..265) at tick 269, none (91..270) at tick 270
            for (var tick = 181; tick <= 269; tick++) table.EndTick();
            table.TryGetExitCount(0, 180, out var at269);
            Assert.AreEqual(5, at269);
            table.EndTick();
            table.TryGetExitCount(0, 180, out var at270);
            Assert.AreEqual(0, at270);

            // 層の360窓は180tickごと: 360tickで1..360の90個、539tickまで据え置き、540tickで181..540の0個
            // The layered 360 window moves every 180 ticks: 90 (1..360) at tick 360, unchanged through 539, then 0 (181..540) at tick 540
            for (var tick = 271; tick <= 360; tick++) table.EndTick();
            Assert.IsTrue(table.TryGetExitCount(0, 360, out var at360));
            Assert.AreEqual(90, at360);
            for (var tick = 361; tick <= 539; tick++) table.EndTick();
            table.TryGetExitCount(0, 360, out var at539);
            Assert.AreEqual(90, at539);
            table.EndTick();
            table.TryGetExitCount(0, 360, out var at540);
            Assert.AreEqual(0, at540);
        }

        [Test]
        public void リセットで履歴と経過tickが消える()
        {
            var table = new BeltSegmentExitStatisticsTable();
            table.Reset(1);
            for (var tick = 1; tick <= 90; tick++)
            {
                table.OnExited(0);
                table.EndTick();
            }
            table.Reset(1);

            Assert.AreEqual(0, table.CompletedTicks);
            Assert.IsFalse(table.TryGetExitCount(0, 90, out _));
            for (var tick = 1; tick <= 90; tick++) table.EndTick();
            table.TryGetExitCount(0, 90, out var count);
            Assert.AreEqual(0, count, "old outputs did not survive the reset");
        }

        [Test]
        public void 窓の更新通知は5tickごとに流れる()
        {
            var table = new BeltSegmentExitStatisticsTable();
            table.Reset(1);
            var fired = 0;
            using var subscription = table.OnWindowAdvanced.Subscribe(_ => fired++);

            for (var tick = 1; tick <= 134; tick++) table.EndTick();
            Assert.AreEqual(26, fired);
            table.EndTick();
            Assert.AreEqual(27, fired);
        }

        [Test]
        public void 持っていない窓はログを出してfalseを返す()
        {
            var table = new BeltSegmentExitStatisticsTable();
            table.Reset(1);
            LogAssert.Expect(LogType.Error, new Regex("7 is not a window"));
            LogAssert.Expect(LogType.Error, new Regex("270 is not a window"));

            Assert.IsFalse(table.TryGetExitCount(0, 7, out _));
            Assert.IsFalse(table.TryGetExitCount(0, 270, out _));
        }
    }
}
