using MainGame.Control.Game;
using MainGame.Control.Game.MouseKeyboard;
using UnityEngine;

namespace Test.TestModule.UI
{
    public class BlockClickDetectTest : MonoBehaviour,IBlockClickDetect
    {
        public bool IsBlockClicked()
        {
            return Input.GetKeyDown(KeyCode.J);
        }

        public Vector2Int GetClickPosition()
        {
            return Vector2Int.zero;
        }
    }
}