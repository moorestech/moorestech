# GOLDEN: 人間が直した内容（捕捉できれば合格）
観点ID: D（コピペ重複の未共通化）

AIは MapVeinGameObject と FluidMapVeinGameObject に Min/Max座標計算・bounds正規化・Gizmo/Inspector描画をコピペで重複実装した。人間が共通ロジックを MapVeinGameObjectService / MapVeinGameObjectEditorService に抽出して重複を解消。レビューはこの2クラス間のコピペ重複（共通サービスへ抽出すべき）を指摘すべき。

```diff
commit 4a6ddc108ff0abcd3dddf79886d5698563ac873c
Author: sakastudio <sakastudio100@gmail.com>
Date:   Mon May 11 17:33:17 2026 +0900

    AIが書いたコードの修正その1 Vein関連の共通化

diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/FluidMapVeinGameObject.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/FluidMapVeinGameObject.cs
index 10df0a20b..40c8fcd70 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/FluidMapVeinGameObject.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/FluidMapVeinGameObject.cs
@@ -6,61 +6,30 @@ namespace Client.Game.InGame.Map.MapVein
     [ExecuteAlways]
     public class FluidMapVeinGameObject : MonoBehaviour
     {
-        public Vector3Int MinPosition => new(
-            Mathf.RoundToInt(transform.position.x - bounds.size.x / 2f + bounds.center.x),
-            Mathf.RoundToInt(transform.position.y - bounds.size.y / 2f + bounds.center.y),
-            Mathf.RoundToInt(transform.position.z - bounds.size.z / 2f + bounds.center.z));
-
-        public Vector3Int MaxPosition => new(
-            Mathf.RoundToInt(transform.position.x + bounds.size.x / 2f + bounds.center.x),
-            Mathf.RoundToInt(transform.position.y + bounds.size.y / 2f + bounds.center.y),
-            Mathf.RoundToInt(transform.position.z + bounds.size.z / 2f + bounds.center.z));
+        public Vector3Int MinPosition => Service.MinPosition(bounds);
+        public Vector3Int MaxPosition => Service.MaxPosition(bounds);
 
         public Guid VeinFluidGuid => Guid.Parse(veinFluidGuid);
         [SerializeField] private string veinFluidGuid;
 
         public Bounds Bounds => bounds;
         [SerializeField] private Bounds bounds = new(Vector3.zero, Vector3.one);
+        
+        private MapVeinGameObjectService _service;
+        public MapVeinGameObjectService Service => _service ??= new MapVeinGameObjectService(transform);
 
-        public void SetBounds(Bounds setBounds)
-        {
-            bounds = setBounds;
-
-            var size = bounds.size;
-            var sizeX = size.x < 1 ? 1 : Mathf.RoundToInt(size.x);
-            var sizeY = size.y < 1 ? 1 : Mathf.RoundToInt(size.y);
-            var sizeZ = size.z < 1 ? 1 : Mathf.RoundToInt(size.z);
-            bounds.size = new Vector3(sizeX, sizeY, sizeZ);
-
-            var centerX = sizeX % 2f == 0 ? 0 : 0.5f;
-            var centerY = sizeY % 2f == 0 ? 0 : 0.5f;
-            var centerZ = sizeZ % 2f == 0 ? 0 : 0.5f;
-            bounds.center = new Vector3(centerX, centerY, centerZ);
-        }
+        public void SetBounds(Bounds setBounds) => bounds = MapVeinGameObjectService.NormalizeBounds(setBounds);
 
         private void Update()
         {
 #if UNITY_EDITOR
-            OnEditorUpdate();
+            bounds = MapVeinGameObjectService.NormalizeBounds(bounds);
 #endif
         }
 
-        private void OnEditorUpdate()
-        {
-            SetBounds(bounds);
-        }
-
         private void OnDrawGizmosSelected()
         {
-            var gizmoBounds = new Bounds();
-            gizmoBounds.SetMinMax(MinPosition, MaxPosition);
-
-            // 液体Veinは青系で表示してアイテムVein(赤)と区別
-            // Render fluid vein in blue to distinguish from item vein (red)
-            var color = Color.blue;
-            color.a = 0.5f;
-            Gizmos.color = color;
-            Gizmos.DrawCube(gizmoBounds.center, gizmoBounds.size);
+            Service.DrowGizmo(bounds, Color.blue);
         }
     }
 }
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinGameObject.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinGameObject.cs
index 45fe8228c..e8268ccac 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinGameObject.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinGameObject.cs
@@ -6,59 +6,30 @@ namespace Client.Game.InGame.Map.MapVein
     [ExecuteAlways]
     public class MapVeinGameObject : MonoBehaviour
     {
-        public Vector3Int MinPosition => new(
-            Mathf.RoundToInt(transform.position.x - bounds.size.x / 2f + bounds.center.x),
-            Mathf.RoundToInt(transform.position.y - bounds.size.y / 2f + bounds.center.y),
-            Mathf.RoundToInt(transform.position.z - bounds.size.z / 2f + bounds.center.z));
-        
-        public Vector3Int MaxPosition => new(
-            Mathf.RoundToInt(transform.position.x + bounds.size.x / 2f + bounds.center.x),
-            Mathf.RoundToInt(transform.position.y + bounds.size.y / 2f + bounds.center.y),
-            Mathf.RoundToInt(transform.position.z + bounds.size.z / 2f + bounds.center.z));
-        
+        public Vector3Int MinPosition => Service.MinPosition(bounds);
+        public Vector3Int MaxPosition => Service.MaxPosition(bounds);
+
         public Guid VeinItemGuid => Guid.Parse(veinItemGuid);
         [SerializeField] private string veinItemGuid;
-        
+
         public Bounds Bounds => bounds;
         [SerializeField] private Bounds bounds = new(Vector3.zero, Vector3.one);
-        
-        public void SetBounds(Bounds setBounds)
-        {
-            bounds = setBounds;
-            
-            var size = bounds.size;
-            var sizeX = size.x < 1 ? 1 : Mathf.RoundToInt(size.x);
-            var sizeY = size.y < 1 ? 1 : Mathf.RoundToInt(size.y);
-            var sizeZ = size.z < 1 ? 1 : Mathf.RoundToInt(size.z);
-            bounds.size = new Vector3(sizeX, sizeY, sizeZ);
-            
-            var centerX = sizeX % 2f == 0 ? 0 : 0.5f;
-            var centerY = sizeY % 2f == 0 ? 0 : 0.5f;
-            var centerZ = sizeZ % 2f == 0 ? 0 : 0.5f;
-            bounds.center = new Vector3(centerX, centerY, centerZ);
-        }
-        
+
+        private MapVeinGameObjectService _service;
+        public MapVeinGameObjectService Service => _service ??= new MapVeinGameObjectService(transform);
+
+        public void SetBounds(Bounds setBounds) => bounds = MapVeinGameObjectService.NormalizeBounds(setBounds);
+
         private void Update()
         {
 #if UNITY_EDITOR
-            OnEditorUpdate();
+            bounds = MapVeinGameObjectService.NormalizeBounds(bounds);
 #endif
         }
-        
-        private void OnEditorUpdate()
-        {;
-            SetBounds(bounds);
-        }
-        
+
         private void OnDrawGizmosSelected()
         {
-            var gizmoBounds = new Bounds();
-            gizmoBounds.SetMinMax(MinPosition, MaxPosition);
-            
-            var color = Color.red;
-            color.a = 0.5f;
-            Gizmos.color = color;
-            Gizmos.DrawCube(gizmoBounds.center, gizmoBounds.size);
+            Service.DrowGizmo(bounds, Color.red);
         }
     }
-}
\ No newline at end of file
+}
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinGameObjectService.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinGameObjectService.cs
new file mode 100644
index 000000000..7a09e6dc8
--- /dev/null
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinGameObjectService.cs
@@ -0,0 +1,56 @@
+using UnityEngine;
+
+namespace Client.Game.InGame.Map.MapVein
+{
+    public class MapVeinGameObjectService
+    {
+        public Transform Transform { get; }
+        
+        public MapVeinGameObjectService(Transform transform)
+        {
+            Transform = transform;
+        }
+
+        // transformとboundsからMin/Max座標を計算
+        // Calculate Min/Max world positions from transform and bounds
+        public Vector3Int MinPosition(Bounds bounds) => new(
+            Mathf.RoundToInt(Transform.position.x - bounds.size.x / 2f + bounds.center.x),
+            Mathf.RoundToInt(Transform.position.y - bounds.size.y / 2f + bounds.center.y),
+            Mathf.RoundToInt(Transform.position.z - bounds.size.z / 2f + bounds.center.z));
+
+        public Vector3Int MaxPosition(Bounds bounds) => new(
+            Mathf.RoundToInt(Transform.position.x + bounds.size.x / 2f + bounds.center.x),
+            Mathf.RoundToInt(Transform.position.y + bounds.size.y / 2f + bounds.center.y),
+            Mathf.RoundToInt(Transform.position.z + bounds.size.z / 2f + bounds.center.z));
+
+        // サイズを最小1に丸め、偶数/奇数でcenterオフセットを調整して正規化
+        // Normalize bounds: clamp size to min 1, adjust center offset for even/odd dimensions
+        public static Bounds NormalizeBounds(Bounds bounds)
+        {
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
+
+            return bounds;
+        }
+
+        // Gizmo描画用のワールド空間Boundsを返す
+        // Return world-space bounds for Gizmo rendering
+        public void DrowGizmo(Bounds bounds, Color color)
+        {
+            var gizmoBounds = new Bounds();
+            gizmoBounds.SetMinMax(MinPosition(bounds), MaxPosition(bounds));
+
+            color.a = 0.5f;
+            Gizmos.color = color;
+            Gizmos.DrawCube(gizmoBounds.center, gizmoBounds.size);
+        }
+    }
+}
diff --git a/moorestech_client/Assets/Scripts/Editor/Inspector/FluidMapVeinGameObjectInspector.cs b/moorestech_client/Assets/Scripts/Editor/Inspector/FluidMapVeinGameObjectInspector.cs
index 37f6ccc7b..88d22789c 100644
--- a/moorestech_client/Assets/Scripts/Editor/Inspector/FluidMapVeinGameObjectInspector.cs
+++ b/moorestech_client/Assets/Scripts/Editor/Inspector/FluidMapVeinGameObjectInspector.cs
@@ -1,35 +1,17 @@
 using Client.Game.InGame.Map.MapVein;
 using UnityEditor;
-using UnityEditor.IMGUI.Controls;
 using UnityEngine;
 
 [CustomEditor(typeof(FluidMapVeinGameObject))]
 public class FluidMapVeinGameObjectInspector : Editor
 {
-    private readonly BoxBoundsHandle _boxBoundsHandle = new();
+    private readonly MapVeinGameObjectEditorService _editorService = new();
 
     private void OnSceneGUI()
     {
         var fluidVein = target as FluidMapVeinGameObject;
-        if (fluidVein == null)
-        {
-            return;
-        }
+        if (fluidVein == null) return;
 
-        EditorGUI.BeginChangeCheck();
-
-        _boxBoundsHandle.center = fluidVein.Bounds.center + fluidVein.transform.position;
-        _boxBoundsHandle.size = fluidVein.Bounds.size;
-
-        _boxBoundsHandle.SetColor(Color.blue);
-        _boxBoundsHandle.DrawHandle();
-
-        if (EditorGUI.EndChangeCheck())
-        {
-            var bounds = new Bounds(_boxBoundsHandle.center, _boxBoundsHandle.size);
-            fluidVein.SetBounds(bounds);
-            Undo.RecordObject(fluidVein, "Change Bounds");
-            EditorUtility.SetDirty(fluidVein);
-        }
+        _editorService.DrawSceneGUI(fluidVein.Service, fluidVein.Bounds, fluidVein.SetBounds, fluidVein, Color.blue);
     }
 }
diff --git a/moorestech_client/Assets/Scripts/Editor/Inspector/MapVeinGameObjectEditorService.cs b/moorestech_client/Assets/Scripts/Editor/Inspector/MapVeinGameObjectEditorService.cs
new file mode 100644
index 000000000..34e8a22d6
--- /dev/null
+++ b/moorestech_client/Assets/Scripts/Editor/Inspector/MapVeinGameObjectEditorService.cs
@@ -0,0 +1,31 @@
+using System;
+using Client.Game.InGame.Map.MapVein;
+using UnityEditor;
+using UnityEditor.IMGUI.Controls;
+using UnityEngine;
+using Object = UnityEngine.Object;
+
+// SceneGUIのBoxBoundsHandle操作とUndo登録の共通処理
+// Common SceneGUI logic for MapVein objects: BoxBoundsHandle manipulation and Undo registration
+public class MapVeinGameObjectEditorService
+{
+    private readonly BoxBoundsHandle _handle = new();
+
+    public void DrawSceneGUI(MapVeinGameObjectService service, Bounds bounds, Action<Bounds> setBoundsAction, Object undoTarget, Color color)
+    {
+        EditorGUI.BeginChangeCheck();
+
+        _handle.center = bounds.center + service.Transform.position;
+        _handle.size = bounds.size;
+        _handle.SetColor(color);
+        _handle.DrawHandle();
+
+        if (EditorGUI.EndChangeCheck())
+        {
+            setBoundsAction(new Bounds(_handle.center, _handle.size));
+            
+            Undo.RecordObject(undoTarget, "Change Bounds");
+            EditorUtility.SetDirty(undoTarget);
+        }
+    }
+}
diff --git a/moorestech_client/Assets/Scripts/Editor/Inspector/MapVeinGameObjectInspector.cs b/moorestech_client/Assets/Scripts/Editor/Inspector/MapVeinGameObjectInspector.cs
index 0469b388d..63080bd4f 100644
--- a/moorestech_client/Assets/Scripts/Editor/Inspector/MapVeinGameObjectInspector.cs
+++ b/moorestech_client/Assets/Scripts/Editor/Inspector/MapVeinGameObjectInspector.cs
@@ -1,37 +1,17 @@
-using System;
 using Client.Game.InGame.Map.MapVein;
 using UnityEditor;
-using UnityEditor.IMGUI.Controls;
 using UnityEngine;
 
 [CustomEditor(typeof(MapVeinGameObject))]
 public class MapVeinGameObjectInspector : Editor
 {
-    private readonly BoxBoundsHandle _boxBoundsHandle = new();
-    
+    private readonly MapVeinGameObjectEditorService _editorService = new();
+
     private void OnSceneGUI()
     {
         var mapVein = target as MapVeinGameObject;
-        if (mapVein == null)
-        {
-            return;
-        }
-        
-        EditorGUI.BeginChangeCheck();
-        
-        _boxBoundsHandle.center = mapVein.Bounds.center + mapVein.transform.position;
-        _boxBoundsHandle.size = mapVein.Bounds.size;
-        
-        _boxBoundsHandle.SetColor(Color.red);
-        _boxBoundsHandle.DrawHandle();
-        
-        if (EditorGUI.EndChangeCheck())
-        {
-            
-            var bounds = new Bounds(_boxBoundsHandle.center, _boxBoundsHandle.size);
-            mapVein.SetBounds(bounds);
-            Undo.RecordObject(mapVein, "Change Bounds");
-            EditorUtility.SetDirty(mapVein);
-        }
+        if (mapVein == null) return;
+
+        _editorService.DrawSceneGUI(mapVein.Service, mapVein.Bounds, mapVein.SetBounds, mapVein, Color.red);
     }
-}
\ No newline at end of file
+}
```
