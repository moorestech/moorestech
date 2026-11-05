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
        public List<int> UnlockModuleSlotStackLimits;
        public List<ResearchUnlockCraftCapDto> UnlockLevelInheritanceCraftCaps;
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
        // この要求に充てられるアイテムID（基準なら上位の変種も下位から）。Web の不足表示がサーバーの充足と同じ規則で数えるため
        // Item IDs that can serve this requirement (higher variants too when it is a base, lowest first), so the Web shortage display counts by the server's rule
        public List<int> AcceptedItemIds;
    }

    /// <summary>
    /// 製作上限の解放表示
    /// - ItemId: 継承先ファミリーの基準アイテム
    /// - Level: 解放後の製作上限
    /// Craft-cap unlock display
    /// - ItemId: the target family's base item
    /// - Level: the craft cap after the unlock
    /// </summary>
    public class ResearchUnlockCraftCapDto
    {
        public int ItemId;
        public int Level;
    }
}
