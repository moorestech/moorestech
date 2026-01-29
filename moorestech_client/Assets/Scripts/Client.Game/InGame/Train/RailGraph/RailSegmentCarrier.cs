using Game.Train.RailGraph;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    public sealed class RailSegmentCarrier : MonoBehaviour
    {
        private RailSegment _railSegment;

        public void SetRailSegment(RailSegment railSegment)
        {
            // レール区間をraycast判定用に保持する
            // Store the rail segment for raycast lookups
            _railSegment = railSegment;
        }

        public RailSegment GetRailSegment()
        {
            // レール区間の参照を返す
            // Return the stored rail segment
            return _railSegment;
        }
    }
}
