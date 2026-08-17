using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Mining;
using Client.Game.InGame.SoundEffect;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Mining.Outcrop
{
    /// <summary>
    ///     露頭の採掘契約を検証
    ///     Verify outcrop mining contract
    /// </summary>
    public class OutcropMiningTargetTest
    {
        private static readonly Guid IronVeinGuid = new("11111111-0000-0000-0000-000000000001");
        private static readonly Guid UnmineableVeinGuid = new("11111111-0000-0000-0000-000000000004");
        private static readonly Guid ToolItemGuid = new("00000000-0000-0000-1234-000000000001");
        private static readonly Guid UnmatchedToolItemGuid = new("00000000-0000-0000-1234-000000000004");

        private readonly List<GameObject> _createdObjects = new();
        private GameObject _colliderChild;
        private OutcropGameObject _outcrop;

        [SetUp]
        public void SetUp()
        {
            // 実ローダーでマスタ初期化
            // Initialize master through real loader
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            _outcrop = CreateOutcrop(IronVeinGuid, out _colliderChild);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
                UnityEngine.Object.DestroyImmediate(createdObject);
            _createdObjects.Clear();
        }

        [Test]
        public void 初期化時に子コライダへ自身を指す露頭マーカーを注入する()
        {
            // 子Collider退行を検出
            // Catch child-collider regression
            var rayTarget = _colliderChild.GetComponent<OutcropRayTarget>();
            Assert.IsNotNull(rayTarget);
            Assert.AreSame(_outcrop, rayTarget.OutcropGameObject);
            Assert.AreSame(_outcrop, ((IMiningRayTarget)rayTarget).MiningTargetObject);
        }

        [Test]
        public void マスタで許可されたツールだけを攻撃間隔付きで解決する()
        {
            var toolItemId = MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
            var unmatchedItemId = MasterHolder.ItemMaster.GetItemId(UnmatchedToolItemGuid);

            // 解決値をマスタへ固定
            // Pin resolution to master
            Assert.AreEqual(MiningStartOutcome.Ready, _outcrop.TryBeginHandMining(toolItemId, out var tool, out var recommended));
            Assert.AreEqual(0.2f, tool.AttackSpeed, 0.0001f);
            CollectionAssert.AreEqual(new[] { toolItemId }, recommended);
            Assert.AreEqual(MiningStartOutcome.ToolMismatch, _outcrop.TryBeginHandMining(unmatchedItemId, out _, out _));
            Assert.AreEqual(MiningStartOutcome.ToolMismatch, _outcrop.TryBeginHandMining(ItemMaster.EmptyItemId, out _, out _));
        }

        [Test]
        public void handMiningTypeがnoneの鉱脈はどの装備でも手掘り不可を返す()
        {
            // 掘れない露頭を掘れないと示すことがPRの目的なので、装備に依らず不可であることを固定する
            // Declaring an unmineable outcrop unmineable is this feature's goal, so pin refusal regardless of equipment
            var unmineableOutcrop = CreateOutcrop(UnmineableVeinGuid, out _);
            var toolItemId = MasterHolder.ItemMaster.GetItemId(ToolItemGuid);

            Assert.AreEqual(MiningStartOutcome.HandMiningNotAllowed, unmineableOutcrop.TryBeginHandMining(toolItemId, out _, out var recommended));
            Assert.IsEmpty(recommended);
            Assert.AreEqual(MiningStartOutcome.HandMiningNotAllowed, unmineableOutcrop.TryBeginHandMining(ItemMaster.EmptyItemId, out _, out _));
        }

        [Test]
        public void 掘れない露頭にも採掘レイマーカーを注入する()
        {
            // レイを吸わせないと「手掘りできません」の提示自体が起きない
            // Without absorbing the ray the refusal message would never be shown at all
            var unmineableOutcrop = CreateOutcrop(UnmineableVeinGuid, out var colliderChild);

            var rayTarget = colliderChild.GetComponent<OutcropRayTarget>();
            Assert.IsNotNull(rayTarget);
            Assert.AreSame(unmineableOutcrop, rayTarget.OutcropGameObject);
        }

        [Test]
        public void 破壊音はIronVeinマスタのstone設定を使う()
        {
            // ForUnitTestにminableなtree鉱脈が無いため、既存stone側のマスタ駆動を固定する
            // The fixture has no minable tree vein, so pin the existing master-driven stone side
            Assert.AreEqual(SoundEffectType.DestroyStone, _outcrop.DestroySoundType);
        }

        private OutcropGameObject CreateOutcrop(Guid veinGuid, out GameObject colliderChild)
        {
            var outcropObject = new GameObject($"outcrop-{veinGuid}");
            _createdObjects.Add(outcropObject);
            colliderChild = new GameObject("collider");
            colliderChild.transform.SetParent(outcropObject.transform);
            colliderChild.AddComponent<BoxCollider>();

            var outcrop = outcropObject.AddComponent<OutcropGameObject>();
            outcrop.Initialize(MasterHolder.MapVeinMaster.GetElementOrNull(veinGuid), veinGuid, new Vector3Int(0, 5, 0));
            return outcrop;
        }
    }
}
