// HierarchyOrderUtility.cs
// ヒエラルキー上（上→下、親→子）で GameObject/Transform/任意の MonoBehaviour をソートするユーティリティ
// 使い方：
//   // GameObject
//   var sorted = HierarchyOrderUtility.SortByHierarchy(originalList);
//
//   // Transform
//   var sortedTransforms = HierarchyOrderUtility.SortByHierarchy(transformList);
//
//   // 任意の MonoBehaviour（例：MyBehaviour）
//   var sortedBehaviours = HierarchyOrderUtility.SortByHierarchy<MyBehaviour>(behaviourList);

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Common.Util
{
    public static class HierarchyOrderUtility
    {
        /// <summary>
        /// ヒエラルキー表示順（上→下、親→子）で GameObject を並べ替えます。
        /// 複数シーンがロードされている場合は SceneManager.GetSceneAt(i) の順を優先します。
        /// null は除外されます。
        /// </summary>
        public static List<GameObject> SortByHierarchy(IEnumerable<GameObject> gameObjects)
        {
            if (gameObjects == null) return new List<GameObject>();
            
            // シーンの表示順（Hierarchy 左上のシーン順）をマップ化
            var sceneOrder = BuildSceneOrderIndex();
            
            // 並べ替え：HierarchyKey を比較キーにして安定ソート
            return gameObjects
                .Where(go => go != null)
                .Select(go => (go, key: BuildKey(go, sceneOrder)))
                .OrderBy(t => t.key) // IComparable 実装で比較
                .Select(t => t.go)
                .ToList();
        }
        
        /// <summary>
        /// ヒエラルキー表示順（上→下、親→子）で Transform を並べ替えます。
        /// </summary>
        public static List<Transform> SortByHierarchy(IEnumerable<Transform> transforms)
        {
            if (transforms == null) return new List<Transform>();
            return SortByHierarchy(transforms.Select(t => t ? t.gameObject : null))
                .Select(go => go.transform)
                .ToList();
        }
        
        /// <summary>
        /// ヒエラルキー表示順（上→下、親→子）で任意の MonoBehaviour 派生コンポーネントを並べ替えます。
        /// </summary>
        /// <typeparam name="T">MonoBehaviour を継承した任意のコンポーネント型</typeparam>
        /// <param name="components">並べ替え対象のコンポーネント列（null は除外）</param>
        public static List<T> SortByHierarchy<T>(IEnumerable<T> components) where T : MonoBehaviour
        {
            if (components == null) return new List<T>();
            
            var sceneOrder = BuildSceneOrderIndex();
            
            return components
                .Where(c => c != null)
                .Select(c => (comp: c, key: BuildKey(c.gameObject, sceneOrder)))
                .OrderBy(t => t.key)
                .Select(t => t.comp)
                .ToList();
        }
        
        // --- 内部実装 ---
        
        // 現在ロードされているシーンの順序を取得（Hierarchy のシーン順に合わせる）
        private static Dictionary<int, int> BuildSceneOrderIndex()
        {
            var dict = new Dictionary<int, int>(SceneManager.sceneCount);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                // Scene.handle は一意の int。これをキーに 0..N-1 の順序を割り当てる
                dict[s.handle] = i;
            }
            return dict;
        }
        
        // 指定 GameObject の「シーン順 + 兄弟インデックスのパス」を比較キーとして構築
        private static HierarchyKey BuildKey(GameObject go, Dictionary<int, int> sceneOrderIndex)
        {
            var t = go.transform;
            
            // ルート→対象 になるように兄弟インデックスのパスを作る
            // 例: Root(2) / A(0) / B(3) なら [2,0,3]
            var path = new List<int>(8);
            while (t != null)
            {
                path.Add(t.GetSiblingIndex());
                t = t.parent;
            }
            path.Reverse();
            
            // シーン順（未知のシーンは最後尾へ）
            int sceneHandle = go.scene.handle;
            int sceneOrd = sceneOrderIndex.TryGetValue(sceneHandle, out var ord)
                ? ord
                : int.MaxValue - 1;
            
            // 同一キーが完全一致した場合の安定化用に InstanceID でタイブレーク
            return new HierarchyKey(sceneOrd, path, go.GetInstanceID());
        }
        
        // 比較キー：シーン順 → 兄弟パス（辞書式）→ パス長（親が先）→ InstanceID
        private readonly struct HierarchyKey : IComparable<HierarchyKey>
        {
            public readonly int SceneOrder;
            public readonly int[] Path;     // ルート→子… の兄弟インデックス列
            public readonly int InstanceId; // タイブレーク用
            
            public HierarchyKey(int sceneOrder, List<int> path, int instanceId)
            {
                SceneOrder = sceneOrder;
                Path = path.ToArray();
                InstanceId = instanceId;
            }
            
            public int CompareTo(HierarchyKey other)
            {
                int c = SceneOrder.CompareTo(other.SceneOrder);
                if (c != 0) return c;
                
                int len = Math.Min(Path.Length, other.Path.Length);
                for (int i = 0; i < len; i++)
                {
                    c = Path[i].CompareTo(other.Path[i]);
                    if (c != 0) return c;
                }
                
                // ここまで完全一致の場合、親（パスが短い方）を先に
                c = Path.Length.CompareTo(other.Path.Length);
                if (c != 0) return c;
                
                // 最後に InstanceID で安定化
                return InstanceId.CompareTo(other.InstanceId);
            }
        }
    }
}
