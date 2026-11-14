using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Block.Index
{
    /// <summary>
    /// 占有セルを持つ八分木。根は基準点からの相対座標で決め、外にセルが入ればその向きへ広げる
    /// Octree of occupied cells; the root is placed relative to an origin and grows toward cells added outside it
    /// </summary>
    internal sealed class OccupiedCellOctree
    {
        // 段 L のノードは基準点からの相対座標で 2^L 立方体。段2が葉で 4×4×4 を ulong 1つのビットで持ち、段3以上は子8つの番号を持つ
        // A level-L node is a 2^L cube in origin-relative coordinates; level 2 leaves hold 4x4x4 as one ulong, levels 3+ hold eight child indices
        public const int LeafLevel = 2;
        private const int FirstRootLevel = LeafLevel + 1;
        private const int NoChild = -1;
        // 根の大きさ 1 << 段数 が int に収まる上限。これを超えて広げるとノードの位置が桁あふれする
        // Highest root level whose size 1 << level fits in an int; growing past it would overflow node positions
        private const int MaxRootLevel = 30;
        private const int PathCapacity = MaxRootLevel + 2;

        private int[] _children = new int[8 * 64];
        private int _nodeCount;
        private readonly Stack<int> _freeNodes = new();
        private ulong[] _leafBits = new ulong[64];
        private int _leafCount;
        private readonly Stack<int> _freeLeaves = new();
        private int _rootNode = NoChild;
        private int _rootLevel;
        private Vector3Int _origin;

        // 問い合わせで直前にたどった道（段ごとのノードと相対キー）と、撤去で使う道
        // Path walked by the previous probe (node and relative key per level), and the path used by removal
        private readonly int[] _pathNode = new int[PathCapacity];
        private readonly Vector3Int[] _pathKey = new Vector3Int[PathCapacity];
        private int _pathLowest = PathCapacity;
        private readonly int[] _removePath = new int[PathCapacity];

        internal int AllocatedNodeCount => _nodeCount - _freeNodes.Count;

        public void Add(Vector3Int cell)
        {
            // 空なら、そのセルを基準点にした段3を根にする
            // When empty, root a level-3 node at this cell
            if (_rootNode == NoChild)
            {
                _rootLevel = FirstRootLevel;
                _origin = cell;
                _rootNode = NewNode();
            }
            while (!IsInsideRoot(cell - _origin)) GrowRootToward(cell - _origin);

            // 根から下り、無いノードと葉を作ってビットを立てる
            // Descend from the root, create missing nodes and the leaf, then set the bit
            // NewNode は配列を広げることがあるので、番号を受けてから書き込む（先に配列を評価すると古い配列へ書いてしまう）
            // NewNode may resize the array, so take the index first and then store it (evaluating the array first would write into the old one)
            var rel = cell - _origin;
            var node = _rootNode;
            for (var level = _rootLevel; level > FirstRootLevel; level--)
            {
                var slot = node * 8 + ChildSlot(rel, level);
                var next = _children[slot];
                if (next == NoChild)
                {
                    next = NewNode();
                    _children[slot] = next;
                }
                node = next;
            }
            var leafSlot = node * 8 + ChildSlot(rel, FirstRootLevel);
            var leaf = _children[leafSlot];
            if (leaf == NoChild)
            {
                leaf = NewLeaf();
                _children[leafSlot] = leaf;
            }
            _leafBits[leaf] |= LeafBit(rel);
        }

        public void Remove(Vector3Int cell)
        {
            // 葉までの道を覚えながら下り、ビットを消す
            // Descend while recording the path, then clear the bit
            var rel = cell - _origin;
            var node = _rootNode;
            for (var level = _rootLevel; level > FirstRootLevel; level--)
            {
                _removePath[level] = node;
                node = _children[node * 8 + ChildSlot(rel, level)];
            }
            _removePath[FirstRootLevel] = node;
            var leafSlot = node * 8 + ChildSlot(rel, FirstRootLevel);
            var leaf = _children[leafSlot];
            _leafBits[leaf] &= ~LeafBit(rel);
            if (_leafBits[leaf] != 0UL) return;

            // 空になった葉と、子が無くなった親を根の手前まで刈り、番号を空きに戻す。根は縮めずに残す
            // Prune the empty leaf and childless parents up to (not including) the root, returning their indices; the root stays
            _children[leafSlot] = NoChild;
            _freeLeaves.Push(leaf);
            for (var level = FirstRootLevel; level < _rootLevel; level++)
            {
                var emptied = _removePath[level];
                if (HasAnyChild(emptied)) return;
                _freeNodes.Push(emptied);
                _children[_removePath[level + 1] * 8 + ChildSlot(rel, level + 1)] = NoChild;
            }
        }

        public bool Contains(Vector3Int cell)
        {
            var rel = cell - _origin;
            if (_rootNode == NoChild || !IsInsideRoot(rel)) return false;
            var node = _rootNode;
            for (var level = _rootLevel; level > FirstRootLevel; level--)
            {
                node = _children[node * 8 + ChildSlot(rel, level)];
                if (node == NoChild) return false;
            }
            var leaf = _children[node * 8 + ChildSlot(rel, FirstRootLevel)];
            return leaf != NoChild && (_leafBits[leaf] & LeafBit(rel)) != 0UL;
        }

        public void ResetProbePath()
        {
            _pathLowest = PathCapacity;
        }

        /// <summary>
        /// セルを含む空のノードのうち最も大きいものの段（0 は飛ばせない）と、セルが埋まっているかを返す
        /// Returns the level of the largest empty node containing the cell (0 when nothing can be skipped) and whether the cell is occupied
        /// </summary>
        public int ProbeEmptyLevel(Vector3Int cell, out bool occupied)
        {
            occupied = false;
            // 根の外は、走査の入口のセルか、外接直方体を出た直後（射程の終わり）のセルでしか起きないので、飛ばさずに1マス進ませる
            // Outside the root only happens at the walk's entry cell or right after leaving the bounding box (end of range), so let the walk step one cell
            var rel = cell - _origin;
            if (_rootNode == NoChild || !IsInsideRoot(rel))
            {
                _pathLowest = PathCapacity;
                return 0;
            }

            // 直前の道のうち、まだこのセルを含む最も深いノードから下りる
            // Descend from the deepest node on the previous path that still contains this cell
            var level = _rootLevel;
            var node = _rootNode;
            if (_pathLowest < PathCapacity)
            {
                var cachedLevel = _pathLowest;
                while (cachedLevel < _rootLevel && Shift(rel, cachedLevel) != _pathKey[cachedLevel]) cachedLevel++;
                if (cachedLevel < _rootLevel)
                {
                    level = cachedLevel;
                    node = _pathNode[cachedLevel];
                }
            }
            for (; level > FirstRootLevel; level--)
            {
                var next = _children[node * 8 + ChildSlot(rel, level)];
                if (next == NoChild)
                {
                    _pathLowest = level;
                    _pathNode[level] = node;
                    _pathKey[level] = Shift(rel, level);
                    return level - 1;
                }
                node = next;
                _pathNode[level - 1] = node;
                _pathKey[level - 1] = Shift(rel, level - 1);
            }
            _pathLowest = FirstRootLevel;

            // 葉が無ければ 4×4×4 ごと空。あればセルのビットを見る
            // No leaf means the whole 4x4x4 is empty; otherwise test the cell's bit
            var leaf = _children[node * 8 + ChildSlot(rel, FirstRootLevel)];
            if (leaf == NoChild) return LeafLevel;
            occupied = (_leafBits[leaf] & LeafBit(rel)) != 0UL;
            return 0;
        }

        public int NodeStart(int axis, int cellCoordinate, int level)
        {
            // ノードの位置は基準点からの相対で決まるので、相対で揃えてから戻す
            // Node positions are relative to the origin, so align in relative coordinates and convert back
            return _origin[axis] + (((cellCoordinate - _origin[axis]) >> level) << level);
        }

        private void GrowRootToward(Vector3Int rel)
        {
            // 各軸2倍に広げる。負の側へ広げる軸は基準点を根の大きさだけ下げ、古い根を上半分に置く（既存のノードはそのまま使える）
            // Double every axis; on axes grown to the negative side, lower the origin by the root size and put the old root in the upper half (existing nodes stay valid)
            // 段数の上限を超えるほど離れたセルは扱えない（約10億マス離れた設置でしか起きない）
            // Cells too far apart for the level cap cannot be held (only happens with placements about a billion cells apart)
            if (_rootLevel >= MaxRootLevel) throw new InvalidOperationException($"Occupied cell octree cannot grow past level {MaxRootLevel} toward relative cell {rel} from origin {_origin}.");
            var size = 1 << _rootLevel;
            var slot = 0;
            for (var axis = 0; axis < 3; axis++)
            {
                if (rel[axis] >= 0) continue;
                _origin[axis] -= size;
                slot |= 1 << axis;
            }
            var parent = NewNode();
            _children[parent * 8 + slot] = _rootNode;
            _rootNode = parent;
            _rootLevel++;
        }

        private bool IsInsideRoot(Vector3Int rel)
        {
            var size = 1 << _rootLevel;
            return rel.x >= 0 && rel.y >= 0 && rel.z >= 0 && rel.x < size && rel.y < size && rel.z < size;
        }

        private bool HasAnyChild(int node)
        {
            for (var i = 0; i < 8; i++)
            {
                if (_children[node * 8 + i] != NoChild) return true;
            }
            return false;
        }

        private int NewNode()
        {
            var node = _freeNodes.Count > 0 ? _freeNodes.Pop() : _nodeCount++;
            if (node * 8 + 8 > _children.Length) Array.Resize(ref _children, _children.Length * 2);
            for (var i = 0; i < 8; i++) _children[node * 8 + i] = NoChild;
            return node;
        }

        private int NewLeaf()
        {
            var leaf = _freeLeaves.Count > 0 ? _freeLeaves.Pop() : _leafCount++;
            if (leaf >= _leafBits.Length) Array.Resize(ref _leafBits, _leafBits.Length * 2);
            _leafBits[leaf] = 0UL;
            return leaf;
        }

        private static int ChildSlot(Vector3Int rel, int level)
        {
            var shift = level - 1;
            return ((rel.x >> shift) & 1) | (((rel.y >> shift) & 1) << 1) | (((rel.z >> shift) & 1) << 2);
        }

        private static ulong LeafBit(Vector3Int rel)
        {
            return 1UL << ((rel.x & 3) | ((rel.y & 3) << 2) | ((rel.z & 3) << 4));
        }

        private static Vector3Int Shift(Vector3Int rel, int level)
        {
            return new Vector3Int(rel.x >> level, rel.y >> level, rel.z >> level);
        }
    }
}
