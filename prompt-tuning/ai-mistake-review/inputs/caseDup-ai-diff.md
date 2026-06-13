# AIコミット diff (base->ai) — レビュー対象
```diff
commit 46797bfd1f731f41d24b535e77e0305193c18198
Author: sakastudio <sakastudio100@gmail.com>
Date:   Mon May 11 14:32:46 2026 +0900

    ポンプの液体供給をFluidMapVein連動に変更
    
    Minerと同じ仕様で、ポンプは設置位置に登録されたFluidMapVeinの上にあるときだけ
    液体を生成する。マスタのgenerateFluidは生成レート表として残り、Veinが指すFluidIdが
    マスタ内に存在する場合のみ、その量・時間で生成する。
    
    - IFluidMapVein/FluidMapVein/FluidMapVeinDatastore を追加
    - MapInfoJson に fluidVeins フィールドを追加（null許容で後方互換）
    - ServerContext と DI に IFluidMapVeinDatastore を登録
    - PumpFluidGenerationUtility が ServerContext.FluidMapVeinDatastore を参照
    - FluidMapVeinGameObject と Inspector、MapExportAndSetting に出力追加
    - PumpFluidVeinTest を追加（Vein一致／Vein無し／マスタ不一致の3ケース）
    
    Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>

diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/FluidMapVeinGameObject.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/FluidMapVeinGameObject.cs
new file mode 100644
index 000000000..10df0a20b
--- /dev/null
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/FluidMapVeinGameObject.cs
@@ -0,0 +1,66 @@
+using System;
+using UnityEngine;
+
+namespace Client.Game.InGame.Map.MapVein
+{
+    [ExecuteAlways]
+    public class FluidMapVeinGameObject : MonoBehaviour
+    {
+        public Vector3Int MinPosition => new(
+            Mathf.RoundToInt(transform.position.x - bounds.size.x / 2f + bounds.center.x),
+            Mathf.RoundToInt(transform.position.y - bounds.size.y / 2f + bounds.center.y),
+            Mathf.RoundToInt(transform.position.z - bounds.size.z / 2f + bounds.center.z));
+
+        public Vector3Int MaxPosition => new(
+            Mathf.RoundToInt(transform.position.x + bounds.size.x / 2f + bounds.center.x),
+            Mathf.RoundToInt(transform.position.y + bounds.size.y / 2f + bounds.center.y),
+            Mathf.RoundToInt(transform.position.z + bounds.size.z / 2f + bounds.center.z));
+
+        public Guid VeinFluidGuid => Guid.Parse(veinFluidGuid);
+        [SerializeField] private string veinFluidGuid;
+
+        public Bounds Bounds => bounds;
+        [SerializeField] private Bounds bounds = new(Vector3.zero, Vector3.one);
+
+        public void SetBounds(Bounds setBounds)
+        {
+            bounds = setBounds;
+
+            var size = bounds.size;
+            var sizeX = size.x < 1 ? 1 : Mathf.RoundToInt(size.x);
+            var sizeY = size.y < 1 ? 1 : Mathf.RoundToInt(size.y);
+            var sizeZ = size.z < 1 ? 1 : Mathf.RoundToInt(size.z);
+            bounds.size = new Vector3(sizeX, sizeY, sizeZ);
+
+            var centerX = sizeX % 2f == 0 ? 0 : 0.5f;
+            var centerY = sizeY % 2f == 0 ? 0 : 0.5f;
+            var centerZ = sizeZ % 2f == 0 ? 0 : 0.5f;
+            bounds.center = new Vector3(centerX, centerY, centerZ);
+        }
+
+        private void Update()
+        {
+#if UNITY_EDITOR
+            OnEditorUpdate();
+#endif
+        }
+
+        private void OnEditorUpdate()
+        {
+            SetBounds(bounds);
+        }
+
+        private void OnDrawGizmosSelected()
+        {
+            var gizmoBounds = new Bounds();
+            gizmoBounds.SetMinMax(MinPosition, MaxPosition);
+
+            // 液体Veinは青系で表示してアイテムVein(赤)と区別
+            // Render fluid vein in blue to distinguish from item vein (red)
+            var color = Color.blue;
+            color.a = 0.5f;
+            Gizmos.color = color;
+            Gizmos.DrawCube(gizmoBounds.center, gizmoBounds.size);
+        }
+    }
+}
diff --git a/moorestech_client/Assets/Scripts/Editor/Inspector/FluidMapVeinGameObjectInspector.cs b/moorestech_client/Assets/Scripts/Editor/Inspector/FluidMapVeinGameObjectInspector.cs
new file mode 100644
index 000000000..37f6ccc7b
--- /dev/null
+++ b/moorestech_client/Assets/Scripts/Editor/Inspector/FluidMapVeinGameObjectInspector.cs
@@ -0,0 +1,35 @@
+using Client.Game.InGame.Map.MapVein;
+using UnityEditor;
+using UnityEditor.IMGUI.Controls;
+using UnityEngine;
+
+[CustomEditor(typeof(FluidMapVeinGameObject))]
+public class FluidMapVeinGameObjectInspector : Editor
+{
+    private readonly BoxBoundsHandle _boxBoundsHandle = new();
+
+    private void OnSceneGUI()
+    {
+        var fluidVein = target as FluidMapVeinGameObject;
+        if (fluidVein == null)
+        {
+            return;
+        }
+
+        EditorGUI.BeginChangeCheck();
+
+        _boxBoundsHandle.center = fluidVein.Bounds.center + fluidVein.transform.position;
+        _boxBoundsHandle.size = fluidVein.Bounds.size;
+
+        _boxBoundsHandle.SetColor(Color.blue);
+        _boxBoundsHandle.DrawHandle();
+
+        if (EditorGUI.EndChangeCheck())
+        {
+            var bounds = new Bounds(_boxBoundsHandle.center, _boxBoundsHandle.size);
+            fluidVein.SetBounds(bounds);
+            Undo.RecordObject(fluidVein, "Change Bounds");
+            EditorUtility.SetDirty(fluidVein);
+        }
+    }
+}
diff --git a/moorestech_server/Assets/Scripts/Game.Map.Interface/Vein/IFluidMapVein.cs b/moorestech_server/Assets/Scripts/Game.Map.Interface/Vein/IFluidMapVein.cs
new file mode 100644
index 000000000..3933b0e2f
--- /dev/null
+++ b/moorestech_server/Assets/Scripts/Game.Map.Interface/Vein/IFluidMapVein.cs
@@ -0,0 +1,13 @@
+using Core.Master;
+using UnityEngine;
+
+namespace Game.Map.Interface.Vein
+{
+    public interface IFluidMapVein
+    {
+        public FluidId VeinFluidId { get; }
+
+        public Vector3Int VeinRangeMin { get; }
+        public Vector3Int VeinRangeMax { get; }
+    }
+}
diff --git a/moorestech_server/Assets/Scripts/Game.Map.Interface/Vein/IFluidMapVeinDatastore.cs b/moorestech_server/Assets/Scripts/Game.Map.Interface/Vein/IFluidMapVeinDatastore.cs
new file mode 100644
index 000000000..4f44f6b3c
--- /dev/null
+++ b/moorestech_server/Assets/Scripts/Game.Map.Interface/Vein/IFluidMapVeinDatastore.cs
@@ -0,0 +1,10 @@
+using System.Collections.Generic;
+using UnityEngine;
+
+namespace Game.Map.Interface.Vein
+{
+    public interface IFluidMapVeinDatastore
+    {
+        public List<IFluidMapVein> GetOverVeins(Vector3Int pos);
+    }
+}
diff --git a/moorestech_server/Assets/Scripts/Game.Map/FluidMapVein.cs b/moorestech_server/Assets/Scripts/Game.Map/FluidMapVein.cs
new file mode 100644
index 000000000..161d1e51c
--- /dev/null
+++ b/moorestech_server/Assets/Scripts/Game.Map/FluidMapVein.cs
@@ -0,0 +1,20 @@
+using Core.Master;
+using Game.Map.Interface.Vein;
+using UnityEngine;
+
+namespace Game.Map
+{
+    public class FluidMapVein : IFluidMapVein
+    {
+        public FluidId VeinFluidId { get; }
+        public Vector3Int VeinRangeMin { get; }
+        public Vector3Int VeinRangeMax { get; }
+
+        public FluidMapVein(FluidId veinFluidId, Vector3Int veinRangeMin, Vector3Int veinRangeMax)
+        {
+            VeinFluidId = veinFluidId;
+            VeinRangeMin = veinRangeMin;
+            VeinRangeMax = veinRangeMax;
+        }
+    }
+}
diff --git a/moorestech_server/Assets/Scripts/Game.Map/FluidMapVeinDatastore.cs b/moorestech_server/Assets/Scripts/Game.Map/FluidMapVeinDatastore.cs
new file mode 100644
index 000000000..7d795c146
--- /dev/null
+++ b/moorestech_server/Assets/Scripts/Game.Map/FluidMapVeinDatastore.cs
@@ -0,0 +1,45 @@
+using System.Collections.Generic;
+using Core.Master;
+using Game.Map.Interface.Json;
+using Game.Map.Interface.Vein;
+using UnityEngine;
+
+namespace Game.Map
+{
+    public class FluidMapVeinDatastore : IFluidMapVeinDatastore
+    {
+        private readonly List<IFluidMapVein> _fluidVeins = new();
+
+        public FluidMapVeinDatastore(MapInfoJson mapInfoJson)
+        {
+            // 既存map.jsonとの互換のためnull許容
+            // Allow null for backward compatibility with legacy map.json
+            if (mapInfoJson.FluidVeins == null) return;
+
+            foreach (var veinJson in mapInfoJson.FluidVeins)
+            {
+                var fluidId = MasterHolder.FluidMaster.GetFluidIdOrNull(veinJson.VeinFluidGuid);
+                if (fluidId == null)
+                {
+                    Debug.LogError($"GUID:{veinJson.VeinFluidGuid}に対応するFluidIdが存在しません。液体鉱脈の生成をスキップします。");
+                    continue;
+                }
+
+                var vein = new FluidMapVein(fluidId.Value, veinJson.MinPosition, veinJson.MaxPosition);
+                _fluidVeins.Add(vein);
+            }
+        }
+
+        public List<IFluidMapVein> GetOverVeins(Vector3Int pos)
+        {
+            var veins = new List<IFluidMapVein>();
+            foreach (var vein in _fluidVeins)
+                if (vein.VeinRangeMin.x <= pos.x && pos.x <= vein.VeinRangeMax.x &&
+                    vein.VeinRangeMin.y <= pos.y && pos.y <= vein.VeinRangeMax.y &&
+                    vein.VeinRangeMin.z <= pos.z && pos.z <= vein.VeinRangeMax.z)
+                    veins.Add(vein);
+
+            return veins;
+        }
+    }
+}
```
