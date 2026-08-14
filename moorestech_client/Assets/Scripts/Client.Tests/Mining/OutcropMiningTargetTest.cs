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
    ///     露頭の採掘契約を検証
    ///     Verify outcrop mining contract
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
            // 実ローダーでマスタ初期化
            // Initialize master through real loader
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            _outcropObject = new GameObject("outcrop");
            _colliderChild = new GameObject("collider");
            _colliderChild.transform.SetParent(_outcropObject.transform);
            _colliderChild.AddComponent<BoxCollider>();
            _outcrop = _outcropObject.AddComponent<OutcropGameObject>();
            _outcrop.Initialize(MasterHolder.MapVeinMaster.GetElementOrNull(IronVeinGuid), IronVeinGuid, new Vector3Int(0, 5, 0));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_outcropObject);
        }

        [Test]
        public void 初期化時に子コライダへ自身を指す露頭マーカーを注入する()
        {
            // 子Collider退行を検出
            // Catch child-collider regression
            var rayTarget = _colliderChild.GetComponent<OutcropRayTarget>();
            Assert.IsNotNull(rayTarget);
            Assert.AreSame(_outcrop, rayTarget.OutcropGameObject);
        }

        [Test]
        public void マスタで許可されたツールだけを攻撃間隔付きで解決する()
        {
            var toolItemId = MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
            var unmatchedItemId = MasterHolder.ItemMaster.GetItemId(UnmatchedToolItemGuid);

            // 解決値をマスタへ固定
            // Pin resolution to master
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
