using DeadMemberAudit.Loading;
using DeadMemberAudit.Model;
using Mono.Cecil;

namespace DeadMemberAudit.Metadata;

// 型の性質判定。属性名は名前空間を無視した単純名で照合する
// Type trait checks; attribute names are matched by simple name, ignoring namespace
public static class TypeTraits
{
    public static bool HasAnyAttribute(ICustomAttributeProvider provider, IReadOnlyCollection<string> attributeNames)
    {
        if (!provider.HasCustomAttributes) return false;
        return provider.CustomAttributes.Any(attribute => attributeNames.Contains(attribute.AttributeType.Name));
    }

    // 基底型を名前で先に照合するので、UnityEngineのDLLが解決できなくても判定できる
    // Base types are matched by name first, so this works even when the UnityEngine DLL cannot be resolved
    public static bool DerivesFrom(TypeDefinition type, string baseFullName)
    {
        for (var current = type.BaseType; current != null;)
        {
            if (current.FullName == baseFullName) return true;
            var definition = AssemblyLoader.TryResolve(current);
            if (definition == null) return false;
            current = definition.BaseType;
        }

        return false;
    }

    public static bool IsUnityObjectType(TypeDefinition type)
    {
        return AuditConstants.UnityObjectBaseTypes.Any(baseName => DerivesFrom(type, baseName));
    }

    // 属性型のコンストラクタと名前付き引数は、ILではなくメタデータのblobから参照される
    // An attribute type's constructor and named arguments are referenced from a metadata blob, not from IL
    public static bool IsAttributeType(TypeDefinition type)
    {
        return DerivesFrom(type, AuditConstants.AttributeFullName);
    }

    // Unityがエンジン側から直接呼ぶメッセージ関数か。完全一致と接頭辞の両方で見る
    // Whether this is a message function the engine calls directly, matched by exact name and by prefix
    public static bool IsUnityMessageName(string methodName)
    {
        if (AuditConstants.UnityMessageMethods.Contains(methodName)) return true;
        return AuditConstants.UnityMessagePrefixes.Any(prefix => methodName.StartsWith(prefix, StringComparison.Ordinal));
    }

    // SourceGenerator生成型。ソースがリポジトリに無いので削除候補にならない
    // Source-generated type; its source is not in the repository, so it is never a deletion candidate
    public static bool IsGeneratedCode(TypeDefinition type)
    {
        if (HasAnyAttribute(type, new[] { "GeneratedCodeAttribute" })) return true;
        if (AuditConstants.GeneratedTypeNames.Contains(type.FullName)) return true;
        var fullName = type.FullName;
        return AuditConstants.GeneratedNamespacePrefixes.Any(prefix => fullName.StartsWith(prefix, StringComparison.Ordinal));
    }

    // シリアライザが反射的にメンバーを読み書きする型か
    // Whether a serializer reflectively reads and writes this type's members
    public static bool IsSerializedType(TypeDefinition type)
    {
        if (HasAnyAttribute(type, AuditConstants.SerializedTypeAttributes)) return true;
        if (type.Fields.Any(field => HasAnyAttribute(field, AuditConstants.SerializedMemberAttributes))) return true;
        return type.Properties.Any(property => HasAnyAttribute(property, AuditConstants.SerializedMemberAttributes));
    }
}
