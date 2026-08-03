using Mono.Cecil;

namespace DeadMemberAudit.Placement;

// 配列・参照・ジェネリック実引数を剥がして、1つの型参照に含まれる全ての型名を集める
// Strips arrays, byrefs and generic arguments to collect every type name contained in one type reference
public static class TypeReferenceWalker
{
    private const int MaxDepth = 6;

    // 全命令に対して呼ばれるため、呼び出し側のバッファを使い回して割り当てを抑える
    // Called for every instruction, so the caller's buffer is reused to keep allocations down
    public static void CollectInto(TypeReference? reference, List<string> buffer)
    {
        Walk(reference, 0);

        #region Internal

        void Walk(TypeReference? current, int depth)
        {
            if (current == null || depth > MaxDepth) return;

            // ジェネリック実引数は「その型を使っている」証拠になるので、開いた型と一緒に数える
            // Generic arguments prove the type is in use, so they are counted alongside the open type
            if (current is GenericInstanceType genericInstance)
            {
                Walk(genericInstance.ElementType, depth + 1);
                foreach (var argument in genericInstance.GenericArguments) Walk(argument, depth + 1);
                return;
            }

            if (current is TypeSpecification specification)
            {
                Walk(specification.ElementType, depth + 1);
                return;
            }

            if (current is GenericParameter) return;
            buffer.Add(current.FullName);
        }

        #endregion
    }
}
