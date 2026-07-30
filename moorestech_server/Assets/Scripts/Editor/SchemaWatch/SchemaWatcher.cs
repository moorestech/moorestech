using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SchemaWatcher
{
    private const float CheckInterval = 1f;
    private static readonly SchemaWatchOrchestrator orchestrator;
    private static float timer;

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
                "ローカライズCSV",
                "localization csv"),
        };

        // 全対象の状態を復元する。
        // Restore every watch target state.
        var cacheFilePath = Path.Combine(Application.dataPath, "../Library/SchemaCache.txt");
        orchestrator = new SchemaWatchOrchestrator(
            watchTargets,
            new SchemaWatchCache(cacheFilePath),
            new UnitySchemaCompilationRequester());
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
        orchestrator.CheckForChanges();
    }
}
