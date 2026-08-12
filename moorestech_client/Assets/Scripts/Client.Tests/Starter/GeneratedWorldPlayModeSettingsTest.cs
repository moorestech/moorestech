using Client.Starter;
using Client.Starter.Editor;
using Game.MapGeneration.Provisioning;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Args;
using UnityEditor;

namespace Client.Tests.Starter
{
    public class GeneratedWorldPlayModeSettingsTest
    {
        [TearDown]
        public void TearDown()
        {
            // フラグ残置は後続テストの起動引数を汚染するため必ず戻す
            // A leftover flag pollutes launch args of later tests, so always reset it
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, false);
        }

        [Test]
        public void フラグ有効時はworld_generatedとgeneratedモードへ書き換える()
        {
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, true);
            var proprieties = InitializeProprieties.CreateDefault();

            GeneratedWorldPlayModeSettings.ApplyIfNeeded(proprieties);

            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            Assert.That(settings.WorldDirectory, Is.EqualTo(GeneratedWorldPlayModeSettings.WorldDirectoryPath));
            Assert.That(settings.MapMode, Is.EqualTo(WorldProvisioner.GeneratedMapMode));
            Assert.That(settings.AutoSave, Is.True);
        }

        [Test]
        public void フラグ無効時は起動引数を変更しない()
        {
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, false);
            var proprieties = InitializeProprieties.CreateDefault();

            GeneratedWorldPlayModeSettings.ApplyIfNeeded(proprieties);

            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            Assert.That(settings.MapMode, Is.EqualTo(WorldProvisioner.TemplateMapMode));
            Assert.That(settings.WorldDirectory, Does.Not.Contain("world_generated"));
        }
    }
}
