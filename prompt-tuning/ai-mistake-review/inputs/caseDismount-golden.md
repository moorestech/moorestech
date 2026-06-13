# GOLDEN: 人間が直した内容（捕捉できれば合格）
観点ID: S（Unity API/プロパティ・入力検出の取り違え）

(1) 降車位置で marker.Position を使用 → marker.transform.position が正しい。 (2) 列車操作の継続入力を GetKeyDown(押下1フレームのみ)で取得 → GetKey(押下継続) が正しい。レビューはこの2つのAPI取り違えを指摘すべき。

```diff
commit 72af4f1d1b8cf1a055c282b728d94f5668296264
Author: sakastudio <sakastudio100@gmail.com>
Date:   Sun May 24 12:41:36 2026 +0900

    列車のDismount修正

diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/Player/StateController/State/RidingPlayerState.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/Player/StateController/State/RidingPlayerState.cs
index 7a94055fc..d7141cccf 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/Player/StateController/State/RidingPlayerState.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/Player/StateController/State/RidingPlayerState.cs
@@ -100,7 +100,7 @@ namespace Client.Game.InGame.Player.StateController.State
                 {
                     var marker = entity.GetComponentInChildren<TrainCarDismountPoint>(true);
                     
-                    if (marker != null) return marker.Position;
+                    if (marker != null) return marker.transform.position;
                     return entity.transform.position;
                 }
 
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/TrainHUDScreenState.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/TrainHUDScreenState.cs
index f6405e471..98fe4a09a 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/TrainHUDScreenState.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/TrainHUDScreenState.cs
@@ -138,7 +138,7 @@ namespace Client.Game.InGame.UI.UIState.State
             }
             
             
-            var isInput = UnityEngine.Input.GetKeyDown(KeyCode.W) || UnityEngine.Input.GetKeyDown(KeyCode.A) || UnityEngine.Input.GetKeyDown(KeyCode.S) || UnityEngine.Input.GetKeyDown(KeyCode.D);
+            var isInput = UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.D);
             if (isInput)
             {
                 ClientContext.VanillaApi.SendOnly.SendTrainCarRidingInput(
```
