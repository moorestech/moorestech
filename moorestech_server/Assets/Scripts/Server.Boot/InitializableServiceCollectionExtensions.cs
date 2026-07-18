using System;
using System.Linq;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Boot
{
    public static class InitializableServiceCollectionExtensions
    {
        //登録済みサービスからマーカーinterface実装を探し、マーカー型へ同一インスタンスを転送登録する
        // Find marker-interface implementations among registered services and forward the same instances under the marker type.
        //レシーバーは具象型でAddSingletonすること（非マーカーinterface型＋ファクトリ登録は検出から漏れる）
        // Register receivers by concrete type via AddSingleton; factory registrations under a non-marker interface type escape detection.
        public static void AddInitializableForwarding(this IServiceCollection services)
        {
            ForwardTo(typeof(IBootInitializable));
            ForwardTo(typeof(IPostLoadInitializable));

            #region Internal

            void ForwardTo(Type markerType)
            {
                var serviceTypes = services
                    .Where(descriptor => !descriptor.IsKeyedService && descriptor.ServiceType != markerType)
                    .Where(descriptor => markerType.IsAssignableFrom(descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType() ?? descriptor.ServiceType))
                    .Select(descriptor => descriptor.ServiceType)
                    .Distinct()
                    .ToList();

                foreach (var serviceType in serviceTypes)
                {
                    services.AddSingleton(markerType, provider => provider.GetRequiredService(serviceType));
                }
            }

            #endregion
        }
    }
}
