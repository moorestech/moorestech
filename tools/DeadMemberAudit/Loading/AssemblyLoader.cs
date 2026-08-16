using DeadMemberAudit.Model;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DeadMemberAudit.Loading;

// 読み込み済みアセンブリと分類のペア
// A loaded assembly paired with its classification
public sealed class LoadedAssembly
{
    public readonly AssemblyDefinition Assembly;
    public readonly AssemblyCategory Category;
    public readonly string Name;

    public LoadedAssembly(AssemblyDefinition assembly, AssemblyCategory category)
    {
        Assembly = assembly;
        Category = category;
        Name = assembly.Name.Name;
    }
}

// 読み込み済みインスタンスを登録できるresolver。Resolveが同一定義を返すために必要
// A resolver that accepts already-loaded instances, needed so Resolve returns the same definitions
public sealed class RegisteringAssemblyResolver : DefaultAssemblyResolver
{
    // 循環フォワーダは複数アセンブリを跨ぐので、ここで読むアセンブリ全部が同一のガードを共有しなければ検知できない
    // A forwarder cycle spans assemblies, so every assembly read here must share one guard for detection to work
    public ForwarderCycleGuardResolver MetadataResolver { get; }

    // 基底のResolve(name)を経由せずガード付きで読むため、その分のキャッシュを自前で持つ
    // Reading with the guard bypasses the base Resolve(name), so its cache is reimplemented here
    private readonly Dictionary<string, AssemblyDefinition> _resolvedByFullName = new(StringComparer.Ordinal);

    public RegisteringAssemblyResolver()
    {
        MetadataResolver = new ForwarderCycleGuardResolver(this);
    }

    public void Register(AssemblyDefinition assembly)
    {
        _resolvedByFullName[assembly.Name.FullName] = assembly;
        RegisterAssembly(assembly);
    }

    public override AssemblyDefinition Resolve(AssemblyNameReference name)
    {
        if (_resolvedByFullName.TryGetValue(name.FullName, out var cached)) return cached;

        var assembly = base.Resolve(name, new ReaderParameters { AssemblyResolver = this, MetadataResolver = MetadataResolver });
        _resolvedByFullName[name.FullName] = assembly;
        return assembly;
    }
}

// ScriptAssemblies配下のDLLを読み、moorestechアセンブリだけを解析対象として返す
// Reads the DLLs under ScriptAssemblies and returns only the moorestech assemblies for analysis
public sealed class AssemblyLoader
{
    private readonly RegisteringAssemblyResolver _resolver = new();
    private readonly AssemblyClassifier _classifier;

    public int SkippedFileCount { get; private set; }
    public int SymbolLessAssemblyCount { get; private set; }
    public int BrokenForwarderCycleCount => _resolver.MetadataResolver.BrokenCycleCount;

    public AssemblyLoader(AssemblyClassifier classifier, string assembliesDirectory, IEnumerable<string> extraSearchDirectories)
    {
        _classifier = classifier;
        _resolver.AddSearchDirectory(assembliesDirectory);
        foreach (var directory in extraSearchDirectories)
        {
            if (Directory.Exists(directory)) _resolver.AddSearchDirectory(directory);
        }
    }

    public List<LoadedAssembly> LoadMoorestechAssemblies(string assembliesDirectory)
    {
        var loaded = new List<LoadedAssembly>();

        // PDBはソース位置と生成コード判定に使う。読めないアセンブリはシンボル無しで読み直す
        // PDBs drive source locations and the generated-code check; unreadable ones are re-read without symbols
        var parameters = new ReaderParameters { AssemblyResolver = _resolver, MetadataResolver = _resolver.MetadataResolver, ReadSymbols = true };

        // 3rdパーティDLLはmoorestechを参照しないので、読み込み自体を分類で足切りする
        // Third-party DLLs never reference moorestech, so classification gates the load itself
        foreach (var path in Directory.EnumerateFiles(assembliesDirectory, "*.dll").OrderBy(path => path, StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var category = _classifier.Classify(name);
            if (!category.IsMoorestech()) continue;

            var assembly = ReadAssembly(path, parameters);
            if (assembly == null) continue;

            // Resolveが同じ定義インスタンスを返すよう、読み込み済みアセンブリをresolverへ登録する
            // Register the loaded assembly with the resolver so Resolve returns the same definition instances
            _resolver.Register(assembly);
            loaded.Add(new LoadedAssembly(assembly, category));
        }

        return loaded;

        #region Internal

        AssemblyDefinition? ReadAssembly(string path, ReaderParameters readerParameters)
        {
            // DLLの読み込みは外部境界。破損DLLとPDB欠損はここで隔離する
            // Loading a DLL is an external boundary, so corrupt DLLs and missing PDBs are contained here
            try
            {
                return AssemblyDefinition.ReadAssembly(path, readerParameters);
            }
            catch (SymbolsNotFoundException)
            {
                return ReadWithoutSymbols(path);
            }
            catch (SymbolsNotMatchingException)
            {
                return ReadWithoutSymbols(path);
            }
            catch (BadImageFormatException)
            {
                SkippedFileCount++;
                return null;
            }
            catch (IOException)
            {
                SkippedFileCount++;
                return null;
            }
        }

        AssemblyDefinition? ReadWithoutSymbols(string path)
        {
            SymbolLessAssemblyCount++;

            // 同じ外部境界の続き。シンボル無しでも読めない場合だけ諦める
            // Still the same external boundary; give up only when it cannot be read even without symbols
            try
            {
                return AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = _resolver, MetadataResolver = _resolver.MetadataResolver, ReadSymbols = false });
            }
            catch (BadImageFormatException)
            {
                SkippedFileCount++;
                return null;
            }
        }

        #endregion
    }

    // 参照解決の失敗は日常的に起きるので、例外にせずnullで返す
    // Resolution failures are routine, so they return null instead of throwing
    public static MethodDefinition? TryResolve(MethodReference reference)
    {
        try
        {
            return reference.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
    }

    public static TypeDefinition? TryResolve(TypeReference reference)
    {
        try
        {
            return reference.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
    }

    public static FieldDefinition? TryResolve(FieldReference reference)
    {
        try
        {
            return reference.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
    }
}
