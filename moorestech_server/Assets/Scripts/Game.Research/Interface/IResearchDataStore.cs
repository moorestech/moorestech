using System;
using System.Collections.Generic;

namespace Game.Research.Interface
{
    public interface IResearchDataStore
    {
        // 研究完了チェック（ワールド単位）
        bool IsResearchCompleted(Guid researchGuid);
        bool CanCompleteResearch(Guid researchGuid, int playerId);

        // 研究完了処理
        ResearchCompletionResult CompleteResearch(Guid researchGuid, int playerId);

        // データ取得
        HashSet<Guid> GetCompletedResearchGuids();

        // 永続化
        ResearchSaveJsonObject GetSaveJsonObject();
        void LoadResearchData(ResearchSaveJsonObject saveData);
    }

    public class ResearchCompletionResult
    {
        public bool Success { get; set; }
        public Guid CompletedResearchGuid { get; set; }
        public string Reason { get; set; }
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