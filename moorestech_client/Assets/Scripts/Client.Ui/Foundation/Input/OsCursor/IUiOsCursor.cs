using System;

namespace Client.Ui.Foundation.Input.OsCursor
{
    /// <summary>
    /// OS カーソルの形を要求する唯一の口（USS はビットマップを参照できないため C# から当てる。D-12）。生きている要求のうち最後に取ったものの形が当たり、Dispose で外れる
    /// The only port that requests an OS cursor shape (USS may not reference bitmaps, so C# applies it; D-12); the most recent live request wins and Dispose withdraws it
    /// </summary>
    public interface IUiOsCursor
    {
        IDisposable Request(UiOsCursorShape shape);
    }
}
