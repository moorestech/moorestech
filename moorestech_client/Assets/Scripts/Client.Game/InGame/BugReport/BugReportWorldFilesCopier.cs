using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.Capture;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;

namespace Client.Game.InGame.BugReport
{
    // 退避済みのスナップショットと記録時のワールド定義を箱へ入れる。再現側はこの2つが揃って初めて起動できる
    // Puts the staged snapshots and the recording's world definition into the box; reproduction boots only when both are present
    public static class BugReportWorldFilesCopier
    {
        private const string TerrainDirectoryName = "terrain";

        // スナップショットとワールド定義はディスク上で別の資料。まとめて握ると、どちらが落ちたか分からないまま片方の名前で欠損が立つ
        // The snapshots and the world definition are separate materials on disk; one shared catch would blame one name without knowing which stage failed
        public static void Copy(BugReportCapturedData data, string bundleDirectory, BugReportManifest manifest)
        {
            try { CopyStagedSnapshots(data, bundleDirectory, manifest); } catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e)) { manifest.AddMissing(BugReportBundleLayout.SnapshotDirectoryName, $"コピーに失敗した: {e.Message}"); }
            try { CopyWorldDefinition(data, bundleDirectory, manifest); } catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e)) { manifest.AddMissing(BugReportBundleLayout.WorldDirectoryName, $"コピーに失敗した: {e.Message}"); }
        }

        private static void CopyStagedSnapshots(BugReportCapturedData data, string bundleDirectory, BugReportManifest manifest)
        {
            if (string.IsNullOrEmpty(data.StagedSnapshotDirectory))
            {
                manifest.AddMissing(BugReportBundleLayout.SnapshotDirectoryName, "Escape時点のサーバー記録を退避できていなかった");
                return;
            }

            var snapshots = Path.Combine(bundleDirectory, BugReportBundleLayout.SnapshotDirectoryName);
            Directory.CreateDirectory(snapshots);

            // 箱に入った実体だけを manifest に載せる。名前だけ載せると受け側が存在しないスナップショットを土台にする
            // Only the files that really landed in the box go into the manifest; names alone make the receiver build on a snapshot that is not there
            manifest.SnapshotFiles = CopyStagedFiles(data.StagedSnapshotDirectory, snapshots, data.SnapshotFileNames, manifest);
            manifest.PacketLogFiles = CopyStagedFiles(data.StagedSnapshotDirectory, snapshots, data.PacketLogFileNames, manifest);

            var ticks = new List<ulong>();
            foreach (var name in manifest.SnapshotFiles)
            {
                // ファイル名規則の定義元は WorldDataDirectory 1箇所。読み側も同じ定義に委譲している
                // WorldDataDirectory is the single definition of the naming rule, and the reading side delegates to the same one
                if (WorldDataDirectory.TryParseSnapshotTick(name, out var tick)) ticks.Add(tick);
                else manifest.AddMissing(name, "スナップショットのtickを読み取れないファイル名だった");
            }
            ticks.Sort();
            manifest.SnapshotTicks = ticks;
        }

        private static List<string> CopyStagedFiles(string stagedDirectory, string destinationDirectory, IReadOnlyList<string> fileNames, BugReportManifest manifest)
        {
            var copied = new List<string>();
            foreach (var name in fileNames)
            {
                var source = Path.Combine(stagedDirectory, name);
                if (!File.Exists(source))
                {
                    manifest.AddMissing(name, "確保時の退避先に無かった");
                    continue;
                }
                File.Copy(source, Path.Combine(destinationDirectory, name), true);
                copied.Add(name);
            }
            return copied;
        }

        // 生成ワールドは world.json だけを入れる。地形は seed・指紋・生成器版から同じものが引き当てられる（ADR 0064）。手作りワールドは従来どおり全部入れる
        // A generated world ships only world.json; its terrain is restored from seed, fingerprint and generator version (ADR 0064). A hand-made world ships everything, as before
        private static void CopyWorldDefinition(BugReportCapturedData data, string bundleDirectory, BugReportManifest manifest)
        {
            if (string.IsNullOrEmpty(data.WorldRootDirectory))
            {
                manifest.AddMissing(BugReportBundleLayout.WorldDirectoryName, "記録時のワールドディレクトリが分からなかった");
                return;
            }

            var source = WorldDataDirectory.FromWorldRoot(data.WorldRootDirectory);
            var world = Path.Combine(bundleDirectory, BugReportBundleLayout.WorldDirectoryName);
            var destination = WorldDataDirectory.FromWorldRoot(world);
            Directory.CreateDirectory(world);

            // 宣言は箱に実際に入った物から決める。world.json が入らなければ受け側は生成か手作りかも決められないので not-captured のまま出す
            // The declaration follows what really landed in the box; without world.json the receiver cannot even tell generated from hand-made, so it stays not-captured
            // ディスクIO境界（他プロセスのロック・権限不足）でのworld.jsonのコピー失敗はここで個別に閉じ、world/ 全体の欠損と区別して残す
            // A disk-IO-boundary failure (a foreign lock or missing permission) copying world.json is contained here and recorded apart from the whole world/
            bool worldMetaCopied;
            try { worldMetaCopied = CopyIfExists(source.WorldMetaFilePath, destination.WorldMetaFilePath, manifest); }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e)) { manifest.AddMissing("world.json", $"コピーに失敗した: {e.Message}"); worldMetaCopied = false; }
            if (!worldMetaCopied) return;

            if (IsGeneratedWorld(source.WorldMetaFilePath, manifest, out var worldMetaUnreadable))
            {
                manifest.WorldDefinition = BugReportWorldDefinitionText.ToContractText(BugReportWorldDefinition.GeneratedWorldJsonOnly);
                return;
            }

            // 手作りワールドは map.json が入って初めて起動できる。入らなければ not-captured のまま出し、full を名乗るのは地形まで写し終えた後
            // A hand-made world boots only with map.json; without it the box stays not-captured, and full is claimed only after the terrain is copied too
            if (!CopyIfExists(source.MapJsonFilePath, destination.MapJsonFilePath, manifest)) return;
            // world.jsonが読めた手作りワールドは地形が無くても欠損にしない（旧挙動）。読めなかったときだけ地形の欠落も箱に残す
            // A hand-made world with a readable world.json is not flagged missing without terrain (legacy behavior); only an unreadable world.json also records the terrain gap
            CopyTerrain(worldMetaUnreadable);
            manifest.WorldDefinition = BugReportWorldDefinitionText.ToContractText(BugReportWorldDefinition.Full);

            #region Internal

            // 生成ワールドの起動は terrain の実ファイルをバイト数まで数えるので、1枚でも欠けると受け側が例外で落ちる。手作りワールドは地形任意が旧来の前提
            // Booting a generated world counts the terrain files down to their bytes, so one missing file kills the receiving side; a hand-made world's terrain has always been optional
            void CopyTerrain(bool requiresTerrain)
            {
                if (!Directory.Exists(source.TerrainDirectory))
                {
                    if (requiresTerrain) manifest.AddMissing(TerrainDirectoryName, "world.jsonを読めず地形の要否を判定できなかった");
                    return;
                }

                Directory.CreateDirectory(destination.TerrainDirectory);
                var copied = 0;
                foreach (var path in Directory.GetFiles(source.TerrainDirectory))
                {
                    File.Copy(path, Path.Combine(destination.TerrainDirectory, Path.GetFileName(path)), true);
                    copied++;
                }
                if (copied == 0 && requiresTerrain) manifest.AddMissing(TerrainDirectoryName, "world.jsonを読めず地形の要否を判定できなかった");
            }

            #endregion
        }

        // world.json は外部入力のJSON。読めないときは全部入れる側に倒し、地形を省く判断が読めた内容だけに基づくようにする。読めたか否かは呼び出し側の地形必須判定にも使う
        // world.json is external JSON input; when it cannot be read, fall back to shipping everything so the omission decision rests only on what was actually read. The caller also uses readability to decide whether terrain is required
        private static bool IsGeneratedWorld(string worldMetaFilePath, BugReportManifest manifest, out bool isUnreadable)
        {
            isUnreadable = false;
            if (!File.Exists(worldMetaFilePath)) return false;
            try
            {
                var meta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(worldMetaFilePath));
                if (meta == null)
                {
                    manifest.AddMissing("world.json", "mapModeを読み取れなかった: 中身が空だった");
                    isUnreadable = true;
                    return false;
                }
                return WorldMapMode.IsGenerated(meta.MapMode);
            }
            catch (JsonException e)
            {
                manifest.AddMissing("world.json", $"mapModeを読み取れなかった: {e.Message}");
                isUnreadable = true;
                return false;
            }
            catch (IOException e)
            {
                manifest.AddMissing("world.json", $"mapModeを読み取れなかった: {e.Message}");
                isUnreadable = true;
                return false;
            }
            catch (UnauthorizedAccessException e)
            {
                manifest.AddMissing("world.json", $"mapModeを読み取れなかった: {e.Message}");
                isUnreadable = true;
                return false;
            }
        }

        // 箱に入ったかを返す。入らなかった理由は欠損に残す
        // Returns whether the file landed in the box; the reason it did not is recorded as missing
        private static bool CopyIfExists(string sourcePath, string destinationPath, BugReportManifest manifest)
        {
            if (!File.Exists(sourcePath))
            {
                manifest.AddMissing(Path.GetFileName(sourcePath), "ワールドディレクトリに無かった");
                return false;
            }
            File.Copy(sourcePath, destinationPath, true);
            return true;
        }
    }
}
