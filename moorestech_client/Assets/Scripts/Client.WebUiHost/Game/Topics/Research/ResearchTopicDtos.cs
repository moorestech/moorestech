using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// research.tree の配信 DTO。表示可否は ui_state.current 側で判定するため open を持たない
    /// Payload DTO for research.tree; no open flag because visibility derives from ui_state.current
    /// </summary>
    public class ResearchTreeDto
    {
        public List<ResearchNodeDto> Nodes;
    }

    public class ResearchNodeDto
    {
        public string Guid;
        public string State;
        public int IconItemId;
        public ResearchPositionDto Position;
        public List<string> PrevGuids;
        public List<ResearchConsumeItemDto> ConsumeItems;
        public List<ResearchRewardItemDto> RewardItems;
        public List<int> UnlockItemRecipeViewItemIds;
        public List<ResearchUnlockBlockDto> UnlockBlocks;
        public List<ResearchUnlockMachineRecipeDto> UnlockMachineRecipes;
        public List<string> UnlockConnectToolGuids;
        public List<string> UnlockTrainCarGuids;
    }

    /// <summary>
    /// unlockBlock表示DTO
    /// - Icon: BlockId
    /// - 名前: Guid導出キー
    /// Display DTO for unlockBlock
    /// - icon: BlockId
    /// - name: Guid-derived key
    /// </summary>
    public class ResearchUnlockBlockDto
    {
        public int BlockId;
        public string BlockGuid;
    }

    /// <summary>
    /// unlockMachineRecipe表示DTO
    /// - 1レシピの出力アイテム/流体を保持
    /// Display DTO for unlockMachineRecipe
    /// - holds one recipe's item and fluid outputs
    /// </summary>
    public class ResearchUnlockMachineRecipeDto
    {
        public string RecipeGuid;
        public List<int> OutputItemIds;
        public List<ResearchUnlockFluidDto> OutputFluids;
    }

    public class ResearchUnlockFluidDto
    {
        public int FluidId;
        public string FluidGuid;
        public double Amount;
    }

    public class ResearchRewardItemDto
    {
        public int ItemId;
        public int Count;
    }

    public class ResearchPositionDto
    {
        public double X;
        public double Y;
    }

    public class ResearchConsumeItemDto
    {
        public int ItemId;
        public int Count;
    }
}
