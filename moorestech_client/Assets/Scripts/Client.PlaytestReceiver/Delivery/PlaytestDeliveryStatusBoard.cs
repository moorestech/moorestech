using UniRx;
using UnityEngine;

namespace Client.PlaytestReceiver.Delivery
{
    public static class PlaytestDeliveryStatusBoard
    {
        private static readonly ReactiveProperty<PlaytestDeliveryStatus> _current = new(new PlaytestDeliveryPending(0));
        public static IReadOnlyReactiveProperty<PlaytestDeliveryStatus> Current => _current;
        internal static void Set(PlaytestDeliveryStatus status) { _current.Value = status; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetOnPlayMode() { Set(new PlaytestDeliveryPending(0)); }
    }
}
