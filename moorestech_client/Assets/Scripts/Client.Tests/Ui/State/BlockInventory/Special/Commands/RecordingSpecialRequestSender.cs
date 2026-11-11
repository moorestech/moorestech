using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Commands;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 送信を文字列で記録し結果を返す
    /// Records sends as strings and returns the chosen outcome
    /// </summary>
    public sealed class RecordingSpecialRequestSender : ISpecialBlockRequestSender
    {
        private SpecialBlockRequestOutcome _next = SpecialBlockRequestOutcome.Accepted;

        public List<string> Sent { get; } = new();

        public void SetNextOutcome(SpecialBlockRequestOutcome outcome)
        {
            _next = outcome;
        }

        public UniTask<SpecialBlockRequestOutcome> SetFilterMode(Vector3Int position, BlockInstanceId instanceId, int directionIndex, FilterSplitterMode mode) => Record($"filter_mode {position} {instanceId.AsPrimitive()} {directionIndex} {mode}");
        public UniTask<SpecialBlockRequestOutcome> SetFilterItem(Vector3Int position, BlockInstanceId instanceId, int directionIndex, int slotIndex, ItemId itemId) => Record($"filter_item {position} {instanceId.AsPrimitive()} {directionIndex} {slotIndex} {itemId.AsPrimitive()}");
        public UniTask<SpecialBlockRequestOutcome> SetElectricToGearMode(Vector3Int position, int modeIndex) => Record($"electric_to_gear {position} {modeIndex}");
        public UniTask<SpecialBlockRequestOutcome> ConnectSpaceElevator(Vector3Int position, BlockInstanceId instanceId) => Record($"connect {position} {instanceId.AsPrimitive()}");
        public UniTask<SpecialBlockRequestOutcome> SendRocketCommand(Vector3Int position, RocketCommand command, BlockInstanceId instanceId) => Record($"rocket {position} {command} {instanceId.AsPrimitive()}");

        private UniTask<SpecialBlockRequestOutcome> Record(string line)
        {
            Sent.Add(line);
            return UniTask.FromResult(_next);
        }
    }
}
