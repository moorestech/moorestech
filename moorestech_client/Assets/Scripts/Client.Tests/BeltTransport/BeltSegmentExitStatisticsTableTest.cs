using System.Text.RegularExpressions;
using Client.Game.InGame.BeltTransport.Statistics;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BeltTransport
{
    // 層で畳んだ搬出数が、窓ちょうどの個数を返し、ブロック単位で古い分を落とすか。期待値は手計算
    // Whether the layered exit counts answer exactly the window's count and drop old blocks block by block; expected values are hand-computed
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
        public void 窓は45tickブロック単位で前へ進み古いブロックを落とす()
        {
            var table = new BeltSegmentExitStatisticsTable();
            table.Reset(1);
            for (var tick = 1; tick <= 90; tick++)
            {
                if (tick % 2 == 1) table.OnExited(0);
                table.EndTick();
            }

            // 134tickまでは窓が動かない(ブロック3が未完成)
            // The window stays until tick 134 (block 3 incomplete)
            for (var tick = 91; tick <= 134; tick++) table.EndTick();
            table.TryGetExitCount(0, 90, out var before);
            Assert.AreEqual(45, before);

            // 135tickでブロック2(46..90の奇数=22個)+ブロック3(0個)になる
            // At tick 135 it becomes block 2 (odd ticks 46..90 = 22) + block 3 (0)
            table.EndTick();
            table.TryGetExitCount(0, 90, out var after);
            Assert.AreEqual(22, after);
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
        public void 上の窓は下の2ブロックの和で窓の半分ごとに更新される()
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

            // 270tickで180窓はブロック2(91..180)+ブロック3(181..270)=0になる
            // At tick 270 the 180 window is block 2 (91..180) + block 3 (181..270) = 0
            for (var tick = 181; tick <= 269; tick++) table.EndTick();
            table.TryGetExitCount(0, 180, out var at269);
            Assert.AreEqual(90, at269, "not yet moved at tick 269");
            table.EndTick();
            table.TryGetExitCount(0, 180, out var at270);
            Assert.AreEqual(0, at270);
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
        public void 窓の更新通知は45tickごとに流れる()
        {
            var table = new BeltSegmentExitStatisticsTable();
            table.Reset(1);
            var fired = 0;
            using var subscription = table.OnWindowAdvanced.Subscribe(_ => fired++);

            for (var tick = 1; tick <= 134; tick++) table.EndTick();
            Assert.AreEqual(2, fired);
            table.EndTick();
            Assert.AreEqual(3, fired);
        }

        [Test]
        public void 持っていない窓はログを出してfalseを返す()
        {
            var table = new BeltSegmentExitStatisticsTable();
            table.Reset(1);
            LogAssert.Expect(LogType.Error, new Regex("100 is not a window"));

            Assert.IsFalse(table.TryGetExitCount(0, 100, out _));
        }
    }
}
