# AIコミット diff (base->ai) — レビュー対象
```diff
commit b117406b595a2eb7bfd26fcfc4d93277ee896993
Author: sakastudio <sakastudio100@gmail.com>
Date:   Sun May 24 01:17:50 2026 +0900

    列車乗車入力をキー押下時のみ送信するよう変更
    
    毎フレームの入力送信を、W/A/S/Dいずれかが押された瞬間のみに限定して
    不要な通信を削減。
    
    Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>

diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/TrainHUDScreenState.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/TrainHUDScreenState.cs
index 0da7c92f8..f6405e471 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/TrainHUDScreenState.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/TrainHUDScreenState.cs
@@ -137,12 +137,18 @@ namespace Client.Game.InGame.UI.UIState.State
                 SendDismountRequestAsync().Forget(LogRpcFault);
             }
             
-            ClientContext.VanillaApi.SendOnly.SendTrainCarRidingInput(
-                _rideContext.CurrentCarId,
-                UnityEngine.Input.GetKey(KeyCode.W),
-                UnityEngine.Input.GetKey(KeyCode.A),
-                UnityEngine.Input.GetKey(KeyCode.S),
-                UnityEngine.Input.GetKey(KeyCode.D));
+            
+            var isInput = UnityEngine.Input.GetKeyDown(KeyCode.W) || UnityEngine.Input.GetKeyDown(KeyCode.A) || UnityEngine.Input.GetKeyDown(KeyCode.S) || UnityEngine.Input.GetKeyDown(KeyCode.D);
+            if (isInput)
+            {
+                ClientContext.VanillaApi.SendOnly.SendTrainCarRidingInput(
+                    _rideContext.CurrentCarId,
+                    UnityEngine.Input.GetKey(KeyCode.W),
+                    UnityEngine.Input.GetKey(KeyCode.A),
+                    UnityEngine.Input.GetKey(KeyCode.S),
+                    UnityEngine.Input.GetKey(KeyCode.D));
+            }
+            
 
             return null;
 
```
