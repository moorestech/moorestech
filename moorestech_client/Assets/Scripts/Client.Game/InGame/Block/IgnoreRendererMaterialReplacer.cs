using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class IgnoreRendererMaterialReplacer : MonoBehaviour
    {
        // 空なら、このコンポーネント以下のレンダラーを丸ごと置き換えない。指定すると、同じ GameObject のレンダラーのうち指定した材質の枠だけ置き換えない
        // Empty keeps every renderer under this component; when set, only these material slots of the renderer on the same GameObject are kept
        [SerializeField] private int[] materialSlots = Array.Empty<int>();

        public IReadOnlyList<int> MaterialSlots => materialSlots;
    }
}
