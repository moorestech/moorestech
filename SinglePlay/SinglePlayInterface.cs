using Game.Crafting.Interface;
using Microsoft.Extensions.DependencyInjection;
using Server;

namespace SinglePlay
{
    public class SinglePlayInterface
    {
        public readonly ICraftingConfig CraftingConfig;

        public SinglePlayInterface(string configPath)
        {
            
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(configPath);

            CraftingConfig = serviceProvider.GetService<ICraftingConfig>();
        }
    }
}