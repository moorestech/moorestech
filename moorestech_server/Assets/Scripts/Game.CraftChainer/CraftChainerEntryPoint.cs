using Game.Context;
using Game.CraftChainer.BlockComponent.Template;
using Game.CraftChainer.CraftNetwork;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Game.CraftChainer
{
    /// <summary>
    /// NOTE: CraftChainerシステムはオミットします。README.mdを参照してください。
    /// Note: The CraftChainer system will be omitted. Please refer to README.md.
    /// 
    /// README PATH : moorestech_server/Assets/Scripts/Game.CraftChainer/README.md
    /// </summary>
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