using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    public class RocketLaunchPadDetailDto
    {
        public string Phase;
        public string WaitReason;
        public float Progress;
        public List<int> MaterialSlots;
        public List<int> PayloadSlots;
        public List<int> OutputSlots;
    }
}
