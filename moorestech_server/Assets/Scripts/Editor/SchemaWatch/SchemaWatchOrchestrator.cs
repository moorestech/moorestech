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
    }

    private static bool UpdateRequesterScript(
        SchemaWatchTarget target,
        Dictionary<string, string> currentHashes)
    {
        if (!Directory.Exists(target.RequesterFolder))
        {
            Debug.LogError($"CompileRequesterフォルダが見つかりません: {target.RequesterFolder}");
            return false;
        }

        // 内容由来tokenで即時更新を検出する。
        // Detect rapid updates with a content-derived token.
        var requesterPath = Path.Combine(target.RequesterFolder, target.RequesterFile);
        var requesterToken = ComputeRequesterToken(currentHashes);
        var requesterContent = $@"
public class {target.ClassName}
{{
    private const string dummyText = ""{requesterToken}"";
}}";

        File.WriteAllText(requesterPath, requesterContent);
        return true;
    }

    private static string ComputeRequesterToken(Dictionary<string, string> currentHashes)
    {
        var tokenSource = new StringBuilder();
        foreach (var fileHash in currentHashes.OrderBy(entry => entry.Key))
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
}
