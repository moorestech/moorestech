using System.Linq;
using Client.Tests.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Context;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 文脈の 3 欄 Model の配り直し（追跡替えと閉・ポーリング更新の通知）
    /// Republishing of the context-entries model (tracking changes, closing and poll-update notifications)
    /// </summary>
    public class BlockContextModelTest
    {
        private TrackedBlockHarness _harness;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _harness = new TrackedBlockHarness();
        }

        [TearDown]
        public void TearDown() => _harness.Dispose();

        [Test]
        public void 追跡の無い間と部屋も機械でもないブロックの追跡中は3欄とも無し()
        {
            var model = new BlockContextModel(_harness.Tracker, new BlockNetworkInfoCache());
            model.Initialize();
            Assert.AreSame(BlockContextViewModel.None, model.Current.Value);

            _harness.Track(_harness.CreateBlock(new Vector3Int(1, 0, 1), MasterHolder.BlockMaster.Blocks.Data.First()));
            Assert.IsFalse(model.Current.Value.HasAny, "ポーラーを始めていない追跡は 3 欄とも追わない");

            _harness.Track(null);
            Assert.AreSame(BlockContextViewModel.None, model.Current.Value, "閉");
        }
    }
}
