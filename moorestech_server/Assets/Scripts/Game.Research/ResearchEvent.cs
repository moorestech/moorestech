using System;

namespace Game.Research
{
    public class ResearchEvent
    {
        public event Action<int, Guid> ResearchCompleted;
        internal void Invoke(int playerId, Guid researchGuid) => ResearchCompleted?.Invoke(playerId, researchGuid);
    }
}
