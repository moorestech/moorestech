using DeadMemberAudit.Loading;
using DeadMemberAudit.Metadata;
using DeadMemberAudit.Model;
using Mono.Cecil.Cil;
using Mono.Cecil;

namespace DeadMemberAudit.Placement;

// 追跡対象の型が、どのアセンブリからどう使われているかを全moorestechアセンブリから集める
// Collects, across every moorestech assembly, how the tracked types are used and from where
public sealed class TypeUsageCollector
{
    private readonly TypeUsageIndex _index;
    private readonly List<string> _buffer = new();

    public TypeUsageCollector(TypeUsageIndex index)
    {
        _index = index;
    }

    public void Collect(IReadOnlyList<LoadedAssembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var type in TypeEnumerator.AllTypes(assembly.Assembly)) CollectFromType(type, assembly.Name);
        }
    }

    private void CollectFromType(TypeDefinition type, string fromAssembly)
    {
        var enclosing = TypeTraits.OutermostFullName(type);

        // 継承・実装は供給側なので解決者と分けて数える。実装があっても解決者ゼロは配置ミスの兆候
        // Inheritance and implementation are the supply side, counted apart: an implementation with no resolver still signals misplacement
        Record(type.BaseType, enclosing, fromAssembly, _index.AddImplementation);
        foreach (var implementation in type.Interfaces) Record(implementation.InterfaceType, enclosing, fromAssembly, _index.AddImplementation);

        foreach (var field in type.Fields) Record(field.FieldType, enclosing, fromAssembly, _index.AddResolverUsage);
        foreach (var property in type.Properties) Record(property.PropertyType, enclosing, fromAssembly, _index.AddResolverUsage);

        foreach (var method in type.Methods)
        {
            Record(method.ReturnType, enclosing, fromAssembly, _index.AddResolverUsage);
            foreach (var parameter in method.Parameters) Record(parameter.ParameterType, enclosing, fromAssembly, _index.AddResolverUsage);
            CollectFromBody(method, enclosing, fromAssembly);
        }
    }

    private void CollectFromBody(MethodDefinition method, string enclosing, string fromAssembly)
    {
        if (!method.HasBody) return;
        foreach (var variable in method.Body.Variables) Record(variable.VariableType, enclosing, fromAssembly, _index.AddResolverUsage);

        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.Operand is TypeReference typeOperand) Record(typeOperand, enclosing, fromAssembly, _index.AddResolverUsage);
            // フィールドは型と持ち主の両方を数える。`X.EventTag` の参照はXを使っていることの証拠になる
            // A field counts for both its type and its owner, because reading `X.EventTag` is evidence that X is in use
            if (instruction.Operand is FieldReference fieldOperand)
            {
                Record(fieldOperand.FieldType, enclosing, fromAssembly, _index.AddResolverUsage);
                Record(fieldOperand.DeclaringType, enclosing, fromAssembly, _index.AddResolverUsage);
            }
            if (instruction.Operand is MethodReference methodOperand) RecordCall(methodOperand, enclosing, fromAssembly);
        }
    }

    // DI登録のジェネリック引数だけは解決者と数えない。PR1095の「登録のみ・解決者なし」を分離するため
    // Generic arguments of DI registration are not counted as resolvers, which isolates PR1095's registration-without-resolver shape
    private void RecordCall(MethodReference methodReference, string enclosing, string fromAssembly)
    {
        Record(methodReference.DeclaringType, enclosing, fromAssembly, _index.AddResolverUsage);
        if (methodReference is not GenericInstanceMethod genericMethod) return;

        if (!AuditConstants.DiRegistrationMethods.Contains(methodReference.Name))
        {
            foreach (var argument in genericMethod.GenericArguments) Record(argument, enclosing, fromAssembly, _index.AddResolverUsage);
            return;
        }

        // `AddSingleton<IFoo, Foo>()` の実装型は IFoo として解決されるので、両者を同じ登録として結ぶ
        // The implementation in `AddSingleton<IFoo, Foo>()` is resolved as IFoo, so both are linked as one registration
        var registered = new List<string>();
        foreach (var argument in genericMethod.GenericArguments)
        {
            Record(argument, enclosing, fromAssembly, _index.AddDiRegistration);
            _buffer.Clear();
            TypeReferenceWalker.CollectInto(argument, _buffer);
            registered.AddRange(_buffer.Where(_index.IsTracked));
        }

        if (registered.Count > 1) _index.LinkRegistrationPeers(registered);
    }

    // 自分自身の宣言に現れる型名は使用ではない。外側の型まで遡って自己参照を落とす
    // A type name appearing in its own declaration is not usage, so self references are dropped up to the outermost type
    private void Record(TypeReference? reference, string enclosing, string fromAssembly, Action<string, string> add)
    {
        _buffer.Clear();
        TypeReferenceWalker.CollectInto(reference, _buffer);
        foreach (var name in _buffer)
        {
            if (name == enclosing || !_index.IsTracked(name)) continue;
            add(name, fromAssembly);
        }
    }
}
