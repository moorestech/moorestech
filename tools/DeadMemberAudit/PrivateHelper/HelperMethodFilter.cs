using DeadMemberAudit.Metadata;
using DeadMemberAudit.Model;
using Mono.Cecil;

namespace DeadMemberAudit.PrivateHelper;

// 「IL上の呼び出し元だけで扱いを決められるprivateメソッド」の母集団を決める。判断できない形はここで全部落とす
// Decides the population of private methods whose fate can be settled from IL call sites alone, dropping every undecidable shape
public static class HelperMethodFilter
{
    private static readonly string[] CompilerGeneratedAttributeNames = { "CompilerGeneratedAttribute" };

    // クロージャ型・状態機械型のメソッドは人が書いたものではないので型ごと外す
    // Methods of closure and state machine types are not hand-written, so the whole type is dropped
    public static bool IsAuditableType(TypeDefinition type)
    {
        if (type.IsEnum || type.IsInterface) return false;
        if (type.Name.Contains('<')) return false;
        if (TypeTraits.IsGeneratedCode(type)) return false;
        return !TypeTraits.HasAnyAttribute(type, CompilerGeneratedAttributeNames);
    }

    public static bool IsAuditableMethod(MethodDefinition method, InterfaceImplementationIndex interfaceIndex, SourceLocator sourceLocator)
    {
        // 本体が無いもの・コンストラクタ・アクセサは、畳むことも単独で消すこともできない
        // Bodiless methods, constructors and accessors can neither be folded in nor deleted on their own
        if (!method.IsPrivate || method.IsConstructor || !method.HasBody) return false;
        if (method.IsGetter || method.IsSetter || method.IsAddOn || method.IsRemoveOn || method.IsFire) return false;

        // 明示的interface実装もprivateとして現れるため、override系と併せてここで落とす
        // An explicit interface implementation also surfaces as private, so it is dropped here alongside the override shapes
        if (method.IsVirtual || method.HasOverrides || method.Name.Contains('.')) return false;
        if (method.Name.Contains('<') || method.IsCompilerControlled) return false;
        if (TypeTraits.HasAnyAttribute(method, CompilerGeneratedAttributeNames)) return false;
        if (TypeTraits.HasAnyAttribute(method, AuditConstants.FrameworkInvokedAttributes)) return false;
        if (interfaceIndex.IsImplicitInterfaceImplementation(method)) return false;

        // Unityは非publicなメッセージ関数も名前で呼ぶので、IL上の呼び出し元だけでは判断できない
        // Unity calls even non-public message functions by name, so IL call sites alone cannot decide
        if (TypeTraits.IsUnityMessageName(method.Name) && TypeTraits.IsUnityObjectType(method.DeclaringType)) return false;
        return !sourceLocator.IsGeneratedSource(method);
    }

    public static string Signature(MethodDefinition method)
    {
        var parameters = method.Parameters.Select(parameter => parameter.ParameterType.Name);
        return $"{method.Name}({string.Join(", ", parameters)})";
    }
}
