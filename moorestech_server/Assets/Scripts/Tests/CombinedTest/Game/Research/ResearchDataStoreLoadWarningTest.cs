using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Research;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Game.ResearchDataStoreTest;

namespace Tests.CombinedTest.Game.Research
{
    public class ResearchDataStoreLoadWarningTest
    {
        private const string UnreadableGuid = "not-a-guid";
        private const string UnknownValidGuid = "ffffffff-ffff-ffff-ffff-ffffffffffff";

        [Test]
        public void 読めないGUIDとマスタに無い研究は警告を残して読み飛ばすTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var researchDataStore = serviceProvider.GetService<IResearchDataStore>();
            LogAssert.Expect(LogType.Warning, new Regex("GUID として読めない値.*value=not-a-guid"));
            LogAssert.Expect(LogType.Warning, new Regex("マスタに無いため.*researchGuid=ffffffff-ffff-ffff-ffff-ffffffffffff"));

            researchDataStore.LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = new List<string> { UnreadableGuid, UnknownValidGuid, Research1Guid.ToString() } });

            // 正常な研究は従来どおり完了として読み込まれる
            // A valid research still loads as completed
            Assert.IsTrue(researchDataStore.IsResearchCompleted(Research1Guid));

            // 読めないGUIDは完了一覧に入らず、マスタに無い有効なGUIDは完了に残る
            // The unreadable GUID stays out of the completed list, while the unknown valid GUID stays in it
            var completedGuids = researchDataStore.GetSaveJsonObject().CompletedResearchGuids;
            Assert.IsFalse(completedGuids.Contains(UnreadableGuid));
            Assert.IsTrue(completedGuids.Contains(UnknownValidGuid));
            Assert.IsTrue(completedGuids.Contains(Research1Guid.ToString()));
            Assert.AreEqual(2, completedGuids.Count);
        }
    }
}
