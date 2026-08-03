namespace DeadMemberAudit.Loading;

// 参照解決用の検索ディレクトリを集める。3rdパーティのinterfaceを解決できないと実装除外が効かない
// Collects search directories for resolution; without third-party interfaces, implementation exclusion fails
public static class SearchDirectoryLocator
{
    // DLLが実際に置かれているディレクトリのみを追加する。Cecilは再帰検索しないため
    // Only directories that actually contain DLLs are added, because Cecil does not search recursively
    private static readonly string[] DllRoots =
    {
        "moorestech_client/Assets/Packages",
        "moorestech_client/Assets/Plugins",
        "moorestech_client/Assets/Dependencies",
        "moorestech_server/Assets/Packages",
        "moorestech_server/Assets/Plugins",
        "moorestech_client/Library/PackageCache",
    };

    public static List<string> Collect(string repositoryRoot)
    {
        var directories = new List<string>();
        foreach (var root in DllRoots)
        {
            var rootPath = Path.Combine(repositoryRoot, root);
            if (!Directory.Exists(rootPath)) continue;
            directories.AddRange(DirectoriesContainingAssemblies(rootPath));
        }

        directories.AddRange(UnityManagedDirectories(repositoryRoot));
        return directories.Distinct(StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<string> DirectoriesContainingAssemblies(string rootPath)
    {
        return Directory.EnumerateFiles(rootPath, "*.dll", SearchOption.AllDirectories)
            .Select(path => Path.GetDirectoryName(path)!)
            .Distinct(StringComparer.Ordinal);
    }

    // Unity本体のManagedはMonoBehaviour等の基底型解決に使う。バージョンはProjectVersion.txtから読む
    // Unity's Managed folder resolves base types such as MonoBehaviour; the version comes from ProjectVersion.txt
    private static IEnumerable<string> UnityManagedDirectories(string repositoryRoot)
    {
        var versionFile = Path.Combine(repositoryRoot, "moorestech_client/ProjectSettings/ProjectVersion.txt");
        if (!File.Exists(versionFile)) return Array.Empty<string>();

        var versionLine = File.ReadLines(versionFile).FirstOrDefault(line => line.StartsWith("m_EditorVersion:", StringComparison.Ordinal));
        if (versionLine == null) return Array.Empty<string>();

        var version = versionLine.Split(':', 2)[1].Trim();
        var contentsRoot = $"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents";

        // Unity 6はResources/Scripting/Managed、旧レイアウトはContents/Managedに置かれる
        // Unity 6 uses Resources/Scripting/Managed while the older layout uses Contents/Managed
        var candidates = new[] { $"{contentsRoot}/Resources/Scripting/Managed", $"{contentsRoot}/Managed" };
        return candidates
            .Where(Directory.Exists)
            .SelectMany(candidate => new[] { candidate }.Concat(DirectoriesContainingAssemblies(candidate)));
    }
}
