using System;
using System.Collections.Generic;

namespace MapGenerator.Pipeline.Spawn
{
    /// <summary>
    /// 整数グリッド上の4近傍連結成分。Burst非依存の純粋ロジックで単体テスト可能。
    /// </summary>
    public static class ConnectedComponents
    {
        public sealed class Component
        {
            public int Label;
            public readonly List<int> Cells = new List<int>(); // index = y*width + x
            public int Area => Cells.Count;
            public int MinX = int.MaxValue, MinY = int.MaxValue;
            public int MaxX = int.MinValue, MaxY = int.MinValue;

            public void Add(int x, int y, int width)
            {
                Cells.Add(y * width + x);
                if (x < MinX) MinX = x;
                if (y < MinY) MinY = y;
                if (x > MaxX) MaxX = x;
                if (y > MaxY) MaxY = y;
            }
        }

        /// <summary>
        /// predicate(値) が真のセルを4近傍で連結成分に分割する。面積降順でソートして返す。
        /// </summary>
        public static List<Component> Label(int[] grid, int width, int height, Func<int, bool> predicate)
        {
            var labels = new int[grid.Length];
            for (int i = 0; i < labels.Length; i++) labels[i] = -1;
            var result = new List<Component>();
            var stack = new Stack<int>();

            for (int start = 0; start < grid.Length; start++)
            {
                if (labels[start] != -1) continue;
                if (!predicate(grid[start])) continue;

                var comp = new Component { Label = result.Count };
                labels[start] = comp.Label;
                stack.Push(start);

                while (stack.Count > 0)
                {
                    int idx = stack.Pop();
                    int x = idx % width;
                    int y = idx / width;
                    comp.Add(x, y, width);

                    TryPush(x - 1, y, width, height, grid, predicate, labels, comp.Label, stack);
                    TryPush(x + 1, y, width, height, grid, predicate, labels, comp.Label, stack);
                    TryPush(x, y - 1, width, height, grid, predicate, labels, comp.Label, stack);
                    TryPush(x, y + 1, width, height, grid, predicate, labels, comp.Label, stack);
                }
                result.Add(comp);
            }

            result.Sort((a, b) => b.Area.CompareTo(a.Area));
            for (int i = 0; i < result.Count; i++) result[i].Label = i;
            return result;
        }

        static void TryPush(int x, int y, int width, int height, int[] grid,
            Func<int, bool> predicate, int[] labels, int label, Stack<int> stack)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            int idx = y * width + x;
            if (labels[idx] != -1) return;
            if (!predicate(grid[idx])) return;
            labels[idx] = label;
            stack.Push(idx);
        }

        /// <summary>
        /// 成分A側のセルのうち、4近傍に成分Bのセルを1つ以上持つものの数を返す（セル数カウント、辺数ではない）。
        /// あるAセルがBに複数辺で接していても1としてカウントするため、(a,b)と(b,a)で結果が異なる非対称な指標。
        /// 呼び出し側は cellSize を乗じて接触長の近似値として用いる。
        /// </summary>
        public static int BorderContactCells(Component a, Component b,
            int[] grid, int width, int height)
        {
            var bSet = new HashSet<int>(b.Cells);
            int contact = 0;
            foreach (int idx in a.Cells)
            {
                int x = idx % width;
                int y = idx / width;
                if (IsNeighborIn(x - 1, y, width, height, bSet)) { contact++; continue; }
                if (IsNeighborIn(x + 1, y, width, height, bSet)) { contact++; continue; }
                if (IsNeighborIn(x, y - 1, width, height, bSet)) { contact++; continue; }
                if (IsNeighborIn(x, y + 1, width, height, bSet)) { contact++; continue; }
            }
            return contact;
        }

        static bool IsNeighborIn(int x, int y, int width, int height, HashSet<int> set)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return false;
            return set.Contains(y * width + x);
        }
    }
}
