using Client.Game.Common;
using Client.Game.InGame.BeltSegment.Model;
using Client.Game.InGame.BeltSegment.Network;
using Client.Game.InGame.Context;
using UnityEngine;
using VContainer;
using VContainer.Unity;
namespace Client.Game.InGame.BeltSegment
{
    public static class BeltWorldRegistration
    {
        public static void Register(IContainerBuilder builder)
        {
            // Starterには登録入口だけを公開し、CPU/GPU所有者はassembly内に保つ。
            // Expose only feature registration to Starter, retaining CPU/GPU ownership inside this assembly.
            var world = new ClientBeltWorld(Resources.Load<ComputeShader>("BeltSegment/BeltGpuReplay"));
            var recovery = new BeltWorldRecovery(world, new BeltSnapshotRequester(ClientContext.VanillaApi.Response), Application.exitCancellationToken);
            builder.RegisterInstance(world);
            builder.RegisterInstance(recovery);
            builder.RegisterEntryPoint<BeltWorldEventHandler>().AsSelf().As<IInitialEventApplyWaitTarget>()
                .WithParameter(Application.exitCancellationToken);
        }
    }
}
