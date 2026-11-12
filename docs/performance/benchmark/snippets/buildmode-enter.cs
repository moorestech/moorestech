// Profiler を開始してから、キー入力を介さず本番の遷移要求で設置モードへ入る
// Start the profiler, then enter build mode through the production transition request without key input
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Core.Master;
using UnityEditorInternal;
using UnityEngine;
using VContainer;
ProfilerDriver.ClearAllFrames();
ProfilerDriver.profileEditor = false;
ProfilerDriver.deepProfiling = false;
ProfilerDriver.enabled = true;
var blockId = MasterHolder.BlockMaster.GetBlockAllIds()[0];
var target = new BlockPlacementTarget(MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid, null);
ClientDIContext.DIContainer.DIContainerResolver.Resolve<PlaceSystemStateController>().SetTarget(target, PlacementOrigin.NonHotbar);
Object.FindFirstObjectByType<UIStateControl>().RequestTransition(UIStateEnum.PlaceBlock);
return "entered request frame=" + Time.frameCount;
