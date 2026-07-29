using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

[InitializeOnLoad]
public static class SchemaWatcher
{
    private const float CheckInterval = 1f;
    private static readonly SchemaWatchTarget[] watchTargets;
    private static readonly SchemaWatchCache cache;
    private static float timer;

    static SchemaWatcher()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../"));
        watchTargets = new[]
        {
            new SchemaWatchTarget(
                Path.Combine(repoRoot, "VanillaSchema"),
                Path.Combine(repoRoot, "moorestech_server/Assets/Scripts/Core.Master"),
                "_CompileRequester.cs",
                "CompileRequester",
                "Core.Master",
                "スキーマ",
                "schema"),
            new SchemaWatchTarget(
                Path.Combine(repoRoot, "Localization"),
                Path.Combine(repoRoot, "moorestech_client/Assets/Scripts/Client.Localization"),
                "_CompileRequester.cs",
                "LocalizationCompileRequester",
                "Client.Localization",
                "ローカライズCSV",
                "localization csv"),
        };

        // 全監視対象の状態を単一ファイルから復元し、更新監視を開始する。
        // Restore every watch target state from one file, then start update monitoring.
        var cacheFilePath = Path.Combine(Application.dataPath, "../Library/SchemaCache.txt");
        cache = new SchemaWatchCache(cacheFilePath);
        EditorApplication.update += Update;
    }

    private static void Update()
    {
        timer += Time.deltaTime;
        if (timer < CheckInterval)
        {
            return;
        }

        timer = 0f;
        CheckForChanges();
    }

    [MenuItem("moorestech/Check Schema Changes")]
    public static void CheckForChanges()
    {
        var cacheChanged = false;
        var requesterChanged = false;

        // 各対象を独立して検査し、欠損フォルダが他対象を妨げないようにする。
        // Inspect targets independently so a missing folder does not block the others.
        foreach (var target in watchTargets)
        {
            if (!target.TryReadCurrentHashes(out var currentHashes))
            {
                continue;
            }

            if (!cache.HasFolderChanged(target, currentHashes))
            {
                continue;
            }

            if (!UpdateDummyScript(target))
            {
                continue;
            }

            Debug.Log($"{target.WatchPath} に変更がありました。{target.AssemblyName}アセンブリを再コンパイルします。");
            cache.ReplaceHashes(target, currentHashes);
            cacheChanged = true;
            requesterChanged = true;
        }

        // 全対象の検査後に保存し、未変更・欠損対象の状態も維持する。
        // Save after all inspections to preserve unchanged and missing target states.
        if (cacheChanged)
        {
            cache.Save();
        }

        if (requesterChanged)
        {
            CompilationPipeline.RequestScriptCompilation();
        }
    }

    private static bool UpdateDummyScript(SchemaWatchTarget target)
    {
        if (!Directory.Exists(target.RequesterFolder))
        {
            Debug.LogError($"CompileRequesterフォルダが見つかりません: {target.RequesterFolder}");
            return false;
        }

        // 対象固有のクラス名と説明を用いて、該当アセンブリだけを更新する。
        // Use target-specific class names and descriptions to update only its assembly.
        var requesterPath = Path.Combine(target.RequesterFolder, target.RequesterFile);
        var currentDateTime = System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
        var requesterContent = $@"
// このコードは{target.AssemblyName}アセンブリを再コンパイルするためのスクリプトです。SchemaWatcherによって更新されます。
// This code is a script to recompile the {target.AssemblyName} assembly. It is updated by SchemaWatcher.
public class {target.ClassName}
{{
// {target.WatchDescriptionJa}を更新したら、こちらの更新もコミットしてください。
// If you update the {target.WatchDescriptionEn}, please also commit this update.
    private const string dummyText = ""{currentDateTime}"";
}}";

        File.WriteAllText(requesterPath, requesterContent);
        return true;
    }
}
