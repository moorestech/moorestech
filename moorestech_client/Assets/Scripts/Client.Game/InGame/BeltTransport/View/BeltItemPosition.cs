using System;
using Core.BeltTransport;
using UnityEngine;
namespace Client.Game.InGame.BeltTransport
{
    public static class BeltItemPosition
    {
        public static Vector3 Calculate(BeltNetworkCell cell, BeltCellItemState item)
        {
            // 標準ベルト面の中心から、搬入元中心までの残距離を戻す。
            // Offset from the standard belt surface center toward the incoming cell center.
            var center = new Vector3(cell.X + 0.5f, cell.Y + 0.35f, cell.Z + 0.5f);
            var entry = item.EntryDirection switch
            {
                BeltDirection.Front => Vector3.forward,
                BeltDirection.Back => Vector3.back,
                BeltDirection.Left => Vector3.left,
                BeltDirection.Right => Vector3.right,
                _ => throw new ArgumentException($"Invalid belt entry direction: {item.EntryDirection}.")
            };
            entry.y = item.EntryHeight;
            return item.IsBuffer ? center : center + entry * (1f - item.Progress / 256f);
        }
    }
}
