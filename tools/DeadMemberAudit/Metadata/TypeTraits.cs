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
        return provider.CustomAttributes.Any(attribute => Matches(attribute.AttributeType.Name));

        #region Internal

        // Unity製の属性はクラス名がAttributeで終わらないものがある（MenuItem・ContextMenu・SerializeField等）
        // Some Unity attribute classes do not end in Attribute, such as MenuItem, ContextMenu and SerializeField
        bool Matches(string name)
        {
            return attributeNames.Contains(name) || attributeNames.Contains($"{name}Attribute");
        }

        #endregion
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

    // ラムダのクロージャやネスト型は外側の型の一部として扱う。C#では入れ子間でprivateが見えるため
    // Closures and nested types belong to their outer type, because C# lets nested scopes see each other's private members
    public static TypeDefinition OutermostType(TypeDefinition type)
    {
        var current = type;
        while (current.DeclaringType != null) current = current.DeclaringType;
        return current;
    }

    public static string OutermostFullName(TypeDefinition type)
    {
        return OutermostType(type).FullName;
    }

    // アセンブリ外から見える型か。internal型のpublicメンバーは既にinternal相当なので縮小提案の対象外
    // Whether the type is visible outside the assembly; a public member of an internal type is already internal in effect
    public static bool IsEffectivelyPublic(TypeDefinition type)
    {
        for (var current = type; current != null; current = current.DeclaringType)
        {
            if (current.DeclaringType == null) return current.IsPublic;
            if (!current.IsNestedPublic) return false;
        }

        return false;
    }

    // async/iteratorの本体は生成された状態機械へ移るので、走査対象をそちらへ差し替える
    // An async or iterator body moves into a generated state machine, so scanning must follow it there
    public static TypeDefinition? StateMachineType(MethodDefinition method)
    {
        if (!method.HasCustomAttributes) return null;
        var attribute = method.CustomAttributes.FirstOrDefault(candidate => AuditConstants.StateMachineAttributes.Contains(candidate.AttributeType.Name));
        if (attribute == null || attribute.ConstructorArguments.Count == 0) return null;
        return attribute.ConstructorArguments[0].Value is TypeReference stateMachine ? AssemblyLoader.TryResolve(stateMachine) : null;
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
