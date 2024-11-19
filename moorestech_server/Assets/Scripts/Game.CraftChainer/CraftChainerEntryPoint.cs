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
            blockFactory.RegisterTemplateIBlock(BlockTypeConst.CraftChainerCrafter, new ChainerCrafterTemplate());
            blockFactory.RegisterTemplateIBlock(BlockTypeConst.CraftChainerTransporter, new ChainerTransporterTemplate());
            blockFactory.RegisterTemplateIBlock(BlockTypeConst.CraftChainerProviderChest, new ChainerProviderChestTemplate());
            blockFactory.RegisterTemplateIBlock(BlockTypeConst.CraftChainerMainComputer, new ChainerMainComputerTemplate());
            
            // manager init
            new CraftChainerManager();
        }
    }
}