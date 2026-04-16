using Client.Game.InGame.Context;
using IngameDebugConsole;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.DebugSystem
{
    public static class TrainManualDebugState
    {
        public static long SelectedTrainCarId { get; private set; } = -1;
        public static bool IsRiding { get; private set; }

        public static void SelectTrainCar(long trainCarId)
        {
            SelectedTrainCarId = trainCarId;
            IsRiding = true;
        }

        public static void ClearSelection()
        {
            SelectedTrainCarId = -1;
            IsRiding = false;
        }
    }

    public static class TrainManualDebugCommands
    {
        [ConsoleMethod("train.manual.select", "Select a train car for manual control", "trainCarId")]
        public static void Select(long trainCarId)
        {
            TrainManualDebugState.SelectTrainCar(trainCarId);
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            ClientContext.VanillaApi.SendOnly.SendCommand($"{SendCommandProtocol.TrainManualSelectCarCommand} {playerId} {trainCarId}");
            Debug.Log($"[TrainManual] Selected train car: {trainCarId}");
        }

        [ConsoleMethod("train.manual.clear", "Clear the current manual-control train car")]
        public static void Clear()
        {
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            ClientContext.VanillaApi.SendOnly.SendCommand($"{SendCommandProtocol.TrainManualClearCarCommand} {playerId}");
            TrainManualDebugState.ClearSelection();
            Debug.Log("[TrainManual] Cleared selected train car.");
        }

        [ConsoleMethod("train.manual.status", "Print the current manual-control selection")]
        public static void Status()
        {
            Debug.Log($"[TrainManual] Riding:{TrainManualDebugState.IsRiding} SelectedCar:{TrainManualDebugState.SelectedTrainCarId}");
        }
    }
}
