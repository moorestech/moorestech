using DeadMemberAudit.Model;
using Mono.Cecil.Cil;
using Mono.Cecil;

namespace DeadMemberAudit.Cancellation;

// CancellationTokenの持ち主判定と、呼び出しサイトでトークンが落ちているかの判定
// Decides whether a caller holds a token and whether a call site drops it
public static class TokenPropagationRules
{
    public static bool IsToken(TypeReference type)
    {
        return type.FullName == AuditConstants.CancellationTokenFullName;
    }

    public static bool IsTokenSource(TypeReference type)
    {
        return type.FullName == AuditConstants.CancellationTokenSourceFullName;
    }

    // 参照のまま引数を読む。外部アセンブリをResolveするとCecilが型フォワードの輪で落ちるため
    // Parameters are read off the reference, because resolving external assemblies drives Cecil into type-forward cycles
    public static bool HasTokenParameter(MethodReference method)
    {
        return method.Parameters.Any(parameter => IsToken(parameter.ParameterType));
    }

    // CTSフィールドを読んでいる＝トークンを取り出せる立場にある
    // Reading a CTS field means the method is in a position to hand a token down
    public static bool AccessesTokenSourceField(MethodDefinition method)
    {
        if (!method.HasBody) return false;
        return method.Body.Instructions.Any(instruction =>
            instruction.Operand is FieldReference field && IsTokenSource(field.FieldType));
    }

    // 待てる呼び先だけを対象にする。同期APIのCT引数（MessagePackのDeserialize等）は寿命の話ではない
    // Only awaitable callees matter; a CT argument on a synchronous API such as MessagePack's Deserialize is not about lifetime
    public static bool IsAwaitable(TypeReference returnType)
    {
        var name = returnType.FullName;
        return AuditConstants.AwaitableReturnPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
    }

    // 同じ型にある「引数を1本増やしてCTを受け取る」同名メソッド。無ければトークンの渡し先が無い
    // The same-named sibling that takes one extra CancellationToken; without it there is nowhere to pass a token
    public static MethodDefinition? FindTokenOverload(MethodDefinition callee)
    {
        return callee.DeclaringType.Methods.FirstOrDefault(sibling =>
            sibling != callee
            && string.Equals(sibling.Name, callee.Name, StringComparison.Ordinal)
            && sibling.Parameters.Count == callee.Parameters.Count + 1
            && IsToken(sibling.Parameters[^1].ParameterType));
    }

    // 直前の命令列でトークンを作っているか。CTは最後の引数に置かれる慣習なので直前だけを見る
    // Whether the token is manufactured right before the call; CT is conventionally the last argument, so only the tail is inspected
    public static string EmptyTokenForm(Instruction call)
    {
        var previous = call.Previous;
        if (previous == null) return string.Empty;
        if (previous.OpCode == OpCodes.Call && previous.Operand is MethodReference getter
            && getter.Name == "get_None" && IsToken(getter.DeclaringType)) return "CancellationToken.None";

        // 既定値の省略も default(CancellationToken) と同じ命令列になる
        // Omitting an optional argument compiles to the same instruction sequence as default(CancellationToken)
        var initializer = previous.Previous;
        if (IsLoadLocal(previous) && initializer != null && initializer.OpCode == OpCodes.Initobj
            && initializer.Operand is TypeReference initialized && IsToken(initialized)) return "default(CancellationToken)";
        return string.Empty;
    }

    private static bool IsLoadLocal(Instruction instruction)
    {
        var code = instruction.OpCode.Code;
        return code is Code.Ldloc or Code.Ldloc_S or Code.Ldloc_0 or Code.Ldloc_1 or Code.Ldloc_2 or Code.Ldloc_3;
    }
}
