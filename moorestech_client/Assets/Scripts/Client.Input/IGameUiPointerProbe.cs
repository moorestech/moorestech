using UnityEngine;

namespace Client.Input
{
    /// <summary>
    /// GameUi(UITK)側のポインタ占有判定。Client.Uiが実装しUiInputExclusivityへ登録する。
    /// Pointer-occupancy check for GameUi (UITK); Client.Ui implements it and registers it with UiInputExclusivity.
    /// </summary>
    public interface IGameUiPointerProbe
    {
        bool IsPointerOverGameUi(Vector2 screenPosition);
    }
}
