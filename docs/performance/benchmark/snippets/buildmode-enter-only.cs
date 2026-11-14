// 設置モードへ本番の経路で入るだけ（半径の判定を数フレーム回してから buildmode-proposed-entry-cost.cs を実行する）
// Only enter build mode through the production path (let the radius evaluation run a few frames, then run buildmode-proposed-entry-cost.cs)
using System.Reflection;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Core.Master;
using UnityEngine;
using VContainer;

var control = Object.FindFirstObjectByType<UIStateControl>();
if (control == null || control.CurrentState != UIStateEnum.GameScreen) return "FAIL not in GameScreen: " + control?.CurrentState;
var blockId = MasterHolder.BlockMaster.GetBlockAllIds()[0];
var target = new BlockPlacementTarget(MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid, null);
ClientDIContext.DIContainer.DIContainerResolver.Resolve<PlaceSystemStateController>().SetTarget(target, PlacementOrigin.NonHotbar);
control.RequestTransition(UIStateEnum.PlaceBlock);
typeof(UIStateControl).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(control, null);
return "state=" + control.CurrentState;
