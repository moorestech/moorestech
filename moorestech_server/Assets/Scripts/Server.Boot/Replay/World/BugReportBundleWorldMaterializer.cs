using System.IO;
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
            if (resolution.Outcome == BugReportBundleWorldOutcome.Rejected) return resolution;
            var source = ((BugReportBundleWorldResolution.ResolvedWorld)resolution).World;

            // 起動側は world.json の無い置き場を破損として拒む。full の箱でも world.json が欠けていれば実体化しても起動できない
            // The boot side refuses a world directory without world.json as corrupt; even a full box missing world.json cannot boot once materialized
            if (!File.Exists(source.WorldMetaFilePath)) return BugReportBundleWorldResolution.Rejected($"引き当てたワールドに world.json が無く、固定ワールド起動に使えません root:{source.Root}");

            // 置き場には prepare-run.sh が置いた save.json があるので、ディレクトリごとは消さずワールド定義だけを差し替える
            // The destination already holds the save.json prepare-run.sh placed, so only the world definition is replaced, never the whole directory
            var destination = WorldDataDirectory.FromWorldRoot(Path.Combine(bundleDirectory, BugReportBundleLayout.MaterializedWorldDirectoryName));
            Directory.CreateDirectory(destination.Root);
            if (Directory.Exists(destination.TerrainDirectory)) Directory.Delete(destination.TerrainDirectory, true);
            if (File.Exists(destination.WorldMetaFilePath)) File.Delete(destination.WorldMetaFilePath);

            // world.json はコミットマーカーなので最後に写す（WorldSnapshotStore と同じ順）。途中で落ちても world.json の無い置き場は起動側が破損として拒む
            // world.json is the commit marker and goes last (as in WorldSnapshotStore); an interrupted copy leaves no world.json, which the boot side refuses as corrupt
            File.Copy(source.MapJsonFilePath, destination.MapJsonFilePath, true);
            if (Directory.Exists(source.TerrainDirectory)) CopyDirectory(source.TerrainDirectory, destination.TerrainDirectory);
            File.Copy(source.WorldMetaFilePath, destination.WorldMetaFilePath, true);
            return BugReportBundleWorldResolution.Resolved(destination);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            foreach (var directory in Directory.GetDirectories(source))
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
