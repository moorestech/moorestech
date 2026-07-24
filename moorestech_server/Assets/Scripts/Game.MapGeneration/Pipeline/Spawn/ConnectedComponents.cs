using System;
using System.Collections.Generic;

namespace Game.MapGeneration.Pipeline.Spawn
{
    // 整数グリッド上の4近傍連結成分。Burst 非依存の純粋ロジックで単体テスト可能。
    // 4-neighbor connected components over an int grid; pure, Burst-free, unit-testable.
    public static class ConnectedComponents
    {
        public sealed class Component
        {
            public int Label;
            public readonly List<int> Cells = new List<int>();
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
