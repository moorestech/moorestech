using Core.Master;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Placement
{
    // 鉱脈と散布共有のリング化規則を固定するテスト。
    // Pins the spawn-distance ring rules shared by veins and object scatter.
    public class SpawnDistanceRingPlannerTest
    {
        [Test]
        public void 外半径昇順に並び負値は無限の最外周になる()
        {
            var bands = BandsOf(350f, 250f, -1f);
            var rings = SpawnDistanceRingPlanner.BuildRings(bands);

            Assert.AreEqual(3, rings.Count);
            Assert.AreSame(bands[1], rings[0].Band);
            Assert.AreEqual(0f, rings[0].Inner);
            Assert.AreEqual(250f, rings[0].Outer);
            Assert.AreSame(bands[0], rings[1].Band);
            Assert.AreEqual(250f, rings[1].Inner);
            Assert.AreEqual(350f, rings[1].Outer);
            Assert.AreSame(bands[2], rings[2].Band);
            Assert.AreEqual(350f, rings[2].Inner);
            Assert.AreEqual(float.PositiveInfinity, rings[2].Outer);
        }

        [Test]
        public void 重複した外半径の後者は縮退してリングにならない()
        {
            var bands = BandsOf(250f, 250f);
            var rings = SpawnDistanceRingPlanner.BuildRings(bands);

            Assert.AreEqual(1, rings.Count);
            Assert.AreSame(bands[0], rings[0].Band);
        }

        [Test]
        public void 空配列はリングを作らない()
        {
            Assert.AreEqual(0, SpawnDistanceRingPlanner.BuildRings(new ObjectScatterBand[0]).Count);
        }

        [Test]
        public void NaN外半径は当該バンドだけ除外され他バンドを汚染しない()
        {
            var bands = BandsOf(float.NaN, 250f, -1f);
            var rings = SpawnDistanceRingPlanner.BuildRings(bands);

            Assert.AreEqual(2, rings.Count);
            Assert.AreSame(bands[1], rings[0].Band);
            Assert.AreEqual(0f, rings[0].Inner);
            Assert.AreEqual(250f, rings[0].Outer);
            Assert.AreSame(bands[2], rings[1].Band);
            Assert.AreEqual(250f, rings[1].Inner);
            Assert.AreEqual(float.PositiveInfinity, rings[1].Outer);
        }

        [Test]
        public void リング判定は内側を含み外側を含まない()
        {
            var ring = new SpawnDistanceRing<ObjectScatterBand>(new ObjectScatterBand(), 250f, 350f);

            Assert.IsTrue(ring.Contains(250f));
            Assert.IsTrue(ring.Contains(349.9f));
            Assert.IsFalse(ring.Contains(350f));
            Assert.IsFalse(ring.Contains(249.9f));
        }

        [Test]
        public void 距離範囲と重ならないリングは重なり判定で落ちる()
        {
            var ring = new SpawnDistanceRing<ObjectScatterBand>(new ObjectScatterBand(), 250f, 350f);

            Assert.IsTrue(ring.OverlapsDistanceRange(0f, 250f));
            Assert.IsTrue(ring.OverlapsDistanceRange(349f, 900f));
            Assert.IsFalse(ring.OverlapsDistanceRange(0f, 249f));
            Assert.IsFalse(ring.OverlapsDistanceRange(350f, 900f));
        }

        [Test]
        public void 妥当な外半径列は診断を出さない()
        {
            Assert.IsEmpty(SpawnDistanceRingPlanner.Diagnose(new[] { 250f, 350f, -1f }));
        }

        [Test]
        public void 帯が無い外半径列は診断される()
        {
            var problems = SpawnDistanceRingPlanner.Diagnose(new float[0]);

            Assert.AreEqual(1, problems.Count);
            Assert.IsTrue(problems[0].Contains("no spawn-distance bands"));
        }

        [Test]
        public void マイナス1以外の負の外半径は診断される()
        {
            var problems = SpawnDistanceRingPlanner.Diagnose(new[] { -5f, 250f });

            Assert.AreEqual(1, problems.Count);
            Assert.IsTrue(problems[0].Contains("negative outer radius"));
        }

        [Test]
        public void 重複した外半径は診断される()
        {
            var problems = SpawnDistanceRingPlanner.Diagnose(new[] { 250f, 250f });

            Assert.AreEqual(1, problems.Count);
            Assert.IsTrue(problems[0].Contains("bands[1]"));
        }

        // BuildRingsがリングにしない帯は全て診断へ出す（規則を二重に書くと片方だけ取り残される）。
        // Every band BuildRings drops must show up in the diagnosis; restating the rules would leave one side behind.
        [Test]
        public void NaN外半径はリングにならないので診断される()
        {
            var problems = SpawnDistanceRingPlanner.Diagnose(new[] { 250f, float.NaN });

            Assert.AreEqual(1, problems.Count);
            Assert.IsTrue(problems[0].Contains("bands[1]"));
        }

        [Test]
        public void 外半径0はリングにならないので診断される()
        {
            var problems = SpawnDistanceRingPlanner.Diagnose(new[] { 0f, 250f });

            Assert.AreEqual(1, problems.Count);
            Assert.IsTrue(problems[0].Contains("bands[0]"));
        }

        private static ObjectScatterBand[] BandsOf(params float[] outerRadiusMeters)
        {
            var bands = new ObjectScatterBand[outerRadiusMeters.Length];
            for (var i = 0; i < outerRadiusMeters.Length; i++)
                bands[i] = new ObjectScatterBand { outerRadiusMeters = outerRadiusMeters[i] };
            return bands;
        }
    }
}
