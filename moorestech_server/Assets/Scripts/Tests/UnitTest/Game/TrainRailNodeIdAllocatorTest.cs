using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Train.RailGraph;
using NUnit.Framework;
using Tests.Util;

namespace Tests.UnitTest.Game
{
    public class TrainRailNodeIdAllocatorTest
    {
        private static readonly FieldInfo RailNodeToIdField = typeof(RailGraphDatastore).GetField("railNodeToId", BindingFlags.Instance | BindingFlags.NonPublic);

        // RailNodeIdAllocatorコメント例①: ペア解放後に単体割り当てで偶数IDが再利用される
        // Allocator comment example 1: single-node allocation reuses even id after releasing a pair
        [Test]
        public void ReleasedPairIsReusedBySingleAllocation()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var datastore = env.GetRailGraphDatastore();

            // 0/1, 2/3, 4/5の順でペアを生成
            // Create three consecutive pairs (0/1, 2/3, 4/5)
            _ = RailNode.CreatePair();
            var (targetFront, targetBack) = RailNode.CreatePair();
            _ = RailNode.CreatePair();

            var targetFrontId = GetNodeId(datastore, targetFront);
            var targetBackId = GetNodeId(datastore, targetBack);
            var expectedEven = Math.Min(targetFrontId, targetBackId);

            // コメント例通りに対象ペアを削除
            // Destroy the target pair as described in the comment
            targetFront.Destroy();
            targetBack.Destroy();

            // 単体ノードの追加で偶数ID (例では2) が戻る
            // Adding a single node should reuse the even id (2 in the comment)
            var single = RailNode.CreateSingleAndRegister();
            Assert.AreEqual(expectedEven, GetNodeId(datastore, single), "解放済みペアの偶数IDが再利用されていません。");
        }

        // RailNodeIdAllocatorコメント例①補足: ペア解放後に再びペアを生成すると両IDが戻る
        // Allocator comment example 1 (pair variant): pair allocation reuses both ids once released
        [Test]
        public void ReleasedPairIsReusedByPairAllocation()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var datastore = env.GetRailGraphDatastore();

            _ = RailNode.CreatePair(); // 0/1
            var (targetFront, targetBack) = RailNode.CreatePair(); // 2/3

            var expectedIds = new HashSet<int>
            {
                GetNodeId(datastore, targetFront),
                GetNodeId(datastore, targetBack)
            };

            targetFront.Destroy();
            targetBack.Destroy();

            var (reusedFront, reusedBack) = RailNode.CreatePair();
            var actualIds = new HashSet<int>
            {
                GetNodeId(datastore, reusedFront),
                GetNodeId(datastore, reusedBack)
            };
            Assert.That(actualIds.SetEquals(expectedIds), "解放済みペアのIDが再利用されていません。");
        }

        // RailNodeIdAllocatorコメント例②: 片側だけ解放すると次のRent()が新しいID(例:6)を返す
        // Allocator comment example 2 (first part): releasing only one side forces the next Rent() to return a new sequential id (6)
        [Test]
        public void HalfReleasedPairAllocatesNewSequentialId()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var datastore = env.GetRailGraphDatastore();

            _ = RailNode.CreatePair();
            var (targetFront, targetBack) = RailNode.CreatePair();
            var (lastFront, lastBack) = RailNode.CreatePair();

            var highestAssignedId = Math.Max(GetNodeId(datastore, lastFront), GetNodeId(datastore, lastBack));
            var expectedSequentialId = highestAssignedId + 1;
            var expectedOdd = Math.Max(GetNodeId(datastore, targetFront), GetNodeId(datastore, targetBack));
            var oddNode = GetNodeId(datastore, targetFront) == expectedOdd ? targetFront : targetBack;

            oddNode.Destroy();

            var singleAfterPartial = RailNode.CreateSingleAndRegister();
            Assert.AreEqual(expectedSequentialId, GetNodeId(datastore, singleAfterPartial), "片側のみ解放された状態で既存IDが再利用されています。");
        }

        // RailNodeIdAllocatorコメント例②: 両側を解放すれば古いID(例:2/3)が再び使える
        // Allocator comment example 2 (second part): releasing both sides makes the old pair ids reusable again
        [Test]
        public void FullReleaseAfterPartialRestoresPair()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var datastore = env.GetRailGraphDatastore();

            _ = RailNode.CreatePair();
            var (targetFront, targetBack) = RailNode.CreatePair();
            var (lastFront, lastBack) = RailNode.CreatePair();

            var expectedEven = Math.Min(GetNodeId(datastore, targetFront), GetNodeId(datastore, targetBack));
            var expectedOdd = Math.Max(GetNodeId(datastore, targetFront), GetNodeId(datastore, targetBack));

            var oddNode = GetNodeId(datastore, targetFront) == expectedOdd ? targetFront : targetBack;
            var evenNode = ReferenceEquals(oddNode, targetFront) ? targetBack : targetFront;

            oddNode.Destroy();
            _ = RailNode.CreateSingleAndRegister(); // Rent() -> 6
            evenNode.Destroy();

            var singleAfterFull = RailNode.CreateSingleAndRegister(); // Rent() -> 2
            Assert.AreEqual(expectedEven, GetNodeId(datastore, singleAfterFull), "両側解放後に偶数IDが戻っていません。");

            singleAfterFull.Destroy(); // 再度解放して奇数IDも確認
            var (pairFront, pairBack) = RailNode.CreatePair();
            var pairIds = new HashSet<int>
            {
                GetNodeId(datastore, pairFront),
                GetNodeId(datastore, pairBack)
            };
            Assert.That(pairIds.SetEquals(new[] { expectedEven, expectedOdd }), "両側解放後のペア割り当てで旧IDが戻っていません。");
        }

        // 単体ノード解放によるID再利用が歯抜けにならないことを確認
        // Ensure single-node ids are reused without gaps
        [Test]
        public void SingleNodeIdIsReusedAfterReturn()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var datastore = env.GetRailGraphDatastore();

            var firstSingle = RailNode.CreateSingleAndRegister();
            var secondSingle = RailNode.CreateSingleAndRegister();
            var firstId = GetNodeId(datastore, firstSingle);
            var secondId = GetNodeId(datastore, secondSingle);
            Assert.That(firstId >= 0 && secondId >= 0, "シングルノードIDの取得に失敗しました。");
            Assert.GreaterOrEqual(secondId - firstId, 2, "シングルノードのIDが偶数間隔になっていません。");

            firstSingle.Destroy();
            var recycled = RailNode.CreateSingleAndRegister();
            Assert.AreEqual(firstId, GetNodeId(datastore, recycled), "解放済みシングルIDが再利用されていません。");
            Assert.AreEqual(secondId, GetNodeId(datastore, secondSingle), "既存ノードのIDが変化しています。");
        }

        // RailGraphDatastore内部のIDマップを反射で取得するヘルパー
        // Helper to read the internal RailGraphDatastore node-id map via reflection
        private static int GetNodeId(RailGraphDatastore datastore, RailNode node)
        {
            if ((datastore == null) || (node == null))
                return -1;
            var map = (Dictionary<RailNode, int>)RailNodeToIdField.GetValue(datastore);
            return (map != null && map.TryGetValue(node, out var id)) ? id : -1;
        }
    }
}
