using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     経路の端点が始点か終点か
    ///     Whether a route endpoint is the start or the end
    /// </summary>
    public enum BeltRouteEndpointRole
    {
        Start,
        End,
    }

    /// <summary>
    ///     ブロックの側面を狙った端点の向きを決める（ADR 0103）
    ///     Decides the facing of an endpoint aimed at a block's side face (ADR 0103)
    /// </summary>
    public static class BeltRouteEndpointFacing
    {
        // faceInward は狙った面からブロックへ向かう向き
        // faceInward heads from the aimed face into the block
        // 始点は、面が出力口ならブロックから離れる向き（その出力を受ける）、それ以外はキーの向き
        // The start heads away from the block on an output port (taking that output), and keeps the key's heading otherwise
        // 終点は、面が入力口ならブロックへ差し込む向き（その入口へ繋ぐ）、それ以外はキーの向き。口の無い壁では向きを変えない
        // The end heads into the block on an input port (feeding that port), and keeps the key's heading otherwise; a plain wall never turns it
        public static BlockDirection Resolve(IBeltRouteWorld world, Vector3Int cell, BlockDirection faceInward, BlockDirection keyDirection, BeltRouteEndpointRole role)
        {
            var faceBit = BeltRouteFaceBits.FaceBit(faceInward, 0);
            if (role == BeltRouteEndpointRole.End) return (world.InputFaceBits(cell) & faceBit) != 0 ? faceInward : keyDirection;
            return (world.OutputFaceBits(cell) & faceBit) != 0 ? BeltRouteDirection.Opposite(faceInward) : keyDirection;
        }
    }
}
