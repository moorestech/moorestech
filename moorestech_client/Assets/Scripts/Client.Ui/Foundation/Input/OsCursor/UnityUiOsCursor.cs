using System;

namespace Client.Ui.Foundation.Input.OsCursor
{
    /// <summary>
    /// 本番の OS カーソルの入口。OS カーソルはプロセスで 1 つなので、どのインスタンスもプロセスで 1 本の要求スタックへ積む（DI の単一インスタンスとギャラリーが互いの形を潰さない）
    /// Production entry to the OS cursor; the OS cursor is one per process, so every instance pushes onto the single process-wide request stack (the DI singleton and the gallery never clobber each other)
    /// </summary>
    public sealed class UnityUiOsCursor : IUiOsCursor
    {
        private static UiOsCursorRequestStack _processStack;

        public UnityUiOsCursor()
        {
            // 絵の読み込みは最初のインスタンスで 1 回だけ
            // Load the pictures only once, on the first instance
            _processStack ??= new UiOsCursorRequestStack(new UnityUiOsCursorApplier());
        }

        public IDisposable Request(UiOsCursorShape shape, UiOsCursorLayer layer)
        {
            return _processStack.Request(shape, layer);
        }
    }
}
