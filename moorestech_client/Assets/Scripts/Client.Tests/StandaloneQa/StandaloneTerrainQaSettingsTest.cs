using Client.Starter.StandaloneQa;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Args;

namespace Client.Tests.StandaloneQa
{
    public class StandaloneTerrainQaSettingsTest
    {
        [Test]
        public void TryParse_全引数から生成ワールド設定を作る()
        {
            var args = new[]
            {
                "moorestech",
                StandaloneTerrainQaSettings.Marker,
                "--qaServerDirectory", "/tmp/server",
                "--qaWorldDirectory", "/tmp/world",
                "--qaResultDirectory", "/tmp/result",
                "--qaSeed", "67890",
            };

            var success = StandaloneTerrainQaSettings.TryParse(args, out var settings, out var error);
            var proprieties = settings.CreateInitializeProprieties();
            var serverSettings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);

            Assert.That(success, Is.True, error);
            Assert.That(serverSettings.ServerDataDirectory, Is.EqualTo("/tmp/server"));
            Assert.That(serverSettings.WorldDirectory, Is.EqualTo("/tmp/world"));
            Assert.That(serverSettings.MapMode, Is.EqualTo("generated"));
            Assert.That(serverSettings.Seed, Is.EqualTo(67890));
            Assert.That(serverSettings.AutoSave, Is.False);
            Assert.That(settings.ResultDirectory, Is.EqualTo("/tmp/result"));
        }

        [TestCase("--qaServerDirectory")]
        [TestCase("--qaWorldDirectory")]
        [TestCase("--qaResultDirectory")]
        [TestCase("--qaSeed")]
        public void TryParse_必須引数の欠落を拒否する(string missingOption)
        {
            var args = CreateValidArgs();
            var index = System.Array.IndexOf(args, missingOption);
            args[index] = "--unknown";

            var success = StandaloneTerrainQaSettings.TryParse(args, out _, out var error);

            Assert.That(success, Is.False);
            Assert.That(error, Does.Contain(missingOption));
        }

        [Test]
        public void TryParse_seedの非整数を拒否する()
        {
            var args = CreateValidArgs();
            args[9] = "not-a-number";

            var success = StandaloneTerrainQaSettings.TryParse(args, out _, out var error);

            Assert.That(success, Is.False);
            Assert.That(error, Does.Contain("--qaSeed"));
        }

        [Test]
        public void TryParse_同じ引数の重複を拒否する()
        {
            var args = new[]
            {
                StandaloneTerrainQaSettings.Marker,
                "--qaServerDirectory", "/tmp/server-a",
                "--qaServerDirectory", "/tmp/server-b",
                "--qaWorldDirectory", "/tmp/world",
                "--qaResultDirectory", "/tmp/result",
                "--qaSeed", "12345",
            };

            var success = StandaloneTerrainQaSettings.TryParse(args, out _, out var error);

            Assert.That(success, Is.False);
            Assert.That(error, Does.Contain("exactly once"));
        }

        private static string[] CreateValidArgs()
        {
            return new[]
            {
                "moorestech",
                StandaloneTerrainQaSettings.Marker,
                "--qaServerDirectory", "/tmp/server",
                "--qaWorldDirectory", "/tmp/world",
                "--qaResultDirectory", "/tmp/result",
                "--qaSeed", "12345",
            };
        }
    }
}
