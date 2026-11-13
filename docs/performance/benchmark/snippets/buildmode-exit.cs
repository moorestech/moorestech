// 通常画面へ戻す
// Return to the normal game screen
using Client.Game.InGame.UI.UIState;
using UnityEngine;
Object.FindFirstObjectByType<UIStateControl>().RequestTransition(UIStateEnum.GameScreen);
return "exit requested frame=" + Time.frameCount;
