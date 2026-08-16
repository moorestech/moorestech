using DeadMemberAudit.Loading;
using DeadMemberAudit.Metadata;
using DeadMemberAudit.Model;
using Mono.Cecil.Cil;
using Mono.Cecil;

namespace DeadMemberAudit.Analysis;

// 全moorestechアセンブリのILを1回走査し、参照表と反射的生成型を同時に集める
// Scans the IL of every moorestech assembly once, collecting the reference table and reflective types together
public sealed class ReferenceCollector
{
    private static readonly HashSet<OpCode> ReferenceOpCodes = new()
    {
        OpCodes.Call, OpCodes.Callvirt, OpCodes.Newobj, OpCodes.Ldftn, OpCodes.Ldvirtftn, OpCodes.Ldtoken,
    };

    private readonly HashSet<string> _productionAssemblyNames;

    public ReferenceCollector(IEnumerable<LoadedAssembly> assemblies)
    {
        _productionAssemblyNames = assemblies
            .Where(assembly => assembly.Category.IsProduction())
            .Select(assembly => assembly.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    public IlScanResult Scan(IReadOnlyList<LoadedAssembly> assemblies)
    {
        var result = new IlScanResult();
        foreach (var assembly in assemblies)
        {
            foreach (var type in TypeEnumerator.AllTypes(assembly.Assembly))
            {
                foreach (var method in type.Methods) ScanMethod(method, assembly.Name, result);
            }
        }

        return result;
    }

    // 自己参照は「生きている」根拠にならないので、呼び出し元メソッド自身への参照は捨てる
    // A self reference is no evidence of life, so references back to the enclosing method are dropped
    private void ScanMethod(MethodDefinition method, string fromAssembly, IlScanResult result)
    {
        if (!method.HasBody) return;
        result.CountScannedMethod();
        var enclosingKey = MemberKey.For(method);
        var enclosingOutermost = TypeTraits.OutermostFullName(method.DeclaringType);
        var enclosingCaller = CallerAttribution.For(method);

        foreach (var instruction in method.Body.Instructions)
        {
            if (!ReferenceOpCodes.Contains(instruction.OpCode)) continue;
            if (instruction.Operand is MethodReference methodReference) HandleMethodOperand(methodReference, instruction.OpCode);
            if (instruction.OpCode == OpCodes.Ldtoken && instruction.Operand is TypeReference typeOperand) RecordReflectiveType(typeOperand);
        }

        #region Internal

        void HandleMethodOperand(MethodReference methodReference, OpCode opCode)
        {
            RecordGenericArguments(methodReference);

            // production以外に宣言されたメソッドは母集団外なので、Resolve自体を省く
            // Methods declared outside production are not in the population, so skip resolving entirely
            if (!_productionAssemblyNames.Contains(MemberKey.ScopeNameOf(methodReference.DeclaringType))) return;
            var definition = AssemblyLoader.TryResolve(methodReference);
            if (definition == null) return;

            var targetKey = MemberKey.For(definition);
            if (targetKey == enclosingKey) return;

            // デリゲート化はメソッドを値として渡す形なので、単一呼び出し元の判定から外すために印を付ける
            // Turning a method into a delegate passes it as a value, so it is marked out of the single-caller judgement
            if (opCode == OpCodes.Ldftn || opCode == OpCodes.Ldvirtftn) result.DelegateBoundMethods.Add(targetKey);

            // 縮小提案のため、参照が宣言型・宣言アセンブリの外へ出たかを同時に記録する
            // Record whether the reference escaped the declaring type or assembly, which drives the narrowing proposal
            var sameOutermostType = TypeTraits.OutermostFullName(definition.DeclaringType) == enclosingOutermost;
            var sameAssembly = string.Equals(definition.Module.Assembly.Name.Name, fromAssembly, StringComparison.Ordinal);
            result.AddReference(targetKey, fromAssembly, sameOutermostType, sameAssembly, enclosingCaller);
        }

        // DI登録とシリアライズAPIのジェネリック引数は、コンテナ/シリアライザが反射的に触る型
        // Generic arguments of registration and serialization APIs are types touched reflectively by the container or serializer
        void RecordGenericArguments(MethodReference methodReference)
        {
            if (methodReference is not GenericInstanceMethod genericMethod) return;
            var isDiRegistration = AuditConstants.DiRegistrationMethods.Contains(methodReference.Name);
            var isSerialization = AuditConstants.ReflectiveSerializationMethods.Contains(methodReference.Name);
            if (!isDiRegistration && !isSerialization) return;

            foreach (var argument in genericMethod.GenericArguments)
            {
                var argumentDefinition = AssemblyLoader.TryResolve(argument);
                if (argumentDefinition == null) continue;
                if (isDiRegistration) result.DiConstructedTypes.Add(argumentDefinition.FullName);
                else result.ReflectivelyReferencedTypes.Add(argumentDefinition.FullName);
            }
        }

        void RecordReflectiveType(TypeReference typeReference)
        {
            var definition = AssemblyLoader.TryResolve(typeReference);
            if (definition != null) result.ReflectivelyReferencedTypes.Add(definition.FullName);
        }

        #endregion
    }
}
