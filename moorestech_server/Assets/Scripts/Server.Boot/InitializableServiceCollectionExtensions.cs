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
        public static void AddInitializableForwarding(this IServiceCollection services)
        {
            ValidatePhaseInterfaceImplemented();
            ForwardTo(typeof(IBootInitializable));
            ForwardTo(typeof(IPostLoadInitializable));

            #region Internal

            void ValidatePhaseInterfaceImplemented()
            {
                //基底IAutoInstantiatedの直接実装は生成もLoadもされないため、起動時に検出して失敗させる
                // Direct IAutoInstantiated implementations are never created nor loaded, so fail fast at boot.
                var violations = services
                    .Where(descriptor => !descriptor.IsKeyedService)
                    .Select(descriptor => descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType() ?? descriptor.ServiceType)
                    .Where(type => type.IsClass && typeof(IAutoInstantiated).IsAssignableFrom(type))
                    .Where(type => !typeof(IBootInitializable).IsAssignableFrom(type) && !typeof(IPostLoadInitializable).IsAssignableFrom(type))
                    .Distinct()
                    .ToList();

                if (violations.Count != 0)
                {
                    var typeNames = string.Join(", ", violations.Select(type => type.Name));
                    throw new InvalidOperationException($"IAutoInstantiatedを直接実装せず、IBootInitializable / IPostLoadInitializableのどちらかを実装してください: {typeNames}");
                }
            }

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
