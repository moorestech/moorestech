using DeadMemberAudit.Metadata;
using DeadMemberAudit.Model;
using Mono.Cecil;

namespace DeadMemberAudit.Analysis;

// 参照が実在するpublicメンバーについて、どこまで公開範囲を狭められるかを判定する
// Decides how far a referenced public member's accessibility could be narrowed
public static class OverPublicRules
{
    public static OverPublicScope Evaluate(MemberCandidate candidate, ReferenceScope? scope)
    {
        // 参照が1件も無いものはリスト1/2の担当。縮小ではなく削除の話になる
        // Members with no references belong to lists 1 and 2, where the answer is deletion rather than narrowing
        if (scope == null) return OverPublicScope.None;
        if (!IsNarrowable(candidate)) return OverPublicScope.None;

        if (!scope.HasOutsideDeclaringType) return OverPublicScope.Private;
        if (!scope.HasOutsideDeclaringAssembly && TypeTraits.IsEffectivelyPublic(candidate.DeclaringType)) return OverPublicScope.Internal;
        return OverPublicScope.None;
    }

    // 縮小するとコンパイルが通らなくなる形をここで落とす。除外理由には数えず、単に候補にしない
    // Drops shapes that cannot compile once narrowed; they are simply not candidates rather than counted exclusions
    private static bool IsNarrowable(MemberCandidate candidate)
    {
        var declaringType = candidate.DeclaringType;

        // interfaceメンバーは常にpublic、virtual/abstractは派生側が上書きするため縮小できない
        // Interface members are always public, and virtual or abstract members must stay visible to overriders
        if (declaringType.IsInterface) return false;
        if (candidate.Methods.Any(method => method.IsVirtual || method.IsAbstract)) return false;

        // 静的コンストラクタとoperatorはアクセシビリティを選べない
        // Static constructors and operators have no accessibility choice
        return !candidate.Methods.Any(IsFixedAccessibility);
    }

    private static bool IsFixedAccessibility(MethodDefinition method)
    {
        if (method.IsConstructor && method.IsStatic) return true;
        return method.IsSpecialName && method.Name.StartsWith("op_", StringComparison.Ordinal);
    }
}
