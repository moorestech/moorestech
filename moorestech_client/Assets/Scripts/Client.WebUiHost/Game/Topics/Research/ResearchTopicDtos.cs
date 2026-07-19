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
        public string Name;
        public string Description;
        public string State;
        public int IconItemId;
        public ResearchPositionDto Position;
        public List<string> PrevGuids;
        public List<ResearchConsumeItemDto> ConsumeItems;
        public List<ResearchRewardItemDto> RewardItems;
        public List<int> UnlockItemIds;
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
