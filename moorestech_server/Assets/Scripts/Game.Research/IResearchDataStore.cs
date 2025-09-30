using System;
using System.Collections.Generic;

namespace Game.Research
{
    public interface IResearchDataStore
    {
        bool IsResearchCompleted(Guid researchGuid);
        bool CompleteResearch(Guid researchGuid, int playerId);
        Dictionary<Guid, ResearchNodeState> GetResearchNodeStates(int playerId);
        ResearchSaveJsonObject GetSaveJsonObject();
        void LoadResearchData(ResearchSaveJsonObject saveData);
    }
    
    public class ResearchSaveJsonObject
    {
        public List<string> CompletedResearchGuids { get; set; }
        
        public ResearchSaveJsonObject()
        {
            CompletedResearchGuids = new List<string>();
        }
    }
    
    public enum ResearchNodeState
    {
        Completed,
        Researchable,
        
        UnresearchableAllReasons,
        UnresearchableNotEnoughItem,
        UnresearchableNotEnoughPreNode,
    }
}
