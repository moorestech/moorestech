using System.IO;
using System.Linq;
using Core.Master;
using Game.UnlockState.Holders;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Block
{
    public class BuildToolMasterTest
    {
        [Test]
        public void BuildToolsをマスタからロードできる()
        {
            // DIコンテナ生成でMasterHolderがロードされる
            // Building the DI container loads MasterHolder
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            Assert.AreEqual(1, MasterHolder.BuildToolMaster.All.Count);
            var tool = MasterHolder.BuildToolMaster.All[0];
            Assert.AreEqual("blueprintCopy", tool.ToolType);
            Assert.AreEqual(tool.BuildToolGuid, MasterHolder.BuildToolMaster.GetBuildTool(tool.BuildToolGuid).BuildToolGuid);
        }

        // マスタのシード値がBP解放状態の初期値まで届いていることを検証する（ADR 0015）
        // Verifies the master seed value reaches the initial blueprint unlock state (ADR 0015)
        [Test]
        public void blueprintInitialUnlockedがBP解放の初期値をシードする()
        {
            LoadMasterWithBlueprintInitialUnlocked(true);
            Assert.IsTrue(new BlueprintUnlockStateHolder().IsUnlocked);

            // 既定のテストマスタ(false)で未解放を確かめつつ、静的なMasterHolderを通常状態へ戻す
            // Confirms the locked default (false) while restoring the static MasterHolder to its normal state
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.IsFalse(new BlueprintUnlockStateHolder().IsUnlocked);

            #region Internal

            void LoadMasterWithBlueprintInitialUnlocked(bool initialUnlocked)
            {
                var modResource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
                var configs = ModJsonStringLoader.GetMasterString(modResource);

                // buildMenuのシードキーだけ差し替え、他のマスタは実ファイルのまま読ませる
                // Swaps only the buildMenu seed key, leaving every other master as the real file
                var buildMenuFileName = new JsonFileName("buildMenu");
                var buildMenu = JObject.Parse(configs[0].JsonContents[buildMenuFileName]);
                buildMenu["blueprintInitialUnlocked"] = initialUnlocked;
                configs[0].JsonContents[buildMenuFileName] = buildMenu.ToString();

                MasterHolder.Load(new MasterJsonFileContainer(configs));
            }

            #endregion
        }
    }
}
