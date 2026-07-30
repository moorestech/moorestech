using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

public class SchemaWatcherPersistenceTest
{
    private string temporaryRoot;

    [SetUp]
    public void SetUp()
    {
        // テスト用領域を隔離する。
        // Isolate the persistence area under test.
        temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            $"moorestech-schema-watcher-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(temporaryRoot))
        {
            Directory.Delete(temporaryRoot, true);
        }
    }

    [Test]
    public void ファイル追加変更削除を永続cacheとの差分として検出する()
    {
        var watchPath = CreateDirectory("watch");
        var target = CreateTarget(watchPath, CreateDirectory("requester"), "CompileRequester");
        var cachePath = Path.Combine(temporaryRoot, "cache.txt");
        var cache = new SchemaWatchCache(cachePath);
        var sourcePath = Path.Combine(watchPath, "schema.yml");

        // 各種類の差分を順に検出する。
        // Detect each mutation independently.
        File.WriteAllText(sourcePath, "first");
        Assert.IsTrue(target.TryReadCurrentHashes(out var initialHashes));
        cache.ReplaceHashes(target, initialHashes);
        cache.Save();
        Assert.IsFalse(new SchemaWatchCache(cachePath).HasFolderChanged(target, initialHashes));

        File.WriteAllText(sourcePath, "changed");
        Assert.IsTrue(target.TryReadCurrentHashes(out var changedHashes));
        Assert.IsTrue(new SchemaWatchCache(cachePath).HasFolderChanged(target, changedHashes));
        cache.ReplaceHashes(target, changedHashes);
        cache.Save();

        File.WriteAllText(Path.Combine(watchPath, "added.yml"), "added");
        Assert.IsTrue(target.TryReadCurrentHashes(out var addedHashes));
        Assert.IsTrue(new SchemaWatchCache(cachePath).HasFolderChanged(target, addedHashes));
        cache.ReplaceHashes(target, addedHashes);
        cache.Save();

        File.Delete(sourcePath);
        Assert.IsTrue(target.TryReadCurrentHashes(out var deletedHashes));
        Assert.IsTrue(new SchemaWatchCache(cachePath).HasFolderChanged(target, deletedHashes));
    }

    [Test]
    public void 複数targetは変更対象のrequesterだけ更新しcompileを一度要求する()
    {
        var first = CreateTarget(
            CreateWatchDirectory("first", "first"),
            CreateDirectory("first-requester"),
            "FirstCompileRequester");
        var second = CreateTarget(
            CreateWatchDirectory("second", "second"),
            CreateDirectory("second-requester"),
            "SecondCompileRequester");
        var requester = new RecordingSchemaCompilationRequester();
        var orchestrator = new SchemaWatchOrchestrator(
            new[] { first, second },
            new SchemaWatchCache(Path.Combine(temporaryRoot, "cache.txt")),
            requester);

        // 二対象のcompile要求をまとめる。
        // Coalesce compilation for both targets.
        orchestrator.CheckForChanges();
        var firstRequester = Path.Combine(first.RequesterFolder, first.RequesterFile);
        var secondRequester = Path.Combine(second.RequesterFolder, second.RequesterFile);
        var initialFirstContent = File.ReadAllText(firstRequester);
        var initialSecondContent = File.ReadAllText(secondRequester);
        Assert.AreEqual(1, requester.RequestCount);

        File.WriteAllText(Path.Combine(first.WatchPath, "input.txt"), "first changed");
        orchestrator.CheckForChanges();

        Assert.AreEqual(2, requester.RequestCount);
        Assert.AreNotEqual(initialFirstContent, File.ReadAllText(firstRequester));
        Assert.AreEqual(initialSecondContent, File.ReadAllText(secondRequester));
    }

    [Test]
    public void requester更新後も日英説明とcommit意図を保持する()
    {
        var target = CreateTarget(
            CreateWatchDirectory("watch", "initial"),
            CreateDirectory("requester"),
            "CompileRequester");
        var orchestrator = new SchemaWatchOrchestrator(
            new[] { target },
            new SchemaWatchCache(Path.Combine(temporaryRoot, "cache.txt")),
            new RecordingSchemaCompilationRequester());

        orchestrator.CheckForChanges();

        var requesterContent = File.ReadAllText(
            Path.Combine(target.RequesterFolder, target.RequesterFile));
        var commentPrefix = new string('/', 2) + " ";
        StringAssert.Contains(commentPrefix + "SchemaWatcher更新用の再compile印", requesterContent);
        StringAssert.Contains(commentPrefix + "Recompile marker updated by SchemaWatcher", requesterContent);
        StringAssert.Contains(commentPrefix + "入力更新時はこの印もcommit", requesterContent);
        StringAssert.Contains(commentPrefix + "Commit this marker with input changes", requesterContent);
    }

    [Test]
    public void cache保存読込は空targetとescaped文字を欠落させない()
    {
        var cachePath = Path.Combine(temporaryRoot, "cache.txt");
        var emptyTarget = CreateTarget("empty|%\r\n", "unused", "EmptyRequester");
        var escapedTarget = CreateTarget("watch|%\r\n", "unused", "EscapedRequester");
        var escapedHashes = new Dictionary<string, string>
        {
            ["folder|%\r\nfile.yml"] = "AA-BB",
        };
        var cache = new SchemaWatchCache(cachePath);
        cache.ReplaceHashes(emptyTarget, new Dictionary<string, string>());
        cache.ReplaceHashes(escapedTarget, escapedHashes);
        cache.Save();

        var loaded = new SchemaWatchCache(cachePath);

        Assert.IsFalse(loaded.HasFolderChanged(emptyTarget, new Dictionary<string, string>()));
        Assert.IsFalse(loaded.HasFolderChanged(escapedTarget, escapedHashes));
        Assert.IsTrue(loaded.HasFolderChanged(
            escapedTarget,
            new Dictionary<string, string> { ["folder|%\r\nfile.yml"] = "CC-DD" }));
    }

    private SchemaWatchTarget CreateTarget(string watchPath, string requesterFolder, string className)
    {
        return new SchemaWatchTarget(
            watchPath,
            requesterFolder,
            "_CompileRequester.cs",
            className,
            className,
            "入力",
            "input");
    }

    private string CreateWatchDirectory(string name, string content)
    {
        var path = CreateDirectory(name);
        File.WriteAllText(Path.Combine(path, "input.txt"), content);
        return path;
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(temporaryRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingSchemaCompilationRequester : ISchemaCompilationRequester
    {
        public int RequestCount;

        public void RequestCompilation()
        {
            RequestCount++;
        }
    }
}
