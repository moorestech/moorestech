using DeadMemberAudit.Model;
using Mono.Cecil;
using System.Text.RegularExpressions;

namespace DeadMemberAudit.Metadata;

// 型の宣言位置を出す。interfaceはメソッド本体が無くPDBに出ないので、ファイル名索引から引き当てる
// Resolves a type's declaration site; interfaces have no method bodies in the PDB, so a filename index is used instead
public sealed class TypeSourceLocator
{
    private readonly string _repositoryRoot;
    private readonly SourceLocator _sourceLocator;
    private readonly Dictionary<string, List<string>> _pathsByFileName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);

    public TypeSourceLocator(string repositoryRoot, SourceLocator sourceLocator)
    {
        _repositoryRoot = repositoryRoot;
        _sourceLocator = sourceLocator;

        foreach (var sourceRoot in AuditConstants.SourceRoots)
        {
            var rootPath = Path.Combine(repositoryRoot, sourceRoot.Path);
            if (!Directory.Exists(rootPath)) continue;
            foreach (var path in Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories))
            {
                var key = Path.GetFileNameWithoutExtension(path);
                if (!_pathsByFileName.TryGetValue(key, out var paths)) _pathsByFileName[key] = paths = new List<string>();
                paths.Add(path);
            }
        }
    }

    public string DisplayLocation(TypeDefinition type)
    {
        if (_cache.TryGetValue(type.FullName, out var cached)) return cached;
        var location = Locate(type);
        _cache[type.FullName] = location;
        return location;
    }

    private string Locate(TypeDefinition type)
    {
        // 本体を持つメソッドがあればPDBのファイルが最も確実。そこから宣言行を探す
        // When a method has a body the PDB file is the most reliable source, and the declaration line is found inside it
        var simpleName = StripGenericArity(type.Name);
        var fromSymbols = type.Methods.Select(_sourceLocator.DisplayLocation).FirstOrDefault(location => location.Length > 0) ?? string.Empty;
        if (fromSymbols.Length > 0)
        {
            var path = Path.Combine(_repositoryRoot, fromSymbols[..fromSymbols.LastIndexOf(':')]);
            var line = FindDeclarationLine(path, simpleName);
            if (line > 0) return $"{Path.GetRelativePath(_repositoryRoot, path)}:{line}";
            return fromSymbols;
        }

        if (!_pathsByFileName.TryGetValue(simpleName, out var candidates)) return string.Empty;
        foreach (var candidate in candidates)
        {
            var line = FindDeclarationLine(candidate, simpleName);
            if (line > 0) return $"{Path.GetRelativePath(_repositoryRoot, candidate)}:{line}";
        }

        return string.Empty;
    }

    private static int FindDeclarationLine(string path, string simpleName)
    {
        if (!File.Exists(path)) return 0;
        var pattern = new Regex($@"\b(class|interface|struct|record|enum)\s+{Regex.Escape(simpleName)}\b");

        // ソース読み込みは外部境界。読めない場合は位置なしとして扱う
        // Reading source is an external boundary; an unreadable file simply yields no location
        try
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;
                if (pattern.IsMatch(line)) return lineNumber;
            }
        }
        catch (IOException)
        {
            return 0;
        }

        return 0;
    }

    private static string StripGenericArity(string name)
    {
        var index = name.IndexOf('`');
        return index < 0 ? name : name[..index];
    }
}
