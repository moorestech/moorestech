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
    /// connectTool候補列挙が、解放フィルタの有無だけを切り替えて同じ並び順を返すことを検証する
    /// Verifies the connectTool candidate listing toggles only the unlock filter while keeping the same ordering
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
            // テストmodのconnectToolは全てinitialUnlocked=false
            // Every connectTool in the test mod starts locked
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

            // 既存APIは解放フィルタありと同じ結果
            // The existing API equals the filtered listing
            Assert.AreEqual(1, ConnectToolSelector.UnlockedByToolType(ConnectToolMasterElement.ToolTypeConst.electricWire, _unlockState).Count());
        }
    }
}
