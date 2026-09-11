using System.Linq;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BuildMenuModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Server.ConnectTool
{
    /// <summary>
    /// 解放フィルタ有無で同じ並び順を返すか検証
    /// Verifies the same ordering is returned regardless of the unlock filter
    /// </summary>
    public class ConnectToolSelectorCandidatesTest
    {
        private IGameUnlockStateDataController _unlockState;

        [SetUp]
        public void SetUp()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();
        }

        [Test]
        public void 解放無視なら全未解放でもelectricWire全件がSortPriority昇順で返る()
        {
            // 全て未解放が前提
            // All are unlocked=false by default
            Assert.IsTrue(_unlockState.ConnectToolUnlockStateInfos.Values.All(info => !info.IsUnlocked));

            var candidates = ConnectToolSelector.CandidatesByToolType(ConnectToolMasterElement.ToolTypeConst.electricWire, _unlockState, true).ToList();

            Assert.Less(0, candidates.Count);
            Assert.IsTrue(candidates.All(element => element.ToolType == ConnectToolMasterElement.ToolTypeConst.electricWire));
            for (var i = 1; i < candidates.Count; i++) Assert.LessOrEqual(candidates[i - 1].SortPriority, candidates[i].SortPriority);
        }

        [Test]
        public void 解放を見るなら全未解放では0件で解放後は解放分だけ返る()
        {
            Assert.AreEqual(0, ConnectToolSelector.CandidatesByToolType(ConnectToolMasterElement.ToolTypeConst.electricWire, _unlockState, false).Count());

            var first = ConnectToolSelector.CandidatesByToolType(ConnectToolMasterElement.ToolTypeConst.electricWire, _unlockState, true).First();
            _unlockState.UnlockConnectTool(first.ConnectToolGuid);

            var unlocked = ConnectToolSelector.CandidatesByToolType(ConnectToolMasterElement.ToolTypeConst.electricWire, _unlockState, false).ToList();
            Assert.AreEqual(1, unlocked.Count);
            Assert.AreEqual(first.ConnectToolGuid, unlocked[0].ConnectToolGuid);
        }
    }
}
