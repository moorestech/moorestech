using DeadMemberAudit.Analysis;
using DeadMemberAudit.Loading;
using DeadMemberAudit.Model;
using DeadMemberAudit.Reporting;

// 引数はScriptAssembliesのパスのみ。省略時はリポジトリルートからの既定パスを使う
// The only argument is the ScriptAssemblies path; omitting it falls back to the default path from the repository root
var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
var assembliesDirectory = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(repositoryRoot, AuditConstants.DefaultAssembliesRelativePath);

if (!Directory.Exists(assembliesDirectory))
{
    Console.Error.WriteLine($"ScriptAssembliesディレクトリが見つかりません: {assembliesDirectory}");
    return 1;
}

// 3rdパーティDLLはinterface実装の判定に必要。無い場合は名前照合だけで判定が続行される
// Third-party DLLs are needed to judge interface implementations; without them only name matching remains
var extraSearchDirectories = SearchDirectoryLocator.Collect(repositoryRoot);
var classifier = new AssemblyClassifier(repositoryRoot);
var result = new AuditRunner(repositoryRoot, assembliesDirectory).Run(extraSearchDirectories);

var report = new MarkdownReportWriter(classifier).Write(result, assembliesDirectory);
var reportPath = Path.Combine(repositoryRoot, "tools/DeadMemberAudit/report.md");
File.WriteAllText(reportPath, report);
Console.Out.Write(report);
Console.Error.WriteLine($"レポートを書き出しました: {reportPath}");
return 0;

// リポジトリルートは.gitの位置で決める。binの深い階層から実行されても正しく遡れる
// The repository root is located by the .git entry, so running from deep inside bin still resolves correctly
static string FindRepositoryRoot(string startDirectory)
{
    for (var directory = new DirectoryInfo(startDirectory); directory != null; directory = directory.Parent)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
        {
            return directory.FullName;
        }
    }

    return Directory.GetCurrentDirectory();
}
