using System;
using System.Linq;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Boot
{
    public static class EventReceiverServiceCollectionExtensions
    {
        //登録済みサービスからマーカーinterface実装を探し、マーカー型へ同一インスタンスを転送登録する
        // Find marker-interface implementations among registered services and forward the same instances under the marker type.
        public static void AddEventReceiverForwarding(this IServiceCollection services)
        {
            ForwardTo(typeof(IEventReceiver));
            ForwardTo(typeof(IPostLoadEventReceiver));

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
