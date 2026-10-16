using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SchemaWatcher
{
    private const float CheckInterval = 1f;
    private static readonly SchemaWatchOrchestrator orchestrator;
    private static double lastCheckTime;

    static SchemaWatcher()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../"));
        var watchTargets = new[]
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
                "CSV",
                "CSV"),
        };

        // 全対象の状態を復元する。
        // Restore every watch target state.
        var cacheFilePath = Path.Combine(Application.dataPath, "../Library/SchemaCache.txt");
        orchestrator = new SchemaWatchOrchestrator(
            watchTargets,
            new SchemaWatchCache(cacheFilePath),
            new UnitySchemaCompilationRequester());

        // CI中の再コンパイルでTest Runnerを中断しない。
        // Avoid interrupting the Test Runner with recompilation in CI.
        if (Application.isBatchMode) return;
        EditorApplication.update += Update;
    }

    private static void Update()
    {
        // EditModeでも監視を進め、起動中の遅延再コンパイルを防ぐ。
        // Advance monitoring in EditMode to avoid delayed recompilation during boot.
        if (EditorApplication.timeSinceStartup - lastCheckTime < CheckInterval)
        {
            return;
        }

        lastCheckTime = EditorApplication.timeSinceStartup;
        CheckForChanges();
    }

    [MenuItem("moorestech/Check Schema Changes")]
    public static void CheckForChanges()
    {
        orchestrator.CheckForChanges();
    }
}
