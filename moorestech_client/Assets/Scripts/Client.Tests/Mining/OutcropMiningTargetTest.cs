using System;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.SoundEffect;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Mining
{
    /// <summary>
    ///     露頭の採掘対象契約がマスタ定義と子コライダへ反映されることを検証する
    ///     Verifies that outcrop mining contracts reflect master data and child colliders
    /// </summary>
    public class OutcropMiningTargetTest
    {
        private static readonly Guid IronVeinGuid = new("11111111-0000-0000-0000-000000000001");
        private static readonly Guid ToolItemGuid = new("00000000-0000-0000-1234-000000000001");
        private static readonly Guid UnmatchedToolItemGuid = new("00000000-0000-0000-1234-000000000004");

        private GameObject _outcropObject;
        private GameObject _colliderChild;
        private OutcropGameObject _outcrop;

        [SetUp]
        public void SetUp()
        {
            // 実運用と同じローダーでForUnitTestマスタを毎テスト初期化する
            // Initialize the ForUnitTest master per test through the production loader
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            _outcropObject = new GameObject("outcrop");
            _colliderChild = new GameObject("collider");
            _colliderChild.transform.SetParent(_outcropObject.transform);
            _colliderChild.AddComponent<BoxCollider>();
            _outcrop = _outcropObject.AddComponent<OutcropGameObject>();
            _outcrop.Initialize(MasterHolder.MapVeinMaster.GetElementOrNull(IronVeinGuid), new Vector3Int(0, 5, 0));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_outcropObject);
        }

        [Test]
        public void 初期化時に子コライダへ自身を指す露頭マーカーを注入する()
        {
            // 子コライダのヒットから同一露頭を引けない退行を検出する
            // Catch regressions where a child-collider hit cannot resolve its owning outcrop
            var rayTarget = _colliderChild.GetComponent<OutcropRayTarget>();
            Assert.IsNotNull(rayTarget);
            Assert.AreSame(_outcrop, rayTarget.OutcropGameObject);
        }

        [Test]
        public void マスタで許可されたツールだけを攻撃間隔付きで解決する()
        {
            var toolItemId = MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
            var unmatchedItemId = MasterHolder.ItemMaster.GetItemId(UnmatchedToolItemGuid);

            // 推奨一覧と実際の解決結果を同じマスタ値へ固定する
            // Pin both the recommendation list and resolution result to the same master value
            CollectionAssert.AreEqual(new[] { toolItemId }, _outcrop.UsableToolItemIds);
            Assert.IsTrue(_outcrop.TryResolveUsableTool(toolItemId, out var tool));
            Assert.AreEqual(0.2f, tool.AttackSpeed, 0.0001f);
            Assert.IsFalse(_outcrop.TryResolveUsableTool(unmatchedItemId, out _));
            Assert.IsFalse(_outcrop.TryResolveUsableTool(ItemMaster.EmptyItemId, out _));
        }

        [Test]
        public void 破壊音はIronVeinマスタのstone設定を使う()
        {
            // ForUnitTestにminableなtree鉱脈が無いため、既存stone側のマスタ駆動を固定する
            // The fixture has no minable tree vein, so pin the existing master-driven stone side
            Assert.AreEqual(SoundEffectType.DestroyStone, _outcrop.DestroySoundType);
        }
    }
}
