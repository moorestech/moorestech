using Game.Context;
using Game.CraftChainer.BlockComponent.Template;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Game.CraftChainer
{
    public static class CraftChainerEntryPoint
    {
        public static void Entry()
        {
            var blockFactory = ServerContext.BlockFactory;    
            
            blockFactory.RegisterTemplateIBlock(BlockTypeConst.CraftChainerTransporter, new ChainerCrafterTemplate());
            blockFactory.RegisterTemplateIBlock(BlockTypeConst.CraftChainerCrafter, new ChainerTransporterTemplate());
            blockFactory.RegisterTemplateIBlock(BlockTypeConst.CraftChainerProviderChest, new ChainerProviderChestTemplate());
            blockFactory.RegisterTemplateIBlock(BlockTypeConst.CraftChainerMainComputer, new ChainerMainComputerTemplate());
        }
    }
}