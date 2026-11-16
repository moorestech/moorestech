using Client.Input.Pointer;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Input.Pointer;

namespace Client.Ui.Editor.Gallery
{
    /// <summary>
    /// ギャラリーの部品が使う OS カーソルの入口。ポインタの位置の購読は InputSystem へ張るので、窓を開くたびに作らず 1 本を使い回す
    /// OS cursor port for gallery parts; the pointer position stream subscribes to InputSystem, so one is shared instead of creating one per window open
    /// </summary>
    public static class GalleryOsCursor
    {
        private static InputSystemUiScreenPointer _pointer;

        public static IUiOsCursor Create()
        {
            _pointer ??= new InputSystemUiScreenPointer(new InputSystemPointerPositionStream());
            return new ProcessUiOsCursor(new UnityUiOsCursorApplier(), _pointer);
        }
    }
}
