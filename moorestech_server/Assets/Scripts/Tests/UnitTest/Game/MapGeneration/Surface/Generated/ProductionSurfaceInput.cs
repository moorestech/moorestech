using System.IO;
using Core.Master;
using Game.MapGeneration.Identity;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Surface.Generated
{
    public sealed class ProductionSurfaceInput
    {
        public readonly string ServerData;
        public readonly string Fingerprint;

        public ProductionSurfaceInput()
        {
            // 不変の比較正本から本番入力を特定する
            // Identify production inputs from the immutable comparison baseline
            var repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var path = Path.Combine(repository,
                "moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Surface/Fixtures/legacy-v4-seed196.json");
            var golden = JObject.Parse(File.ReadAllText(path));
            ServerData = Path.GetFullPath(Path.Combine(repository, (string)golden["meta"]["serverDataDirectory"]));
            Assert.That(Directory.Exists(Path.Combine(ServerData, "mods")), Is.True, "Pinned production inputs required");
            var resources = new ModsResource(Path.Combine(ServerData, "mods"));
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(resources)));

            // JSONと配置PNGの指紋が採取時と一致する必要がある
            // Require JSON and placement PNG fingerprints to match the captured inputs
            Fingerprint = GenerationMasterFingerprint.Compute(MasterHolder.GenerationMaster.SourceJsonText,
                MasterHolder.GenerationMaster.SelectedGeneration, ServerData);
            Assert.That(Fingerprint, Is.EqualTo((string)golden["meta"]["generationMasterFingerprint"]));
        }
    }
}
