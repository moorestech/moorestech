using Client.Game.InGame.ColliderStreaming;
using NUnit.Framework;
using UnityEngine;
using static Client.Tests.CullingBounds;

namespace Client.Tests
{
    /// <summary>
    /// チャンクテーブルの差分オンオフ・参照カウント・多重チャンク挙動を網羅するテスト
    /// Covers the chunk table's delta on/off, refcounting, and multi-chunk behavior
    /// </summary>
    public class ColliderCullingChunkTableTest
    {
        private const float ChunkSize = 10f;
        private const float Radius = 15f;

        [Test]
        public void Add_BeforeFirstUpdate_DoesNotToggle()
        {
            // 初回評価前は既存の有効状態を尊重し一切触らない
            // Before the first evaluation, respect the authored enabled state and touch nothing
            var table = new ColliderCullingChunkTable(ChunkSize);
            var far = new FakeCullingTarget();
            table.Add(Point(1000f, 1000f), far);

            Assert.AreEqual(0, far.TotalCalls);
        }

        [Test]
        public void FirstUpdate_TurnsOffFar_LeavesNearOn()
        {
            // 初回評価で遠方をoffにし、近傍は既定onのまま触らない
            // First evaluation turns off far entries and leaves near ones on (untouched)
            var table = new ColliderCullingChunkTable(ChunkSize);
            var near = new FakeCullingTarget();
            var far = new FakeCullingTarget();
            table.Add(Point(0f, 0f), near);
            table.Add(Point(1000f, 1000f), far);

            table.UpdateForCenter(Vector3.zero, Radius);

            Assert.AreEqual(0, near.TotalCalls, "near stays on, no flip");
            Assert.AreEqual(0, far.OnCount);
            Assert.AreEqual(1, far.OffCount);
        }

        [Test]
        public void MovingNearThenAway_TogglesOnThenOff()
        {
            // 遠方へ移動でoff、再接近でonへ戻る
            // Move away -> off, come back -> on again
            var table = new ColliderCullingChunkTable(ChunkSize);
            var target = new FakeCullingTarget();
            table.Add(Point(0f, 0f), target);

            table.UpdateForCenter(Vector3.zero, Radius); // near: stays on
            Assert.AreEqual(0, target.TotalCalls);

            table.UpdateForCenter(new Vector3(1000f, 0f, 1000f), Radius); // away: off
            Assert.AreEqual(1, target.OffCount);
            Assert.AreEqual(false, target.Last);

            table.UpdateForCenter(Vector3.zero, Radius); // back: on
            Assert.AreEqual(1, target.OnCount);
            Assert.AreEqual(true, target.Last);
        }

        [Test]
        public void RepeatedUpdate_SameMembership_NoRedundantToggles()
        {
            // メンバーシップが変わらない再評価では余計なオンオフを出さない
            // Re-evaluations that don't change membership emit no redundant toggles
            var table = new ColliderCullingChunkTable(ChunkSize);
            var near = new FakeCullingTarget();
            var far = new FakeCullingTarget();
            table.Add(Point(0f, 0f), near);
            table.Add(Point(1000f, 1000f), far);

            table.UpdateForCenter(Vector3.zero, Radius);
            var nearBefore = near.TotalCalls;
            var farBefore = far.TotalCalls;

            // 近傍内で微小移動を何度繰り返しても追加のトグルは0
            table.UpdateForCenter(new Vector3(1f, 0f, 1f), Radius);
            table.UpdateForCenter(new Vector3(2f, 0f, 0f), Radius);
            table.UpdateForCenter(Vector3.zero, Radius);

            Assert.AreEqual(nearBefore, near.TotalCalls);
            Assert.AreEqual(farBefore, far.TotalCalls);
        }

        [Test]
        public void MultiChunkEntry_StaysOn_WhileAnyChunkInRange()
        {
            // チャンク境界をまたぐ大きなコライダーは1チャンクでも範囲内なら点灯を保つ
            // A collider spanning chunk borders stays on while any one chunk is in range
            var table = new ColliderCullingChunkTable(ChunkSize);
            var big = new FakeCullingTarget();
            // z 0..35 spans chunks (0,0),(0,1),(0,2),(0,3)
            table.Add(Span(0f, 0f, 5f, 35f), big);

            // 遠端チャンク付近に立つ。近い端だけが範囲内でも点灯継続
            table.UpdateForCenter(new Vector3(0f, 0f, 35f), Radius);
            Assert.AreEqual(0, big.TotalCalls, "at least one chunk in range -> stays on");

            // 全チャンクが範囲外になった時だけ、しかも1回だけoff
            table.UpdateForCenter(new Vector3(1000f, 0f, 1000f), Radius);
            Assert.AreEqual(1, big.OffCount, "off exactly once despite spanning many chunks");
            Assert.AreEqual(0, big.OnCount);
        }

        [Test]
        public void MultiChunkEntry_PartialExit_DoesNotToggle()
        {
            // 一部チャンクが範囲外になっても、残りが範囲内なら状態は変わらない
            // Partial exit (some chunks leave) keeps state unchanged while others remain in range
            var table = new ColliderCullingChunkTable(ChunkSize);
            var big = new FakeCullingTarget();
            table.Add(Span(0f, 0f, 5f, 35f), big);

            table.UpdateForCenter(new Vector3(0f, 0f, 15f), Radius); // several chunks in range
            Assert.AreEqual(0, big.TotalCalls);

            // 上端チャンクだけ範囲外に落ちるが下端は残る -> トグルなし
            table.UpdateForCenter(new Vector3(0f, 0f, 5f), Radius);
            Assert.AreEqual(0, big.TotalCalls, "still on, no redundant toggle");
        }

        [Test]
        public void Add_AfterInit_EvaluatesImmediately()
        {
            // 初期化後に登録された対象は現在位置で即オンオフ判定される
            // Targets registered after init are evaluated immediately against the current position
            var table = new ColliderCullingChunkTable(ChunkSize);
            table.UpdateForCenter(Vector3.zero, Radius); // initialize with empty table

            var far = new FakeCullingTarget();
            var near = new FakeCullingTarget();
            table.Add(Point(1000f, 1000f), far);
            table.Add(Point(0f, 0f), near);

            Assert.AreEqual(1, far.OffCount, "far registered after init -> off now");
            Assert.AreEqual(0, near.TotalCalls, "near registered after init -> stays on");
        }

        [Test]
        public void Dispose_Unregisters_NoFurtherToggles()
        {
            // 解除後はプレイヤーが動いても対象に指示が飛ばない
            // After removal, no instructions reach the target even as the player moves
            var table = new ColliderCullingChunkTable(ChunkSize);
            var target = new FakeCullingTarget();
            var handle = table.Add(Point(0f, 0f), target);

            table.UpdateForCenter(Vector3.zero, Radius);
            handle.Dispose();

            table.UpdateForCenter(new Vector3(1000f, 0f, 1000f), Radius);
            table.UpdateForCenter(Vector3.zero, Radius);
            Assert.AreEqual(0, target.TotalCalls, "removed target never toggled again");
        }

        [Test]
        public void ManyEntries_OnlyFlippedChunksToggle()
        {
            // 多数エントリのうち、実際に境界を跨いだ側だけがトグルされる
            // Among many entries, only those whose chunk actually flips get toggled
            var table = new ColliderCullingChunkTable(ChunkSize);
            var stayNear = new FakeCullingTarget();
            var boundary = new FakeCullingTarget();
            table.Add(Point(0f, 0f), stayNear);   // chunk (0,0)
            table.Add(Point(0f, 20f), boundary);  // chunk (0,2), near only for higher z

            table.UpdateForCenter(new Vector3(0f, 0f, 10f), Radius); // both in range
            Assert.AreEqual(0, stayNear.TotalCalls);
            Assert.AreEqual(0, boundary.TotalCalls);

            // 少しだけ下へ動くと boundary(0,2) だけが範囲外へ落ち、stayNear(0,0) は範囲内のまま
            table.UpdateForCenter(new Vector3(0f, 0f, 3f), Radius);
            Assert.AreEqual(0, stayNear.TotalCalls, "unaffected entry not touched");
            Assert.AreEqual(1, boundary.OffCount, "only the flipped entry toggled");
        }
    }
}
