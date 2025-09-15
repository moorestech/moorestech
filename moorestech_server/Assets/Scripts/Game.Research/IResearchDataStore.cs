using System;
using System.Collections.Generic;

namespace Game.Research
{
    public interface IResearchDataStore
    {
        bool CompleteResearch(Guid researchGuid, int playerId);
    }
    
    public class ResearchSaveJsonObject
    {
        public List<string> CompletedResearchGuids { get; set; }
        
        public ResearchSaveJsonObject()
        {
            CompletedResearchGuids = new List<string>();
        }
    }
}