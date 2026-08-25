using System.IO;
using Client.DebugSystem.Environment;
using Client.Starter;
using Client.Starter.Editor;
using Common.Debug;
using Game.MapGeneration.Transfer;
using Game.Paths;
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

        // アセンブリ全体を隔離しているClientTestsDebugParametersIsolationFixtureのoverride先
        // The override set by ClientTestsDebugParametersIsolationFixture, which isolates the whole assembly
        private string _fixtureCacheDirectory;

        [SetUp]
        public void SetUp()
        {
            // SessionStateはエディタセッション中生存するため、毎テスト明示的に倒して初期状態を固定する
            // SessionState survives the whole editor session, so reset it per test to pin the starting state
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, false);
            _fixtureCacheDirectory = DebugParametersCacheDirectory.GetOverride();
        }

        [TearDown]
        public void TearDown()
        {
            // フラグ残置は後続テストの起動引数を汚染するため必ず戻す
            // A leftover flag pollutes launch args of later tests, so always reset it
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, false);

            // override残置はアセンブリ全体の隔離を壊すため、fixtureが張った値へ確実に戻す
            // A leftover override breaks the assembly-wide isolation, so restore the fixture's value for sure
            DebugParametersCacheDirectory.SetOverride(_fixtureCacheDirectory);
        }

        [Test]
        public void フラグ有効時はworld_generatedとgeneratedモードへ書き換える()
        {
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, true);
            var proprieties = InitializeProprieties.CreateLocalServer(null);

            GeneratedWorldPlayModeSettings.ApplyIfNeeded(proprieties);

            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            Assert.That(settings.WorldDirectory, Is.EqualTo(GameSystemPaths.GetSaveFilePath("world_generated")));
            Assert.That(settings.MapMode, Is.EqualTo(WorldMapMode.Generated));
            Assert.That(settings.AutoSave, Is.True);
        }

        [Test]
        public void フラグ無効時は起動引数を変更しない()
        {
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, false);
            var proprieties = InitializeProprieties.CreateLocalServer(null);

            GeneratedWorldPlayModeSettings.ApplyIfNeeded(proprieties);

            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            var defaultSettings = new StartServerSettings();
            Assert.That(settings.MapMode, Is.EqualTo(defaultSettings.MapMode));
            Assert.That(settings.WorldDirectory, Is.EqualTo(defaultSettings.WorldDirectory));
        }

        [Test]
        public void フラグ有効でもworldDirectoryとmapMode以外の指定は保持する()
        {
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, true);

            // 既定値と異なる値を渡し、上書き対象が本当に2項目だけかを検出できるようにする
            // Pass non-default values so an overwrite beyond the intended two fields is detectable
            var original = new StartServerSettings
            {
                WorldDirectory = "/tmp/moorestech-test-world",
                MapMode = WorldMapMode.Template,
                Seed = 4321,
                Port = 21564,
                AutoSave = false,
                ServerDataDirectory = "/tmp/moorestech-test-server-data",
            };
            var proprieties = InitializeProprieties.CreateLocalServer(null);
            proprieties.CreateLocalServerArgs = CliConvert.Serialize(original);

            GeneratedWorldPlayModeSettings.ApplyIfNeeded(proprieties);

            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            Assert.That(settings.WorldDirectory, Is.EqualTo(GameSystemPaths.GetSaveFilePath("world_generated")));
            Assert.That(settings.MapMode, Is.EqualTo(WorldMapMode.Generated));
            Assert.That(settings.Seed, Is.EqualTo(4321));
            Assert.That(settings.Port, Is.EqualTo(21564));
            Assert.That(settings.AutoSave, Is.False);
            Assert.That(settings.ServerDataDirectory, Is.EqualTo("/tmp/moorestech-test-server-data"));
        }

        [Test]
        public void 隔離開始で一時cacheへ切替えRuntimeを書き込む()
        {
            GeneratedWorldPlayModeSettings.BeginIsolatedDebugEnvironment();

            Assert.That(DebugParametersCacheDirectory.GetOverride(), Is.EqualTo(GeneratedWorldPlayModeSettings.DebugCacheDirectory));
            Assert.That(DebugParameters.GetValueOrDefaultInt(DebugEnvironmentTypeKey, (int)DebugEnvironmentType.Debug), Is.EqualTo((int)DebugEnvironmentType.Runtime));
        }

        [Test]
        public void 隔離終了でoverrideが解除される()
        {
            GeneratedWorldPlayModeSettings.BeginIsolatedDebugEnvironment();

            GeneratedWorldPlayModeSettings.EndIsolatedDebugEnvironment();

            Assert.That(DebugParametersCacheDirectory.GetOverride(), Is.Null);
        }

        [Test]
        public void 他人のoverrideが張られている時は隔離終了しても外さない()
        {
            // fixtureやPlaytestが張った隔離を巻き添えで剥がさないことを保証する
            // Guarantee that isolation set up by a fixture or playtest is not torn down as collateral
            var otherDirectory = Path.Combine(Path.GetTempPath(), "moorestech-other-owner-debug-cache");
            DebugParametersCacheDirectory.SetOverride(otherDirectory);

            GeneratedWorldPlayModeSettings.EndIsolatedDebugEnvironment();

            Assert.That(DebugParametersCacheDirectory.GetOverride(), Is.EqualTo(otherDirectory));
        }
    }
}
