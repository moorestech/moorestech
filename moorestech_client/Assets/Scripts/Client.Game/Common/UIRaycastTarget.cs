using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.Common
{
    /// <summary>
    /// GraphicRaycastの判定だけを取る描画負荷のないuGUI要素
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIRaycastTarget : Graphic
    {
        public override void Rebuild(CanvasUpdate update)
        {
        }
    }
}