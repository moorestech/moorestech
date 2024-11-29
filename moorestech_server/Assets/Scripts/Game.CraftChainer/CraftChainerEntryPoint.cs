using Game.Context;
using Game.CraftChainer.BlockComponent.Template;
using Game.CraftChainer.CraftNetwork;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Game.CraftChainer
{
    public static class CraftChainerEntryPoint
    {
        public static void Entry()
        {
            // block template register
            var blockFactory = ServerContext.BlockFactory;    
            blockFactory.RegisterTemplateIBlock(BlockTypeConst.CraftChainerCrafter, new CraftChainerCrafterTemplate());
            blockFactory.RegisterTemplateIBlock(BlockTypeConst.CraftChainerTransporter, new CraftChainerTransporterTemplate());
            blockFactory.RegisterTemplateIBlock(BlockTypeConst.CraftChainerProviderChest, new CraftChainerProviderChestTemplate());
            blockFactory.RegisterTemplateIBlock(BlockTypeConst.CraftChainerMainComputer, new CraftChainerMainComputerTemplate());
            
            // manager init
            new CraftChainerMainComputerManager();
        }
    }
}