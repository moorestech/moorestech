using DeadMemberAudit.Metadata;
using DeadMemberAudit.Model;
using Mono.Cecil;

namespace DeadMemberAudit.Analysis;

// IL上に呼び出し元が現れないメンバーを、理由付きで母集団から外す
// Removes members that cannot show an IL call site from the population, recording why
public sealed class ExclusionRules
{
    private readonly IlScanResult _scan;
    private readonly SourceLocator _sourceLocator;
    private readonly InterfaceImplementationIndex _interfaceIndex = new();
    private readonly Dictionary<string, bool> _serializedTypeCache = new(StringComparer.Ordinal);

    public ExclusionRules(IlScanResult scan, SourceLocator sourceLocator)
    {
        _scan = scan;
        _sourceLocator = sourceLocator;
    }

    public ExclusionReason Evaluate(MemberCandidate candidate)
    {
        var declaringType = candidate.DeclaringType;

        // アセンブリ単位・型単位の除外を先に済ませ、以降はメンバー個別の判定に絞る
        // Settle assembly-wide and type-wide exclusions first, then judge each member individually
        if (AuditConstants.ExternalApiAssemblies.Contains(candidate.AssemblyName)) return ExclusionReason.ExternalApiAssembly;
        if (TypeTraits.IsGeneratedCode(declaringType) || IsGeneratedSource()) return ExclusionReason.GeneratedCode;
        if (TypeTraits.IsAttributeType(declaringType)) return ExclusionReason.AttributeType;
        if (IsCompilerGenerated()) return ExclusionReason.CompilerGenerated;

        foreach (var method in candidate.Methods)
        {
            var methodReason = EvaluateMethod(method, declaringType);
            if (methodReason != ExclusionReason.None) return methodReason;
        }

        // シリアライズ対象型のプロパティと引数なしコンストラクタは、シリアライザだけが触る
        // Properties and parameterless constructors of a serialized type are touched only by the serializer
        if (IsSerializedMember()) return ExclusionReason.SerializedMember;
        return EvaluateConstructorRoots();

        #region Internal

        // 生成ファイル由来のメンバー。partialの生成側に居るoperator等がここで落ちる
        // Members originating in a generated file, such as operators emitted into the generated half of a partial
        bool IsGeneratedSource()
        {
            return candidate.Methods.Any(method => method.HasBody) && candidate.Methods.Where(method => method.HasBody).All(_sourceLocator.IsGeneratedSource);
        }

        bool IsCompilerGenerated()
        {
            if (candidate.Property != null)
            {
                if (TypeTraits.HasAnyAttribute(candidate.Property, new[] { "CompilerGeneratedAttribute" })) return true;
                return candidate.Property.Name.Contains('<');
            }

            return candidate.Methods.Any(method => method.IsCompilerControlled || method.Name.Contains('<')
                                                   || TypeTraits.HasAnyAttribute(method, new[] { "CompilerGeneratedAttribute" }));
        }

        bool IsSerializedMember()
        {
            if (candidate.Property != null && TypeTraits.HasAnyAttribute(candidate.Property, AuditConstants.SerializedMemberAttributes)) return true;
            var isSerializedType = IsSerializedTypeCached(declaringType);
            if (candidate.Property != null) return isSerializedType;
            if (!candidate.Methods.All(method => method.IsConstructor && method.Parameters.Count == 0)) return false;

            // [Serializable]はカスタム属性ではなく型フラグなので、IsSerializableで見る
            // [Serializable] is a type flag rather than a custom attribute, so it is read through IsSerializable
            return isSerializedType || declaringType.IsSerializable;
        }

        // DIコンテナ登録型とtypeof参照型のコンストラクタは、反射で呼ばれるためIL上に現れない
        // Constructors of container-registered and typeof-referenced types are called reflectively, so they never appear in IL
        ExclusionReason EvaluateConstructorRoots()
        {
            if (!candidate.Methods.All(method => method.IsConstructor)) return ExclusionReason.None;
            if (TypeTraits.IsUnityObjectType(declaringType)) return ExclusionReason.UnityObjectConstructor;
            if (candidate.Methods.All(IsImplicitDefaultConstructor)) return ExclusionReason.ImplicitDefaultConstructor;
            if (_scan.DiConstructedTypes.Contains(declaringType.FullName)) return ExclusionReason.DiConstructedType;
            if (_scan.ReflectivelyReferencedTypes.Contains(declaringType.FullName)) return ExclusionReason.ReflectivelyConstructedType;
            return ExclusionReason.None;
        }

        #endregion
    }

    // メソッド単位の除外。プロパティ候補では両アクセサに対して呼ばれる
    // Per-method exclusions, applied to both accessors for a property candidate
    private ExclusionReason EvaluateMethod(MethodDefinition method, TypeDefinition declaringType)
    {
        if (method.IsVirtual && !method.IsNewSlot) return ExclusionReason.Override;
        if (method.HasOverrides || (method.Name.Contains('.') && !method.IsConstructor)) return ExclusionReason.ExplicitInterfaceImplementation;
        if (!method.IsConstructor && _interfaceIndex.IsImplicitInterfaceImplementation(method)) return ExclusionReason.ImplicitInterfaceImplementation;
        if (TypeTraits.HasAnyAttribute(method, AuditConstants.FrameworkInvokedAttributes)) return ExclusionReason.FrameworkInvokedAttribute;

        // Unityメッセージ関数はエンジンが名前で呼ぶため、MonoBehaviour派生に限って除外する
        // Unity calls message functions by name, so exclude them only on MonoBehaviour-derived types
        if (!method.IsConstructor && TypeTraits.IsUnityMessageName(method.Name) && TypeTraits.IsUnityObjectType(declaringType))
        {
            return ExclusionReason.UnityMessageFunction;
        }

        return ExclusionReason.None;
    }

    // base呼び出しだけの引数なしコンストラクタ。人が書いたソースが存在しないので削除対象にならない
    // A parameterless constructor that only calls base; no hand-written source exists, so it cannot be deleted
    private static bool IsImplicitDefaultConstructor(MethodDefinition method)
    {
        if (method.IsStatic || method.Parameters.Count != 0 || !method.HasBody) return false;
        var opCodes = method.Body.Instructions
            .Where(instruction => instruction.OpCode != Mono.Cecil.Cil.OpCodes.Nop)
            .Select(instruction => instruction.OpCode.Code)
            .ToArray();

        return opCodes.Length == 3
               && opCodes[0] == Mono.Cecil.Cil.Code.Ldarg_0
               && opCodes[1] == Mono.Cecil.Cil.Code.Call
               && opCodes[2] == Mono.Cecil.Cil.Code.Ret;
    }

    private bool IsSerializedTypeCached(TypeDefinition type)
    {
        if (_serializedTypeCache.TryGetValue(type.FullName, out var cached)) return cached;
        var isSerialized = TypeTraits.IsSerializedType(type) || _scan.ReflectivelyReferencedTypes.Contains(type.FullName);
        _serializedTypeCache[type.FullName] = isSerialized;
        return isSerialized;
    }
}
