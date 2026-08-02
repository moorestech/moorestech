using DeadMemberAudit.Model;
using System.Text.Json;

namespace DeadMemberAudit.Loading;

// asmdefの実体からmoorestechアセンブリ名を集め、名前パターンで役割を分類する
// Collects moorestech assembly names from the actual asmdefs and classifies their role by name pattern
public sealed class AssemblyClassifier
{
    private readonly HashSet<string> _moorestechAssemblyNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AssemblySide> _sideByAssemblyName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AssemblyCategory> _cache = new(StringComparer.Ordinal);

    public AssemblyClassifier(string repositoryRoot)
    {
        // asmdefのファイル名とアセンブリ名は一致しないことがあるのでnameフィールドを読む
        // The asmdef filename can differ from the assembly name, so read the name field
        foreach (var sourceRoot in AuditConstants.SourceRoots)
        {
            var rootPath = Path.Combine(repositoryRoot, sourceRoot.Path);
            if (!Directory.Exists(rootPath)) continue;
            foreach (var asmdefPath in Directory.EnumerateFiles(rootPath, "*.asmdef", SearchOption.AllDirectories))
            {
                var name = ReadAssemblyName(asmdefPath);
                if (name == null) continue;
                _moorestechAssemblyNames.Add(name);
                _sideByAssemblyName[name] = sourceRoot.Side;
            }
        }

        foreach (var defaultName in AuditConstants.DefaultAssemblyNames)
        {
            _moorestechAssemblyNames.Add(defaultName);
            _sideByAssemblyName[defaultName] = AssemblySide.Client;
        }

        #region Internal

        string? ReadAssemblyName(string path)
        {
            // asmdefは外部データなので破損時のパースエラーを境界で握り潰す
            // An asmdef is external data, so parse failures are contained at this boundary
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                return document.RootElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        #endregion
    }

    public int MoorestechAssemblyCount()
    {
        return _moorestechAssemblyNames.Count;
    }

    // 名前パターンではなくasmdefの実所在で決める。名前にServer/Clientが入らないアセンブリがあるため
    // Decided by the asmdef's actual location rather than the name, because not every assembly name carries Server/Client
    public AssemblySide SideOf(string assemblyName)
    {
        return _sideByAssemblyName.TryGetValue(assemblyName, out var side) ? side : AssemblySide.Unknown;
    }

    public IReadOnlyDictionary<string, AssemblySide> SideTable()
    {
        return _sideByAssemblyName;
    }

    public AssemblyCategory Classify(string assemblyName)
    {
        if (_cache.TryGetValue(assemblyName, out var cached)) return cached;
        var category = Evaluate(assemblyName);
        _cache[assemblyName] = category;
        return category;
    }

    // 分類順が意味を持つ。Assembly-CSharp系を先に確定し、次にテスト・デバッグ・エディタの順で判定する
    // Evaluation order matters: pin the Assembly-CSharp family first, then test, debug, editor
    private AssemblyCategory Evaluate(string assemblyName)
    {
        if (!_moorestechAssemblyNames.Contains(assemblyName)) return AssemblyCategory.External;
        if (AuditConstants.DefaultAssemblyNames.Contains(assemblyName)) return AssemblyCategory.Default;
        if (ContainsAny(AuditConstants.TestNameFragments)) return AssemblyCategory.Test;
        if (ContainsAny(AuditConstants.DebugNameFragments)) return AssemblyCategory.Debug;
        if (ContainsAny(AuditConstants.EditorNameFragments)) return AssemblyCategory.Editor;
        return AssemblyCategory.Production;

        #region Internal

        bool ContainsAny(string[] fragments)
        {
            return fragments.Any(fragment => assemblyName.Contains(fragment, StringComparison.Ordinal));
        }

        #endregion
    }
}
