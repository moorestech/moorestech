using Core.ConfigJson;
using Core.Item.Interface;
using Core.Item.Config;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.RecipeConfig;
using Game.Context;
using Game.Crafting.Interface;
using Microsoft.Extensions.DependencyInjection;
using Server.Boot;

namespace ServerServiceProvider
{
    public class MoorestechServerServiceProvider
    {
        public readonly ServiceProvider ServiceProvider;
        
        public MoorestechServerServiceProvider(string serverDirectory)
        {
            (_, ServiceProvider) = new MoorestechServerDIContainerGenerator().Create(serverDirectory);
        }
        
        public T GetService<T>()
        {
            return ServiceProvider.GetService<T>();
        }
    }
}