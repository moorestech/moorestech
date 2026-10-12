using System;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    // サーバーの拒否と通信失敗を区別する
    // Distinguishes server rejection from request failure
    public enum BlueprintCreateFailure
    {
        None,
        RequestFailed,
        NotUnlocked,
        InvalidName,
        EmptyArea,
        InvalidRequest,
        Unknown,
    }

    public readonly struct BlueprintCreateResult
    {
        public readonly BlueprintCreateFailure Failure;
        public readonly Guid BlueprintGuid;
        public bool Success => Failure == BlueprintCreateFailure.None;

        private BlueprintCreateResult(BlueprintCreateFailure failure, Guid blueprintGuid)
        {
            Failure = failure;
            BlueprintGuid = blueprintGuid;
        }

        public static BlueprintCreateResult Succeeded(Guid blueprintGuid) => new(BlueprintCreateFailure.None, blueprintGuid);
        public static BlueprintCreateResult RequestFailed() => new(BlueprintCreateFailure.RequestFailed, Guid.Empty);

        public static BlueprintCreateResult Rejected(BlueprintFailureReason reason)
        {
            var failure = reason switch
            {
                BlueprintFailureReason.NotUnlocked => BlueprintCreateFailure.NotUnlocked,
                BlueprintFailureReason.InvalidName => BlueprintCreateFailure.InvalidName,
                BlueprintFailureReason.EmptyArea => BlueprintCreateFailure.EmptyArea,
                BlueprintFailureReason.InvalidRequest => BlueprintCreateFailure.InvalidRequest,
                _ => BlueprintCreateFailure.Unknown,
            };
            return new BlueprintCreateResult(failure, Guid.Empty);
        }
    }
}
