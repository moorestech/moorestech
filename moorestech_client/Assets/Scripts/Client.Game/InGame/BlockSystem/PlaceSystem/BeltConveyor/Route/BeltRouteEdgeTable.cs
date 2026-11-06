using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route
{
    /// <summary>
    ///     箱の中の既存の入出力辺。辺そのものの表と、探索の内側で1回引くためのセルごとの面ビットを持つ
    ///     Existing port edges inside the box: the edge table itself plus per-cell face bits the search reads with one lookup
    ///     面ビットは bit = 面(0..3) + 4×高さ差（0:セルの高さ、1:セルの1つ上）。下り坂の入口は1つ上の高さで照合する
    ///     A face bit is face (0..3) + 4 x height offset (0: the cell's level, 1: one above); a down slope's entry matches one level up
    /// </summary>
    public sealed class BeltRouteEdgeTable
    {
        private readonly Vector3Int _min;
        private readonly Vector3Int _size;
        private readonly bool[] _outputEdges;
        private readonly bool[] _inputEdges;
        private readonly byte[] _outputFaceBits;
        private readonly byte[] _inputFaceBits;

        public BeltRouteEdgeTable(Vector3Int min, Vector3Int size, int cellCount)
        {
            _min = min;
            _size = size;
            var edgeCount = (size.x + 1) * (size.y + 1) * (size.z + 1) * 2;
            _outputEdges = new bool[edgeCount];
            _inputEdges = new bool[edgeCount];
            _outputFaceBits = new byte[cellCount];
            _inputFaceBits = new byte[cellCount];
        }

        public static int FaceBit(BlockDirection face, int heightOffset)
        {
            return 1 << ((int)face + BeltRouteDirection.DirectionCount * heightOffset);
        }

        public void AddOutput(BeltRouteEdge edge) => Add(edge, _outputEdges, _outputFaceBits);
        public void AddInput(BeltRouteEdge edge) => Add(edge, _inputEdges, _inputFaceBits);

        public bool HasOutput(BeltRouteEdge edge)
        {
            var index = EdgeIndex(edge);
            return 0 <= index && _outputEdges[index];
        }

        public bool HasInput(BeltRouteEdge edge)
        {
            var index = EdgeIndex(edge);
            return 0 <= index && _inputEdges[index];
        }

        public int OutputFaceBits(int cellIndex) => _outputFaceBits[cellIndex];
        public int InputFaceBits(int cellIndex) => _inputFaceBits[cellIndex];

        // silent-ok: 箱の中のセルが問い合わせうる辺は必ず表に収まるので、収まらない辺は捨ててよい / 経路のプレビューで気づける
        private void Add(BeltRouteEdge edge, bool[] edges, byte[] faceBits)
        {
            var index = EdgeIndex(edge);
            if (index < 0) return;
            edges[index] = true;

            // 辺に接する2セルへ、辺の高さにある段と1つ下の段の両方で面ビットを立てる
            // Raise the face bit on both cells touching the edge, at the edge's level and one level below
            var positive = edge.PositiveCell;
            var forward = edge.NormalIsX ? new Vector3Int(1, 0, 0) : new Vector3Int(0, 0, 1);
            var positiveFace = edge.NormalIsX ? BlockDirection.East : BlockDirection.North;
            var negativeFace = BeltRouteDirection.Opposite(positiveFace);
            for (var heightOffset = 0; heightOffset <= 1; heightOffset++)
            {
                var level = positive.y - heightOffset;
                Mark(faceBits, new Vector3Int(positive.x - forward.x, level, positive.z - forward.z), positiveFace, heightOffset);
                Mark(faceBits, new Vector3Int(positive.x, level, positive.z), negativeFace, heightOffset);
            }
        }

        private void Mark(byte[] faceBits, Vector3Int cell, BlockDirection face, int heightOffset)
        {
            var local = cell - _min;
            if (local.x < 0 || _size.x <= local.x || local.y < 0 || _size.y <= local.y || local.z < 0 || _size.z <= local.z) return;
            // BeltRouteGrid.CellIndex と同じ並び
            // The same layout as BeltRouteGrid.CellIndex
            var cellIndex = (local.y * _size.z + local.z) * _size.x + local.x;
            faceBits[cellIndex] |= (byte)FaceBit(face, heightOffset);
        }

        // 正側セルは各軸で箱の最大+1まで来る（端のセルの北・東面、坂の出口高さ）。範囲外は-1
        // The positive cell reaches one past the box on each axis (north/east faces of edge cells, slope exit heights); -1 when outside
        private int EdgeIndex(BeltRouteEdge edge)
        {
            var x = edge.PositiveCell.x - _min.x;
            var y = edge.PositiveCell.y - _min.y;
            var z = edge.PositiveCell.z - _min.z;
            if (x < 0 || _size.x < x || y < 0 || _size.y < y || z < 0 || _size.z < z) return -1;
            return (((y * (_size.z + 1) + z) * (_size.x + 1) + x) << 1) | (edge.NormalIsX ? 1 : 0);
        }
    }
}
