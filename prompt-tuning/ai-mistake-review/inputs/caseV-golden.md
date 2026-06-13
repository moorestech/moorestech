# GOLDEN: 人間が実際に直した内容（＝AIのミス。捕捉できれば合格）
観点ID: V（規約違反: 不要な後方互換コード）

人間は FluidMapVeinDatastore のコンストラクタから「既存map.jsonとの互換のため」の `if (mapInfoJson.FluidVeins == null) return;` を削除した。本プロジェクトは後方互換不要・コア値へのnullチェック不要の方針。レビューはこの不要な後方互換null早期returnを指摘すべき。

```diff
commit 338630bfac6545bbf5ebbe9280dd1b77530e8acf
Author: sakastudio <sakastudio100@gmail.com>
Date:   Mon May 11 17:46:27 2026 +0900

    不要な互換を削除

diff --git a/moorestech_server/Assets/Scripts/Game.Map/FluidMapVeinDatastore.cs b/moorestech_server/Assets/Scripts/Game.Map/FluidMapVeinDatastore.cs
index 7d795c146..c9a551c57 100644
--- a/moorestech_server/Assets/Scripts/Game.Map/FluidMapVeinDatastore.cs
+++ b/moorestech_server/Assets/Scripts/Game.Map/FluidMapVeinDatastore.cs
@@ -12,10 +12,6 @@ namespace Game.Map
 
         public FluidMapVeinDatastore(MapInfoJson mapInfoJson)
         {
-            // 既存map.jsonとの互換のためnull許容
-            // Allow null for backward compatibility with legacy map.json
-            if (mapInfoJson.FluidVeins == null) return;
-
             foreach (var veinJson in mapInfoJson.FluidVeins)
             {
                 var fluidId = MasterHolder.FluidMaster.GetFluidIdOrNull(veinJson.VeinFluidGuid);
```
