using System;
using System.Diagnostics;
using Client.Common.Shutdown;
using Client.Network.Settings;
using Cysharp.Threading.Tasks;
using UniRx;
using VContainer.Unity;

namespace Client.Network.API
{
    public class VanillaApi : IInitializable
    {
        private readonly Process _localServerProcess;

        private readonly ServerCommunicator _serverCommunicator;
        public readonly VanillaApiEvent Event;
        public readonly VanillaApiWithResponse Response;
        public readonly VanillaApiSendOnly SendOnly;

        public VanillaApi(PacketExchangeManager packetExchangeManager, PacketSender packetSender, ServerCommunicator serverCommunicator, PlayerConnectionSetting playerConnectionSetting, Process localServerProcess)
        {
            _serverCommunicator = serverCommunicator;
            _localServerProcess = localServerProcess;

            Event = new VanillaApiEvent(packetExchangeManager, playerConnectionSetting);
            Response = new VanillaApiWithResponse(packetExchangeManager, playerConnectionSetting);
            SendOnly = new VanillaApiSendOnly(packetSender, playerConnectionSetting);
        }

        public IObservable<Unit> OnDisconnect => _serverCommunicator.OnDisconnect;

        public void Initialize()
        {
            // 終了パイプラインに Save ACK → ソケット切断 → ローカルプロセス kill を登録
            // Register save ACK, socket close, and local server kill into the shutdown pipeline
            ShutdownCoordinator.Register(ShutdownPhase.BeforeDisconnect, "VanillaApi.Save",
                async () => await Response.SaveAsync());
            ShutdownCoordinator.Register(ShutdownPhase.Disconnect, "VanillaApi.Close",
                () => { _serverCommunicator.Close(); return UniTask.CompletedTask; });
            ShutdownCoordinator.Register(ShutdownPhase.DisposeSubsystems, "VanillaApi.KillLocalServer",
                () => { _localServerProcess?.Kill(); return UniTask.CompletedTask; });
        }

        // Task 20 で BackToMainMenu 削除後に本メソッドも削除する
        // Will be removed together with BackToMainMenu in Task 20
        [System.Obsolete("Use ShutdownCoordinator.ShutdownAsync() instead. Removed in Task 20.")]
        public void Disconnect() { }
    }
}
