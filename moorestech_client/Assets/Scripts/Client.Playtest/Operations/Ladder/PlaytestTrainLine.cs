using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.Input;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.UnlockState;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using TrainCarEntityObject = Client.Game.InGame.Train.View.Object.Core.TrainCarEntityObject;

namespace Client.Playtest.Operations
{
    // 駅 2 基と機関車を置いた列車線の準備結果
    // The prepared line of two stations and a locomotive
    public sealed class PlaytestTrainLineSetup
    {
        public readonly IBlock StationA;
        public readonly IBlock StationB;
        public readonly Vector3Int StationAPosition;
        public readonly Vector3Int StationBPosition;
        public readonly TrainUnit Train;

        public PlaytestTrainLineSetup(IBlock stationA, IBlock stationB, Vector3Int stationAPosition, Vector3Int stationBPosition, TrainUnit train)
        {
            StationA = stationA;
            StationB = stationB;
            StationAPosition = stationAPosition;
            StationBPosition = stationBPosition;
            Train = train;
        }
    }

    // ラダーの列車の前段（駅の結線・機関車の設置・燃料）をサーバー直で組み、駅と車両を F で開く。駅名は UI で付けるので付けない
    // Builds the ladder's train prelude server-side and opens stations and cars with F; station names are left for the UI
    public static class PlaytestTrainLine
    {
        private const string StationBlockName = "蒸気機関車駅";

        public static async UniTask<PlaytestTrainLineSetup> Build(PlaytestDriver p, Vector3Int stationAPosition, Vector3Int stationBPosition)
        {
            // スポーン付近の岩が駅の南端に重なり照準を遮るので、マップオブジェクトを消す（前例 train-timetable-via-ui）
            // A rock near the spawn blocks the aim at the station's south end, so hide map objects (as train-timetable-via-ui)
            Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);

            // 駅 2 基を南向きに置き、front と back の両側で A→B・B→A を結線する（ConnectNode は片方向。docs/train/TrainSystemNotes.md）
            // Place two south-facing stations and connect A->B and B->A on both the front and back sides (ConnectNode is one-way)
            await p.PlaceBlockDirect(StationBlockName, stationAPosition, BlockDirection.South);
            await p.PlaceBlockDirect(StationBlockName, stationBPosition, BlockDirection.South);
            await p.WaitBlockGameObject(stationAPosition);
            await p.WaitBlockGameObject(stationBPosition);
            var stationA = p.GetBlock(stationAPosition);
            var stationB = p.GetBlock(stationBPosition);
            var railsA = stationA.GetComponents<RailComponent>();
            var railsB = stationB.GetComponents<RailComponent>();
            p.Assert(railsA.Count == 2 && railsB.Count == 2, "駅は2本のRailComponentを持つ");
            await p.RunOnServer("駅2基を結線する", () =>
            {
                ExitOf(railsA).FrontNode.ConnectNode(EntryOf(railsB).FrontNode);
                ExitOf(railsB).FrontNode.ConnectNode(EntryOf(railsA).FrontNode);
                EntryOf(railsB).BackNode.ConnectNode(ExitOf(railsA).BackNode);
                EntryOf(railsA).BackNode.ConnectNode(ExitOf(railsB).BackNode);
            });

            // 機関車を解放して駅 A の back 側の停車位置へ置き、燃料を入れる（UI の停車駅は back 側で、反転せず北の B へ向かう）
            // Unlock a locomotive, place it back-side at station A's stop and fuel it (UI stops use the back side; it heads north to B)
            var clientUnlockState = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IGameUnlockStateData>();
            var carMaster = MasterHolder.TrainUnitMaster.Train.TrainCars.First(c => 0 < c.TractionForce);
            await p.RunOnServer("機関車を解放する", () => ServerContext.GetService<IGameUnlockStateDataController>().UnlockTrainCar(carMaster.TrainCarGuid));
            await p.Until(() => clientUnlockState.TrainCarUnlockStateInfos.TryGetValue(carMaster.TrainCarGuid, out var info) && info.IsUnlocked, 10f, "車両アンロック同期");
            foreach (var required in carMaster.RequiredItems) await p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(required.ItemGuid).Name, required.Count);
            var railPosition = new RailPosition(new List<IRailNode> { EntryOf(railsA).BackNode, ExitOf(railsA).BackNode }, TrainLengthConverter.ToRailUnits(carMaster.Length), 0);
            var placed = await ClientContext.VanillaApi.Response.Train.PlaceTrainOnRail(railPosition, carMaster.TrainCarGuid, CancellationToken.None);
            p.Assert(placed != null && placed.Success, $"車両設置成功 (failure={placed?.FailureType})");
            await p.Until(() => SpawnedCar() != null, 15f, "車両entity出現");
            p.SendCommand("addFuelToAllTrainCarsCommand");
            await p.WaitSeconds(1f);

            TrainUnit train = null;
            await p.RunOnServer("検証対象の列車を取得する", () => train = ServerContext.GetService<ITrainUnitLookupDatastore>().GetRegisteredTrains().First());
            return new PlaytestTrainLineSetup(stationA, stationB, stationAPosition, stationBPosition, train);

            #region Internal

            RailComponent ExitOf(List<RailComponent> rails) => rails.First(r => r.FrontNode.StationRef.NodeRole == StationNodeRole.Exit);
            RailComponent EntryOf(List<RailComponent> rails) => rails.First(r => r.FrontNode.StationRef.NodeRole == StationNodeRole.Entry);

            #endregion
        }

        public static async UniTask OpenCar(PlaytestDriver p)
        {
            // 駅の外の後端（南）側から F の届く 2m 以内で車両の上寄りを狙って開く（下端の HUD に照準が重なると走査が空になる）
            // Open the car aiming high from outside the station's rear (south) end within the 2m F reach (aiming over the bottom HUD empties the scan)
            var rear = SpawnedCar().GetComponentsInChildren<Collider>(true).OrderBy(collider => collider.bounds.min.z).First().bounds;
            p.WarpPlayer(new Vector3(rear.center.x, 33.5f, rear.min.z - 1.2f));
            await p.WaitSeconds(1f);
            await p.AimAt(new Vector3(rear.center.x, rear.max.y - 0.3f, rear.min.z + 0.3f));
            await p.WaitSeconds(0.5f);
            await p.PressInteract();
            await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        }

        public static async UniTask OpenStation(PlaytestDriver p, Vector3Int stationPosition)
        {
            // 駅の外の南側からコライダーへ照準を合わせて開く（前例 uitk-train-inventory の OpenStation。Alt でカーソルを外したまま狙う）
            // Open the station aiming at its collider from outside the south side (as uitk-train-inventory's OpenStation; aim with Alt held)
            var colliders = new List<Collider>();
            await p.Until(() => 0 < (colliders = ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(stationPosition).GetComponentsInChildren<Collider>(true)
                .Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && c.bounds.size != Vector3.zero).ToList()).Count, 10f, "駅のColliderが復帰する");
            var bounds = colliders[0].bounds;
            foreach (var collider in colliders) bounds.Encapsulate(collider.bounds);
            p.WarpPlayer(new Vector3(bounds.center.x, 33.5f, bounds.min.z - 1f));
            await UniTask.DelayFrame(10);
            SemanticInput.KeyDown(Key.LeftAlt);
            await UniTask.DelayFrame(2);
            await p.AimAt(new Vector3(bounds.center.x, 32.4f, bounds.min.z + 0.1f));
            await p.PressInteract();
            await p.WaitUiState(UIStateEnum.SubInventory, 10f);
            SemanticInput.KeyUp(Key.LeftAlt);
            await p.UntilUiElement("train-station-name-section", 10f);
        }

        private static TrainCarEntityObject SpawnedCar() => Object.FindObjectsByType<TrainCarEntityObject>(FindObjectsSortMode.None).FirstOrDefault();
    }
}
