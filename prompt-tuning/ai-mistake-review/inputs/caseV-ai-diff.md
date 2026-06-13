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
diff --git a/moorestech_client/Assets/Scripts/Editor/MapExportAndSetting.cs b/moorestech_client/Assets/Scripts/Editor/MapExportAndSetting.cs
index a4e9964af..c375a99ac 100644
--- a/moorestech_client/Assets/Scripts/Editor/MapExportAndSetting.cs
+++ b/moorestech_client/Assets/Scripts/Editor/MapExportAndSetting.cs
@@ -20,6 +20,7 @@ public class MapExportAndSetting : EditorWindow
             DefaultSpawnPointJson = GetSpawnPointJson(),
             MapObjects = SetUpMapObjectInfos(),
             MapVeins = GetMapVeinInfo(),
+            FluidVeins = GetFluidVeinInfo(),
         };
         
         // jsonに変換
@@ -83,7 +84,7 @@ public class MapExportAndSetting : EditorWindow
         {
             var veins = FindObjectsOfType<MapVeinGameObject>(true);
             var result = new List<MapVeinInfoJson>();
-            
+
             foreach (var vein in veins)
             {
                 var config = new MapVeinInfoJson
@@ -92,17 +93,41 @@ public class MapExportAndSetting : EditorWindow
                     MinX = vein.MinPosition.x,
                     MinY = vein.MinPosition.y,
                     MinZ = vein.MinPosition.z,
-                    
+
                     MaxX = vein.MaxPosition.x,
                     MaxY = vein.MaxPosition.y,
                     MaxZ = vein.MaxPosition.z,
                 };
                 result.Add(config);
             }
-            
+
             return result;
         }
-        
+
+        List<FluidVeinInfoJson> GetFluidVeinInfo()
+        {
+            var veins = FindObjectsOfType<FluidMapVeinGameObject>(true);
+            var result = new List<FluidVeinInfoJson>();
+
+            foreach (var vein in veins)
+            {
+                var config = new FluidVeinInfoJson
+                {
+                    VeinFluidGuidStr = vein.VeinFluidGuid.ToString(),
+                    MinX = vein.MinPosition.x,
+                    MinY = vein.MinPosition.y,
+                    MinZ = vein.MinPosition.z,
+
+                    MaxX = vein.MaxPosition.x,
+                    MaxY = vein.MaxPosition.y,
+                    MaxZ = vein.MaxPosition.z,
+                };
+                result.Add(config);
+            }
+
+            return result;
+        }
+
         #endregion
     }
     
diff --git a/moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/GearPumpComponent.cs b/moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/GearPumpComponent.cs
index 237d63019..ccb5d4649 100644
--- a/moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/GearPumpComponent.cs
+++ b/moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/GearPumpComponent.cs
@@ -13,12 +13,14 @@ namespace Game.Block.Blocks.Gear
         private readonly GearPumpBlockParam _param;
         private readonly GearEnergyTransformer _gearEnergyTransformer;
         private readonly PumpFluidOutputComponent _output;
+        private readonly BlockPositionInfo _blockPositionInfo;
 
-        public GearPumpComponent(GearPumpBlockParam param, GearEnergyTransformer gearEnergyTransformer, PumpFluidOutputComponent output)
+        public GearPumpComponent(GearPumpBlockParam param, GearEnergyTransformer gearEnergyTransformer, PumpFluidOutputComponent output, BlockPositionInfo blockPositionInfo)
         {
             _param = param;
             _gearEnergyTransformer = gearEnergyTransformer;
             _output = output;
+            _blockPositionInfo = blockPositionInfo;
         }
 
         public void Update()
@@ -30,7 +32,8 @@ namespace Game.Block.Blocks.Gear
             PumpFluidGenerationUtility.GenerateFluids(
                 _param.GenerateFluid.items,
                 _gearEnergyTransformer.GetCurrentOperatingRate(),
-                _output);
+                _output,
+                _blockPositionInfo.OriginalPos);
         }
 
         public bool IsDestroy { get; private set; }
diff --git a/moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/ElectricPumpProcessorComponent.cs b/moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/ElectricPumpProcessorComponent.cs
index cde9c6f6a..446ee4ab9 100644
--- a/moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/ElectricPumpProcessorComponent.cs
+++ b/moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/ElectricPumpProcessorComponent.cs
@@ -18,13 +18,15 @@ namespace Game.Block.Blocks.Pump
         private readonly ElectricPumpBlockParam _param;
         private readonly PumpFluidOutputComponent _output;
         private readonly ElectricPower _requiredPower;
+        private readonly BlockPositionInfo _blockPositionInfo;
         private ElectricPower _currentPower;
 
-        public ElectricPumpProcessorComponent(ElectricPumpBlockParam param, PumpFluidOutputComponent output)
+        public ElectricPumpProcessorComponent(ElectricPumpBlockParam param, PumpFluidOutputComponent output, BlockPositionInfo blockPositionInfo)
         {
             _param = param;
             _output = output;
             _requiredPower = new ElectricPower(Mathf.Max(0.0001f, param.RequiredPower));
+            _blockPositionInfo = blockPositionInfo;
         }
 
         public void SupplyPower(ElectricPower power)
@@ -45,7 +47,8 @@ namespace Game.Block.Blocks.Pump
             PumpFluidGenerationUtility.GenerateFluids(
                 _param.GenerateFluid.items,
                 powerRate,
-                _output);
+                _output,
+                _blockPositionInfo.OriginalPos);
 
             _currentPower = new ElectricPower(0);
         }
diff --git a/moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/PumpFluidGenerationUtility.cs b/moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/PumpFluidGenerationUtility.cs
index e308e2ff2..2ef4e44e6 100644
--- a/moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/PumpFluidGenerationUtility.cs
+++ b/moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/PumpFluidGenerationUtility.cs
@@ -1,8 +1,11 @@
 using System;
+using System.Collections.Generic;
 using Core.Master;
 using Core.Update;
+using Game.Context;
 using Game.Fluid;
 using Mooresmaster.Model.GenerateFluidsModule;
+using UnityEngine;
 
 namespace Game.Block.Blocks.Pump
 {
@@ -11,13 +14,21 @@ namespace Game.Block.Blocks.Pump
     /// </summary>
     public static class PumpFluidGenerationUtility
     {
-        public static void GenerateFluids(Element[] generateFluids, float powerRate, PumpFluidOutputComponent output)
+        public static void GenerateFluids(Element[] generateFluids, float powerRate, PumpFluidOutputComponent output, Vector3Int blockPos)
         {
             if (powerRate <= 0f || output == null || generateFluids == null || generateFluids.Length == 0)
             {
                 return;
             }
 
+            // 設置位置にFluidMapVeinが無ければ何も生成しない（Minerと同仕様）
+            // No generation if no FluidMapVein at this position (same spec as Miner)
+            var veins = ServerContext.FluidMapVeinDatastore.GetOverVeins(blockPos);
+            if (veins.Count == 0) return;
+
+            var veinFluidIds = new HashSet<FluidId>();
+            foreach (var vein in veins) veinFluidIds.Add(vein.VeinFluidId);
+
             // tick数を秒数に変換
             // Convert ticks to seconds
             var deltaSeconds = GameUpdater.SecondsPerTick;
@@ -26,11 +37,15 @@ namespace Game.Block.Blocks.Pump
             {
                 if (gen.GenerateTime <= 0) continue;
 
+                var fluidId = MasterHolder.FluidMaster.GetFluidId(gen.FluidGuid);
+                // VeinのFluidIdに無い液体はスキップ（マスタは生成レート表として機能）
+                // Skip fluids not present in any vein at this position
+                if (!veinFluidIds.Contains(fluidId)) continue;
+
                 var perSecond = gen.Amount / Math.Max(0.0001, gen.GenerateTime);
                 var addAmount = perSecond * powerRate * deltaSeconds;
                 if (addAmount <= 0) continue;
 
-                var fluidId = MasterHolder.FluidMaster.GetFluidId(gen.FluidGuid);
                 var stack = new FluidStack(addAmount, fluidId);
                 output.EnqueueGeneratedFluid(stack);
             }
diff --git a/moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaElectricPumpTemplate.cs b/moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaElectricPumpTemplate.cs
index 3b5438285..99cc84a5c 100644
--- a/moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaElectricPumpTemplate.cs
+++ b/moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaElectricPumpTemplate.cs
@@ -30,7 +30,7 @@ namespace Game.Block.Factory.BlockTemplate
             var outputComponent = componentStates == null
                 ? new PumpFluidOutputComponent(param.InnerTankCapacity, fluidConnector)
                 : new PumpFluidOutputComponent(componentStates, param.InnerTankCapacity, fluidConnector);
-            var processorComponent = new ElectricPumpProcessorComponent(param, outputComponent);
+            var processorComponent = new ElectricPumpProcessorComponent(param, outputComponent, blockPositionInfo);
             var electricComponent = new ElectricPumpComponent(blockInstanceId, new ElectricPower(param.RequiredPower), processorComponent);
 
             var components = new List<IBlockComponent>
diff --git a/moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearPumpTemplate.cs b/moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearPumpTemplate.cs
index 7071c31a6..2285e6006 100644
--- a/moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearPumpTemplate.cs
+++ b/moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearPumpTemplate.cs
@@ -38,7 +38,7 @@ namespace Game.Block.Factory.BlockTemplate
                 ? new PumpFluidOutputComponent(param.InnerTankCapacity, fluidConnector)
                 : new PumpFluidOutputComponent(componentStates, param.InnerTankCapacity, fluidConnector);
             
-            var pumpComponent = new GearPumpComponent(param, gearEnergyTransformer, outputComponent);
+            var pumpComponent = new GearPumpComponent(param, gearEnergyTransformer, outputComponent, blockPositionInfo);
 
             var components = new List<IBlockComponent>
             {
diff --git a/moorestech_server/Assets/Scripts/Game.Context/ServerContext.cs b/moorestech_server/Assets/Scripts/Game.Context/ServerContext.cs
index 260678d9b..758e3f62f 100644
--- a/moorestech_server/Assets/Scripts/Game.Context/ServerContext.cs
+++ b/moorestech_server/Assets/Scripts/Game.Context/ServerContext.cs
@@ -18,6 +18,7 @@ namespace Game.Context
         
         public static IWorldBlockDatastore WorldBlockDatastore { get; private set; }
         public static IMapVeinDatastore MapVeinDatastore { get; private set; }
+        public static IFluidMapVeinDatastore FluidMapVeinDatastore { get; private set; }
         public static IMapObjectDatastore MapObjectDatastore { get; private set; }
         
         public static IWorldBlockUpdateEvent WorldBlockUpdateEvent { get; private set; }
@@ -39,6 +40,7 @@ namespace Game.Context
             BlockFactory = initializeServiceProvider.GetService<IBlockFactory>();
             WorldBlockDatastore = initializeServiceProvider.GetService<IWorldBlockDatastore>();
             MapVeinDatastore = initializeServiceProvider.GetService<IMapVeinDatastore>();
+            FluidMapVeinDatastore = initializeServiceProvider.GetService<IFluidMapVeinDatastore>();
             WorldBlockUpdateEvent = initializeServiceProvider.GetService<IWorldBlockUpdateEvent>();
             BlockOpenableInventoryUpdateEvent = initializeServiceProvider.GetService<IBlockOpenableInventoryUpdateEvent>();
             MapObjectDatastore = initializeServiceProvider.GetService<IMapObjectDatastore>();
diff --git a/moorestech_server/Assets/Scripts/Game.Map.Interface/Json/MapInfoJson.cs b/moorestech_server/Assets/Scripts/Game.Map.Interface/Json/MapInfoJson.cs
index 533758f1e..0f1957360 100644
--- a/moorestech_server/Assets/Scripts/Game.Map.Interface/Json/MapInfoJson.cs
+++ b/moorestech_server/Assets/Scripts/Game.Map.Interface/Json/MapInfoJson.cs
@@ -10,6 +10,7 @@ namespace Game.Map.Interface.Json
         [JsonProperty("defaultSpawnPoint")] public SpawnPointJson DefaultSpawnPointJson;
         [JsonProperty("mapObjects")] public List<MapObjectInfoJson> MapObjects;
         [JsonProperty("mapVeins")] public List<MapVeinInfoJson> MapVeins;
+        [JsonProperty("fluidVeins")] public List<FluidVeinInfoJson> FluidVeins;
     }
     
     public class MapObjectInfoJson
@@ -41,6 +42,22 @@ namespace Game.Map.Interface.Json
         [JsonProperty("maxZ")] public int MaxZ;
     }
     
+    public class FluidVeinInfoJson
+    {
+        [JsonProperty("veinFluidGuid")] public string VeinFluidGuidStr;
+        [JsonIgnore] public Guid VeinFluidGuid => Guid.Parse(VeinFluidGuidStr);
+
+        [JsonIgnore] public Vector3Int MinPosition => new(MinX, MinY, MinZ);
+        [JsonProperty("minX")] public int MinX;
+        [JsonProperty("minY")] public int MinY;
+        [JsonProperty("minZ")] public int MinZ;
+
+        [JsonIgnore] public Vector3Int MaxPosition => new(MaxX, MaxY, MaxZ);
+        [JsonProperty("maxX")] public int MaxX;
+        [JsonProperty("maxY")] public int MaxY;
+        [JsonProperty("maxZ")] public int MaxZ;
+    }
+
     public class SpawnPointJson
     {
         [JsonProperty("x")] public float X;
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
diff --git a/moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs b/moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs
index 72a22ca2e..123d12107 100644
--- a/moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs
+++ b/moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs
@@ -95,21 +95,22 @@ namespace Server.Boot
 
             initializerCollection.AddSingleton<IWorldBlockDatastore, WorldBlockDatastore>();
             initializerCollection.AddSingleton<IWorldBlockUpdateEvent, WorldBlockUpdateEvent>();
-            initializerCollection.AddSingleton<IBlockOpenableInventoryUpdateEvent, BlockOpenableInventoryUpdateEvent>();
-            initializerCollection.AddSingleton<GearNetworkDatastore>();
-            initializerCollection.AddSingleton<RailGraphDatastore>();
-            initializerCollection.AddSingleton<IRailGraphDatastore>(provider => provider.GetService<RailGraphDatastore>());
-            initializerCollection.AddSingleton<TrainUnitDatastore>();
-            initializerCollection.AddSingleton<ITrainUnitMutationDatastore>(provider => provider.GetService<TrainUnitDatastore>());
-            initializerCollection.AddSingleton<ITrainUnitLookupDatastore>(provider => provider.GetService<TrainUnitDatastore>());
-            initializerCollection.AddSingleton<TrainDiagramManager>();
-            initializerCollection.AddSingleton<TrainRailPositionManager>();
-            initializerCollection.AddSingleton<IRailGraphNodeRemovalListener>(provider => provider.GetService<TrainDiagramManager>());
-            initializerCollection.AddSingleton<IRailGraphNodeRemovalListener>(provider => provider.GetService<TrainRailPositionManager>());
+            initializerCollection.AddSingleton<IBlockOpenableInventoryUpdateEvent, BlockOpenableInventoryUpdateEvent>();
+            initializerCollection.AddSingleton<GearNetworkDatastore>();
+            initializerCollection.AddSingleton<RailGraphDatastore>();
+            initializerCollection.AddSingleton<IRailGraphDatastore>(provider => provider.GetService<RailGraphDatastore>());
+            initializerCollection.AddSingleton<TrainUnitDatastore>();
+            initializerCollection.AddSingleton<ITrainUnitMutationDatastore>(provider => provider.GetService<TrainUnitDatastore>());
+            initializerCollection.AddSingleton<ITrainUnitLookupDatastore>(provider => provider.GetService<TrainUnitDatastore>());
+            initializerCollection.AddSingleton<TrainDiagramManager>();
+            initializerCollection.AddSingleton<TrainRailPositionManager>();
+            initializerCollection.AddSingleton<IRailGraphNodeRemovalListener>(provider => provider.GetService<TrainDiagramManager>());
+            initializerCollection.AddSingleton<IRailGraphNodeRemovalListener>(provider => provider.GetService<TrainRailPositionManager>());
 
             var mapPath = Path.Combine(options.ServerDataDirectory, "map", "map.json");
             initializerCollection.AddSingleton(JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(mapPath)));
             initializerCollection.AddSingleton<IMapVeinDatastore, MapVeinDatastore>();
+            initializerCollection.AddSingleton<IFluidMapVeinDatastore, FluidMapVeinDatastore>();
             initializerCollection.AddSingleton<IMapObjectDatastore, MapObjectDatastore>();
             initializerCollection.AddSingleton<IMapObjectFactory, MapObjectFactory>();
 
@@ -127,22 +128,22 @@ namespace Server.Boot
             services.AddSingleton<IInventorySubscriptionStore, InventorySubscriptionStore>();
             services.AddSingleton<IWorldEnergySegmentDatastore<EnergySegment>, WorldEnergySegmentDatastore<EnergySegment>>();
             services.AddSingleton<MaxElectricPoleMachineConnectionRange, MaxElectricPoleMachineConnectionRange>();
-            services.AddSingleton<IEntitiesDatastore, EntitiesDatastore>();
-            services.AddSingleton<IEntityFactory, EntityFactory>(); // TODO これを削除してContext側に加える？
-            var railGraphDatastore = initializerProvider.GetService<RailGraphDatastore>();
-            var trainUnitDatastore = initializerProvider.GetService<TrainUnitDatastore>();
-            services.AddSingleton(initializerProvider.GetService<GearNetworkDatastore>());
-            services.AddSingleton(railGraphDatastore);
-            services.AddSingleton<IRailGraphDatastore>(railGraphDatastore);
-            services.AddSingleton<IRailGraphProvider>(railGraphDatastore);
-            services.AddSingleton(trainUnitDatastore);
-            services.AddSingleton<ITrainUnitMutationDatastore>(trainUnitDatastore);
-            services.AddSingleton<ITrainUnitLookupDatastore>(trainUnitDatastore);
-            services.AddSingleton<RailConnectionCommandHandler>();
-            services.AddSingleton(initializerProvider.GetService<TrainDiagramManager>());
-            services.AddSingleton(initializerProvider.GetService<TrainRailPositionManager>());
-            services.AddSingleton<IRailGraphNodeRemovalListener>(initializerProvider.GetService<TrainDiagramManager>());
-            services.AddSingleton<IRailGraphNodeRemovalListener>(initializerProvider.GetService<TrainRailPositionManager>());
+            services.AddSingleton<IEntitiesDatastore, EntitiesDatastore>();
+            services.AddSingleton<IEntityFactory, EntityFactory>(); // TODO これを削除してContext側に加える？
+            var railGraphDatastore = initializerProvider.GetService<RailGraphDatastore>();
+            var trainUnitDatastore = initializerProvider.GetService<TrainUnitDatastore>();
+            services.AddSingleton(initializerProvider.GetService<GearNetworkDatastore>());
+            services.AddSingleton(railGraphDatastore);
+            services.AddSingleton<IRailGraphDatastore>(railGraphDatastore);
+            services.AddSingleton<IRailGraphProvider>(railGraphDatastore);
+            services.AddSingleton(trainUnitDatastore);
+            services.AddSingleton<ITrainUnitMutationDatastore>(trainUnitDatastore);
+            services.AddSingleton<ITrainUnitLookupDatastore>(trainUnitDatastore);
+            services.AddSingleton<RailConnectionCommandHandler>();
+            services.AddSingleton(initializerProvider.GetService<TrainDiagramManager>());
+            services.AddSingleton(initializerProvider.GetService<TrainRailPositionManager>());
+            services.AddSingleton<IRailGraphNodeRemovalListener>(initializerProvider.GetService<TrainDiagramManager>());
+            services.AddSingleton<IRailGraphNodeRemovalListener>(initializerProvider.GetService<TrainRailPositionManager>());
 
             services.AddSingleton<IGameUnlockStateDataController, GameUnlockStateDataController>();
             services.AddSingleton<CraftTreeManager>();
diff --git a/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/PumpFluidVeinTest.cs b/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/PumpFluidVeinTest.cs
new file mode 100644
index 000000000..bc18e21fb
--- /dev/null
+++ b/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/PumpFluidVeinTest.cs
@@ -0,0 +1,97 @@
+using System;
+using Core.Master;
+using Core.Update;
+using Game.Block.Blocks.Pump;
+using Game.Block.Interface;
+using Game.Block.Interface.Component;
+using Game.Block.Interface.Extension;
+using Game.Context;
+using Game.EnergySystem;
+using NUnit.Framework;
+using Server.Boot;
+using Tests.Module;
+using Tests.Module.TestMod;
+using UnityEngine;
+
+namespace Tests.CombinedTest.Core
+{
+    /// <summary>
+    /// 液体マップ鉱脈（FluidMapVein）の上に置かれたポンプだけが液体を生成することを検証する。
+    /// Verifies that pumps only generate fluid when placed over a registered FluidMapVein.
+    /// </summary>
+    public class PumpFluidVeinTest
+    {
+        // ForUnitTestModの map.json で定義された FluidVein 座標
+        // Coordinates of FluidVein defined in ForUnitTestMod map.json
+        private static readonly Vector3Int WaterVeinPos = new(10, 0, 0);
+        private static readonly Vector3Int SteamVeinPos = new(20, 0, 0);
+        private static readonly Vector3Int NoVeinPos = new(30, 0, 0);
+
+        private static readonly Guid WaterFluidGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
+
+        // ポンプ位置にWater Veinあり、マスタも一致 → 内部タンクに水が貯まる
+        // Vein matches master entry → water accumulates
+        [Test]
+        public void PumpOnMatchingFluidVein_GeneratesFluid()
+        {
+            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+
+            var pump = PlacePoweredPump(WaterVeinPos);
+
+            // 数tick待って内部タンクに液体が溜まることを確認
+            // Wait several ticks and verify fluid accumulation
+            for (var i = 0; i < 10; i++) GameUpdater.RunFrames(1);
+
+            var inventory = pump.GetComponent<PumpFluidOutputComponent>().GetFluidInventory();
+            Assert.AreEqual(1, inventory.Count, "内部タンクに液体が1種類入っているはず");
+            Assert.AreEqual(MasterHolder.FluidMaster.GetFluidId(WaterFluidGuid), inventory[0].FluidId);
+            Assert.Greater(inventory[0].Amount, 0);
+        }
+
+        // ポンプ位置に Vein が無い → 何も生成されない
+        // No vein at position → no generation
+        [Test]
+        public void PumpOutsideFluidVein_GeneratesNothing()
+        {
+            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+
+            var pump = PlacePoweredPump(NoVeinPos);
+
+            for (var i = 0; i < 10; i++) GameUpdater.RunFrames(1);
+
+            var inventory = pump.GetComponent<PumpFluidOutputComponent>().GetFluidInventory();
+            Assert.AreEqual(0, inventory.Count, "Vein無しの位置では液体は生成されないはず");
+        }
+
+        // Vein は存在するがポンプのマスタ generateFluid に含まれない液体 → 生成されない
+        // Vein exists but its fluid is not in pump master → no generation
+        [Test]
+        public void PumpOnMismatchedFluidVein_GeneratesNothing()
+        {
+            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+
+            // TestElectricPump は Water だけを generateFluid に持つので Steam Vein 上では生成されない
+            // TestElectricPump only has Water in its generateFluid table
+            var pump = PlacePoweredPump(SteamVeinPos);
+
+            for (var i = 0; i < 10; i++) GameUpdater.RunFrames(1);
+
+            var inventory = pump.GetComponent<PumpFluidOutputComponent>().GetFluidInventory();
+            Assert.AreEqual(0, inventory.Count, "マスタに一致するfluidGuidが無ければ生成されないはず");
+        }
+
+        private static IBlock PlacePoweredPump(Vector3Int pos)
+        {
+            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
+            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPump, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pump);
+
+            // 電力を十分に供給して powerRate=1.0 にする
+            // Supply enough power so powerRate = 1.0
+            var segment = new EnergySegment();
+            segment.AddEnergyConsumer(pump.GetComponent<IElectricConsumer>());
+            segment.AddGenerator(new TestElectricGenerator(new ElectricPower(10000), new BlockInstanceId(10)));
+
+            return pump;
+        }
+    }
+}
```
