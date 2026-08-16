using Mono.Cecil;

namespace DeadMemberAudit.Metadata;

// 呼び出し元を「人が書いたメソッド」単位で指す。ラムダ・ローカル関数・状態機械は元のメソッドへ寄せる
// Identifies a caller at the granularity of a hand-written method, folding lambdas, local functions and state machines back into it
public sealed class CallerAttribution : IEquatable<CallerAttribution>
{
    public readonly string TypeFullName;
    public readonly string DisplayName;

    private CallerAttribution(string typeFullName, string displayName)
    {
        TypeFullName = typeFullName;
        DisplayName = displayName;
    }

    // 生成名は `<Owner>b__0` `<Owner>g__Local|0_1` `<Owner>d__3` の形なので、山括弧の中が元のメソッド名
    // Generated names look like `<Owner>b__0`, `<Owner>g__Local|0_1` or `<Owner>d__3`, so the owner sits inside the angle brackets
    public static CallerAttribution For(MethodDefinition method)
    {
        var outermost = TypeTraits.OutermostType(method.DeclaringType);
        return new CallerAttribution(outermost.FullName, $"{outermost.Name}.{OwnerMethodName(method)}");
    }

    private static string OwnerMethodName(MethodDefinition method)
    {
        // コンストラクタのIL名は `.ctor`。ラムダ経由でも先頭ドットが残るので、返す直前に必ず落とす
        // A constructor's IL name is `.ctor` and the leading dot survives through lambdas, so it is trimmed on the way out
        return Owner().TrimStart('.');

        #region Internal

        string Owner()
        {
            var fromMethodName = ExtractOwner(method.Name);
            if (fromMethodName.Length > 0) return fromMethodName;

            // メソッド名が素なら、クロージャ型・状態機械型の名前に元のメソッド名が残っている
            // When the method name is plain, the closure or state machine type name still carries the owner
            for (var type = method.DeclaringType; type != null; type = type.DeclaringType)
            {
                var fromTypeName = ExtractOwner(type.Name);
                if (fromTypeName.Length > 0) return fromTypeName;
            }

            return method.Name;
        }

        #endregion
    }

    // 入れ子の生成名 `<<Send>g__Local|1_0>d__5` があるので、山括弧は開いた分だけ読み飛ばす
    // Generated names nest, as in `<<Send>g__Local|1_0>d__5`, so every opening bracket is skipped
    private static string ExtractOwner(string name)
    {
        var open = 0;
        while (open < name.Length && name[open] == '<') open++;
        if (open == 0) return string.Empty;

        var close = name.IndexOf('>', open);
        return close <= open ? string.Empty : name.Substring(open, close - open);
    }

    public bool Equals(CallerAttribution? other)
    {
        if (other == null) return false;
        return string.Equals(TypeFullName, other.TypeFullName, StringComparison.Ordinal)
               && string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal);
    }

    public override bool Equals(object? other)
    {
        return Equals(other as CallerAttribution);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TypeFullName, DisplayName);
    }
}
