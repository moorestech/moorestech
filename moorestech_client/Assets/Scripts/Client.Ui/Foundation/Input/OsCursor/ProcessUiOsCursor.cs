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

        public ProcessUiOsCursor()
        {
            _processStack ??= CreateProcessStack();

            #region Internal

            UiOsCursorRequestStack CreateProcessStack()
            {
                // 最初のインスタンスで絵を読み、既定を当ててから記録（既定）と揃えたスタックを作る
                // On the first instance load the pictures, apply the default, then create a stack whose record (default) matches
                var applier = new UnityUiOsCursorApplier();
                applier.Apply(UiOsCursorShape.Default);
                return new UiOsCursorRequestStack(applier);
            }

            #endregion
        }

        public IDisposable Request(UiOsCursorShape shape, UiOsCursorLayer layer)
        {
            return _processStack.Request(shape, layer);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void DropProcessStackOnPlayStart()
        {
            // ドメインリロード無しの設定でも前の要求を持ち越さない。終了（Editor では PlayMode を出る時）にも捨てる
            // Carry no earlier requests even without a domain reload; also drop on quit (leaving PlayMode in the Editor)
            _processStack = null;
            Application.quitting -= DropProcessStack;
            Application.quitting += DropProcessStack;
        }

        private static void DropProcessStack()
        {
            _processStack = null;
        }
    }
}
