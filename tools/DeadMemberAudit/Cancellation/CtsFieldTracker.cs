using DeadMemberAudit.Loading;
using DeadMemberAudit.Metadata;
using DeadMemberAudit.Model;
using Mono.Cecil;

namespace DeadMemberAudit.Cancellation;

// CancellationTokenSourceフィールドを集め、どのアセンブリからもCancel/Disposeされないものを挙げる
// Collects CancellationTokenSource fields and reports those never cancelled or disposed from any assembly
public sealed class CtsFieldTracker
{
    private readonly Dictionary<string, FieldDefinition> _fieldsByKey = new(StringComparer.Ordinal);
    private readonly HashSet<string> _releasedKeys = new(StringComparer.Ordinal);

    // 母集団はproductionの手書き型のフィールドのみ。状態機械へ巻き上げられたローカルはフィールドに化けるので外す
    // The population is fields of hand-written production types; locals hoisted into a state machine masquerade as fields and are dropped
    public void CollectFields(IReadOnlyList<LoadedAssembly> assemblies)
    {
        foreach (var assembly in assemblies.Where(assembly => assembly.Category.IsProduction()))
        {
            foreach (var type in TypeEnumerator.AllTypes(assembly.Assembly))
            {
                if (type.Name.Contains('<') || TypeTraits.IsGeneratedCode(type)) continue;
                foreach (var field in type.Fields)
                {
                    if (!TokenPropagationRules.IsTokenSource(field.FieldType) || field.Name.Contains('<')) continue;
                    _fieldsByKey[KeyOf(field)] = field;
                }
            }
        }
    }

    // 後始末はフィールドを読んだメソッド単位で見る。`_cts?.Cancel()` のような分岐込みの形を素直に拾うため
    // Release is judged per method that reads the field, so branchy shapes such as `_cts?.Cancel()` are picked up as-is
    public void CollectReleases(IReadOnlyList<LoadedAssembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var type in TypeEnumerator.AllTypes(assembly.Assembly))
            {
                foreach (var method in type.Methods) CollectFromMethod(method);
            }
        }
    }

    public List<FieldDefinition> UnreleasedFields()
    {
        return _fieldsByKey
            .Where(entry => !_releasedKeys.Contains(entry.Key))
            .Select(entry => entry.Value)
            .OrderBy(field => field.DeclaringType.FullName, StringComparer.Ordinal)
            .ThenBy(field => field.Name, StringComparer.Ordinal)
            .ToList();
    }

    private void CollectFromMethod(MethodDefinition method)
    {
        if (!method.HasBody) return;
        var loadedKeys = new List<string>();
        var releases = false;

        foreach (var instruction in method.Body.Instructions)
        {
            // 参照のままキーを作る。Resolveすると型フォワードの輪でCecilが落ちるため
            // The key is built from the reference, because resolving drives Cecil into type-forward cycles
            if (instruction.Operand is FieldReference fieldReference && TokenPropagationRules.IsTokenSource(fieldReference.FieldType))
            {
                loadedKeys.Add(KeyOf(fieldReference));
            }

            if (instruction.Operand is MethodReference methodReference && IsReleaseCall(methodReference)) releases = true;
        }

        if (releases) _releasedKeys.UnionWith(loadedKeys);
    }

    // `using`はIDisposable.Dispose経由で呼ばれるため、CTS直呼び以外も後始末として認める
    // A `using` goes through IDisposable.Dispose, so calls beyond the direct CTS method also count as release
    private static bool IsReleaseCall(MethodReference methodReference)
    {
        if (!AuditConstants.CancellationTokenSourceReleaseMethods.Contains(methodReference.Name)) return false;
        var declaringName = methodReference.DeclaringType.FullName;
        return declaringName == AuditConstants.CancellationTokenSourceFullName || declaringName == "System.IDisposable";
    }

    // 閉じたジェネリック型のフィールド参照は `Foo`1<Bar>` 形になるため、要素型に戻してから突き合わせる
    // A field reference on a closed generic reads as `Foo`1<Bar>`, so it is folded back to the element type before matching
    private static string KeyOf(FieldReference field)
    {
        return $"{field.DeclaringType.GetElementType().FullName}|{field.Name}";
    }
}
