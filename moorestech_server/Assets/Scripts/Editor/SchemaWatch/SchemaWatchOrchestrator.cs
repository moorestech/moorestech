using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor.Compilation;
using UnityEngine;

public interface ISchemaCompilationRequester
{
    void RequestCompilation();
}

public sealed class UnitySchemaCompilationRequester : ISchemaCompilationRequester
{
    public void RequestCompilation()
    {
        CompilationPipeline.RequestScriptCompilation();
    }
}

public sealed class SchemaWatchOrchestrator
{
    private readonly SchemaWatchTarget[] targets;
    private readonly SchemaWatchCache cache;
    private readonly ISchemaCompilationRequester compilationRequester;

    public SchemaWatchOrchestrator(
        SchemaWatchTarget[] targets,
        SchemaWatchCache cache,
        ISchemaCompilationRequester compilationRequester)
    {
        this.targets = targets;
        this.cache = cache;
        this.compilationRequester = compilationRequester;
    }

    public void CheckForChanges()
    {
        var cacheChanged = false;

        // 更新成功時だけcacheを進める。
        // Advance cache only after successful updates.
        foreach (var target in targets)
        {
            if (!target.TryReadCurrentHashes(out var currentHashes)) continue;
            if (!cache.HasFolderChanged(target, currentHashes)) continue;
            if (!UpdateRequesterScript(target, currentHashes)) continue;

            Debug.Log($"{target.WatchPath} に変更がありました。{target.AssemblyName}アセンブリを再コンパイルします。");
            cache.ReplaceHashes(target, currentHashes);
            cacheChanged = true;
        }

        // 保存とcompile要求をまとめる。
        // Coalesce cache persistence and compilation.
        if (!cacheChanged) return;
        cache.Save();
        compilationRequester.RequestCompilation();

        bool UpdateRequesterScript(
            SchemaWatchTarget watchTarget,
            Dictionary<string, string> watchHashes)
        {
            if (!Directory.Exists(watchTarget.RequesterFolder))
            {
                Debug.LogError($"CompileRequesterフォルダが見つかりません: {watchTarget.RequesterFolder}");
                return false;
            }

            // 内容由来tokenで即時更新を検出する。
            // Detect rapid updates with a content-derived token.
            var requesterPath = Path.Combine(watchTarget.RequesterFolder, watchTarget.RequesterFile);
            var requesterToken = ComputeRequesterToken();
            var commentPrefix = new string('/', 2) + " ";
            var japaneseDescriptionComment =
                commentPrefix + watchTarget.WatchDescriptionJa + "更新時はこの印もcommit";
            var englishDescriptionComment =
                commentPrefix + "Commit this marker with " + watchTarget.WatchDescriptionEn + " changes";
            var requesterContent = $@"
// SchemaWatcher更新用の再compile印
// Recompile marker updated by SchemaWatcher
public class {watchTarget.ClassName}
{{
{japaneseDescriptionComment}
{englishDescriptionComment}
    private const string dummyText = ""{requesterToken}"";
}}";

            File.WriteAllText(requesterPath, requesterContent);
            return true;

            #region Internal

            string ComputeRequesterToken()
            {
                var tokenSource = new StringBuilder();
                foreach (var fileHash in watchHashes.OrderBy(entry => entry.Key))
                {
                    tokenSource.Append(fileHash.Key);
                    tokenSource.Append('\0');
                    tokenSource.Append(fileHash.Value);
                    tokenSource.Append('\0');
                }

                using var md5 = MD5.Create();
                var digest = md5.ComputeHash(Encoding.UTF8.GetBytes(tokenSource.ToString()));
                return System.BitConverter.ToString(digest);
            }

            #endregion
        }
    }
}
