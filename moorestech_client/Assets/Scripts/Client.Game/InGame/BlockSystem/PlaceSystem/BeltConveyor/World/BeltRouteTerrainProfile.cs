using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     始点確定時に一度だけ測った箱の地形。列ごとの下限Yと、そこから決まるYの範囲を持つ
    ///     The box's terrain measured once at start selection: a floor Y per column and the Y range derived from it
    /// </summary>
    public sealed class BeltRouteTerrainProfile
    {
        private const int NoGround = int.MinValue;

        public readonly Vector3Int StartCell;
        public readonly int MinY;
        public readonly int MaxY;
        private readonly int[] _columnFloors;

        private BeltRouteTerrainProfile(Vector3Int startCell, int minY, int maxY, int[] columnFloors)
        {
            StartCell = startCell;
            MinY = minY;
            MaxY = maxY;
            _columnFloors = columnFloors;
        }

        public int HorizontalMinX => StartCell.x - BeltRouteGrid.HorizontalSize / 2;
        public int HorizontalMinZ => StartCell.z - BeltRouteGrid.HorizontalSize / 2;

        // 下限は地形最低点と始点の低い方、上限は地形最高点と始点の高い方＋余白
        // The lower bound is the lower of the terrain min and the start; the upper bound is the higher of the terrain max and the start plus headroom
        public static BeltRouteTerrainProfile Scan(Vector3Int startCell)
        {
            var size = BeltRouteGrid.HorizontalSize;
            var minX = startCell.x - size / 2;
            var minZ = startCell.z - size / 2;
            var floors = new int[size * size];
            var minFloor = startCell.y;
            var maxFloor = startCell.y;
            for (var z = 0; z < size; z++)
            {
                for (var x = 0; x < size; x++)
                {
                    var column = new Vector3Int(minX + x, 0, minZ + z);
                    // silent-ok: 地表が無い列は下限なし(ADR 0081と同じ扱い) / プレビューでそのセルが埋まらないことが見える
                    if (!GroundHeightProbe.TryGetFootprintMaxGroundHeight(column, BlockDirection.North, Vector3Int.one, out var groundMax))
                    {
                        floors[z * size + x] = NoGround;
                        continue;
                    }
                    var floor = PlacementGroundCellResolver.ResolveCellY(groundMax, 0);
                    floors[z * size + x] = floor;
                    if (floor < minFloor) minFloor = floor;
                    if (maxFloor < floor) maxFloor = floor;
                }
            }
            return new BeltRouteTerrainProfile(startCell, minFloor, maxFloor + BeltRouteGrid.HeadroomAboveMax, floors);
        }

        // 地形だけ書いた空の箱を作る。ブロック情報は呼び出し側が重ねる
        // Creates a box with only terrain written; the caller layers block data on top
        public BeltRouteGrid CreateGrid()
        {
            var grid = BeltRouteGrid.CreateAround(StartCell, MinY, MaxY);
            var size = BeltRouteGrid.HorizontalSize;
            for (var z = 0; z < size; z++)
            {
                for (var x = 0; x < size; x++)
                {
                    var floor = _columnFloors[z * size + x];
                    if (floor != NoGround) grid.SetColumnFloor(HorizontalMinX + x, HorizontalMinZ + z, floor);
                }
            }
            return grid;
        }
    }
}
