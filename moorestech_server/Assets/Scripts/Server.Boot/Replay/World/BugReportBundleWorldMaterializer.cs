using System.IO;
using Game.MapGeneration.Provisioning;
using Game.Paths;

namespace Server.Boot.Replay.World
{
    // 箱から地形付きのワールドを world-materialized/ に実体化する。固定ワールド起動（自動修正ランの観察）は world.json だけの箱では地形が無く落ちる（ADR 0064）
    // Materializes a terrain-bearing world from the box into world-materialized/; a fixed-world boot (the auto-fix observation) dies without terrain on a world.json-only box (ADR 0064)
    public static class BugReportBundleWorldMaterializer
    {
        public static BugReportBundleWorldResolution Materialize(string bundleDirectory, string serverDataDirectory)
        {
            // 引き当ては再生と同じ resolver に委ねる。別経路で探すと再生と観察で違うワールドを使いうる
            // Locating goes through the same resolver as replay; a separate search could let replay and observation use different worlds
            var resolution = BugReportBundleWorldResolver.Resolve(bundleDirectory, serverDataDirectory);
            if (!resolution.TryGetWorld(out var source, out _)) return resolution;

            // 起動側は world.json の無い置き場を破損として拒む。full の箱でも world.json が欠けていれば実体化しても起動できない
            // The boot side refuses a world directory without world.json as corrupt; even a full box missing world.json cannot boot once materialized
            if (!File.Exists(source.WorldMetaFilePath)) return BugReportBundleWorldResolution.Rejected($"引き当てたワールドに world.json が無く、固定ワールド起動に使えません root:{source.Root}");

            // 一時ディレクトリに書き切ってからリネームで確定する（WorldSnapshotStore と同じ規約）。途中で落ちても半端な置き場が残らない
            // Write everything into a temp directory and commit by rename (WorldSnapshotStore's rule), so an interruption never leaves a half-built world
            var destination = WorldDataDirectory.FromWorldRoot(Path.Combine(bundleDirectory, BugReportBundleLayout.MaterializedWorldDirectoryName));
            var temp = WorldDataDirectory.FromWorldRoot(destination.ProvisioningTempDirectory);
            if (Directory.Exists(temp.Root)) Directory.Delete(temp.Root, true);
            Directory.CreateDirectory(temp.Root);

            // 置き場には prepare-run.sh が置いた save.json があるので、一時側へ引き継いでからワールド本体を写す
            // The destination holds the save.json prepare-run.sh placed, so it is carried into the temp side before the world core is copied
            if (File.Exists(destination.SaveJsonFilePath)) File.Copy(destination.SaveJsonFilePath, temp.SaveJsonFilePath, true);
            WorldSnapshotStore.CopyWorldCore(source, temp);

            if (Directory.Exists(destination.Root)) Directory.Delete(destination.Root, true);
            Directory.Move(temp.Root, destination.Root);
            return BugReportBundleWorldResolution.Resolved(destination);
        }
    }
}
