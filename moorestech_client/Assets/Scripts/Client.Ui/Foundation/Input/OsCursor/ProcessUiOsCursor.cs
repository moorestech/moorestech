using System;
using UnityEngine;

namespace Client.Ui.Foundation.Input.OsCursor
{
    /// <summary>
    /// 本番の OS カーソルの入口。OS カーソルはプロセスで 1 つなので、どのインスタンスもプロセスで 1 本の要求スタック（static）へ積む。1 本であることは DI の Singleton ではなくこの static が保証し、DI の入口とギャラリーの入口が互いの形を潰さない。スタックは PlayMode に入るたび・出るたびに捨て、次の生成で既定を当て直して OS カーソルと記録を揃える（前の PlayMode の要求や当てた形を持ち越さない）
    /// Production entry to the OS cursor; the OS cursor is one per process, so every instance pushes onto the single process-wide request stack (static). That static, not the DI singleton, guarantees one stack, so the DI entry and the gallery entry never clobber each other. The stack is dropped on entering and leaving PlayMode, and the next creation re-applies the default so the OS cursor and the record agree (no requests or applied shape carry over from a previous PlayMode)
    /// </summary>
    public sealed class ProcessUiOsCursor : IUiOsCursor
    {
        private static UiOsCursorRequestStack _processStack;
        private static int _generation;
        private static IUiOsCursorApplier _applierOverride;

        public ProcessUiOsCursor()
        {
            // 生成時に既定を当てて OS カーソルと記録を揃える
            // Apply the default at creation so the OS cursor and the record agree
            _processStack ??= CreateProcessStack();
        }

        public IDisposable Request(UiOsCursorShape shape, UiOsCursorLayer layer)
        {
            // 捨てた後に生き残ったインスタンスの要求でも落とさないよう、無ければここで作る
            // Create the stack here when missing so a request from an instance surviving a drop does not throw
            _processStack ??= CreateProcessStack();
            return new GenerationGuardedRequest(_generation, _processStack.Request(shape, layer));
        }

        private static UiOsCursorRequestStack CreateProcessStack()
        {
            // 絵を読み、既定を当ててから記録（既定）と揃えたスタックを作る
            // Load the pictures, apply the default, then create a stack whose record (default) matches
            var applier = _applierOverride ?? new UnityUiOsCursorApplier();
            applier.Apply(UiOsCursorShape.Default);
            return new UiOsCursorRequestStack(applier);
        }

        // 次に作るスタックの当て先を差し替える（テスト用・内部のみ）
        // Replace the applier of the next stack (tests only, internal)
        internal static void UseApplierForNextProcessStack(IUiOsCursorApplier applier)
        {
            _applierOverride = applier;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void DropProcessStackOnPlayStart()
        {
            // ドメインリロード無しの設定でも前の要求を持ち越さない。終了（Editor では PlayMode を出る時）にも捨てる
            // Carry no earlier requests even without a domain reload; also drop on quit (leaving PlayMode in the Editor)
            DropProcessStack();
            Application.quitting -= DropProcessStack;
            Application.quitting += DropProcessStack;
        }

        internal static void DropProcessStack()
        {
            _processStack = null;
            _generation++;
        }

        private sealed class GenerationGuardedRequest : IDisposable
        {
            private readonly int _requestGeneration;
            private readonly IDisposable _inner;

            public GenerationGuardedRequest(int requestGeneration, IDisposable inner)
            {
                _requestGeneration = requestGeneration;
                _inner = inner;
            }

            public void Dispose()
            {
                // silent-ok: 捨てたスタックの要求は新しい記録を変えないよう無視する / 捨てた側の要求は新しいスタックの記録に関係せず、気づく相手はいない
                if (_requestGeneration != _generation) return;
                _inner.Dispose();
            }
        }
    }
}
