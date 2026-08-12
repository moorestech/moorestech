using Client.DebugSystem.Environment;
using Client.Starter;
using Client.Starter.Editor;
using Common.Debug;
using Game.MapGeneration.Provisioning;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Args;
using UnityEditor;

namespace Client.Tests.Starter
{
    public class GeneratedWorldPlayModeSettingsTest
    {
        // DebugEnvironmentController等と重複定義（各所で共有されているprivate constの既存流儀）
        // Duplicated from DebugEnvironmentController (existing convention of a shared private const per file)
        private const string DebugEnvironmentTypeKey = "DebugEnvironmentTypeKey";
        private int _originalDebugEnvironmentTypeValue;

        [SetUp]
        public void SetUp()
        {
            // 実機環境の設定を壊さないよう元値を退避しておく
            // Save the original value so the real editor environment is not corrupted
            _originalDebugEnvironmentTypeValue = DebugParameters.GetValueOrDefaultInt(DebugEnvironmentTypeKey, (int)DebugEnvironmentType.Debug);
        }

        [TearDown]
        public void TearDown()
        {
            // フラグ残置は後続テストの起動引数を汚染するため必ず戻す
            // A leftover flag pollutes launch args of later tests, so always reset it
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, false);

            // 退避マーカーが残っていれば消化し、値も元へ確実に戻す
            // Consume any leftover restore marker and make sure the value is put back
            GeneratedWorldPlayModeSettings.RestoreDebugEnvironmentIfNeeded();
            DebugParameters.SaveInt(DebugEnvironmentTypeKey, _originalDebugEnvironmentTypeValue);
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

        [Test]
        public void デバッグ環境の切替えは旧値退避後にRuntimeへ上書きし復元で元へ戻す()
        {
            DebugParameters.SaveInt(DebugEnvironmentTypeKey, (int)DebugEnvironmentType.Other);

            GeneratedWorldPlayModeSettings.ApplyDebugEnvironmentOverride();
            Assert.That(DebugParameters.GetValueOrDefaultInt(DebugEnvironmentTypeKey, (int)DebugEnvironmentType.Debug), Is.EqualTo((int)DebugEnvironmentType.Runtime));

            GeneratedWorldPlayModeSettings.RestoreDebugEnvironmentIfNeeded();
            Assert.That(DebugParameters.GetValueOrDefaultInt(DebugEnvironmentTypeKey, (int)DebugEnvironmentType.Debug), Is.EqualTo((int)DebugEnvironmentType.Other));
        }

        [Test]
        public void 退避していない状態での復元は通常再生の設定を上書きしない()
        {
            DebugParameters.SaveInt(DebugEnvironmentTypeKey, (int)DebugEnvironmentType.PureNature);

            GeneratedWorldPlayModeSettings.RestoreDebugEnvironmentIfNeeded();

            Assert.That(DebugParameters.GetValueOrDefaultInt(DebugEnvironmentTypeKey, (int)DebugEnvironmentType.Debug), Is.EqualTo((int)DebugEnvironmentType.PureNature));
        }

        [Test]
        public void 連続適用でも最初の退避値だけが復元される()
        {
            DebugParameters.SaveInt(DebugEnvironmentTypeKey, (int)DebugEnvironmentType.Other);

            // EnterPlaymode失敗などでRestore未実行のまま再クリックされる経路を模擬する
            // Simulate a re-click while Restore never ran, e.g. after a failed EnterPlaymode
            GeneratedWorldPlayModeSettings.ApplyDebugEnvironmentOverride();
            GeneratedWorldPlayModeSettings.ApplyDebugEnvironmentOverride();

            GeneratedWorldPlayModeSettings.RestoreDebugEnvironmentIfNeeded();
            Assert.That(DebugParameters.GetValueOrDefaultInt(DebugEnvironmentTypeKey, (int)DebugEnvironmentType.Debug), Is.EqualTo((int)DebugEnvironmentType.Other));
        }
    }
}
