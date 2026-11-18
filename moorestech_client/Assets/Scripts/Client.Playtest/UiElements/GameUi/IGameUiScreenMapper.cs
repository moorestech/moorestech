using UnityEngine;

namespace Client.Playtest.UiElements.GameUi
{
    /// <summary>
    /// GameUi のパネル矩形（左上原点）を Unity スクリーン矩形（左下原点・px）へ写す
    /// Maps a GameUi panel rect (top-left origin) to a Unity screen rect (bottom-left origin, pixels)
    /// </summary>
    internal interface IGameUiScreenMapper
    {
        Rect PanelToScreen(Rect panelRect);
    }
}
