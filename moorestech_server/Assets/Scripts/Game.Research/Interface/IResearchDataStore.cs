using System;
using System.Collections.Generic;

namespace Game.Research.Interface
{
    public interface IResearchDataStore
    {
        bool IsResearchCompleted(Guid researchGuid);
        bool CanCompleteResearch(Guid researchGuid, int playerId);
        ResearchCompletionResult CompleteResearch(Guid researchGuid, int playerId);
        HashSet<Guid> GetCompletedResearchGuids();
        ResearchSaveJsonObject GetSaveJsonObject();
        void LoadResearchData(ResearchSaveJsonObject saveData);
    }
}

