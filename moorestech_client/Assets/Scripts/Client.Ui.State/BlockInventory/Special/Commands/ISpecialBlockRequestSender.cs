using Client.Ui.State.BlockInventory.Logic.Special;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    public enum SpecialBlockRequestOutcomeKind
    {
        Accepted,
        Stale,
        Failed,
    }

    // 送信の結果。古い操作と失敗はコードを持つ
    // Send outcome; stale and failed outcomes carry a code
    public readonly struct SpecialBlockRequestOutcome
    {
        public SpecialBlockRequestOutcomeKind Kind { get; }
        public string WireCode { get; }

        private SpecialBlockRequestOutcome(SpecialBlockRequestOutcomeKind kind, string wireCode)
        {
            Kind = kind;
            WireCode = wireCode;
        }

        public static SpecialBlockRequestOutcome Accepted => new(SpecialBlockRequestOutcomeKind.Accepted, "");
        public static SpecialBlockRequestOutcome Stale(string wireCode) => new(SpecialBlockRequestOutcomeKind.Stale, wireCode);
        public static SpecialBlockRequestOutcome Failed(string wireCode) => new(SpecialBlockRequestOutcomeKind.Failed, wireCode);
    }

    // サーバーへの送信の口（テストは記録係に差し替える）
    // Port that sends to the server; tests swap in a recorder
    public interface ISpecialBlockRequestSender
    {
        UniTask<SpecialBlockRequestOutcome> SetFilterMode(Vector3Int position, BlockInstanceId instanceId, int directionIndex, FilterSplitterMode mode);
        UniTask<SpecialBlockRequestOutcome> SetFilterItem(Vector3Int position, BlockInstanceId instanceId, int directionIndex, int slotIndex, ItemId itemId);
        UniTask<SpecialBlockRequestOutcome> SetElectricToGearMode(Vector3Int position, int modeIndex);
        UniTask<SpecialBlockRequestOutcome> ConnectSpaceElevator(Vector3Int position, BlockInstanceId instanceId);
        UniTask<SpecialBlockRequestOutcome> SendRocketCommand(Vector3Int position, RocketCommand command, BlockInstanceId instanceId);
    }
}
