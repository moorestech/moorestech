using Game.Train.RailGraph;

namespace Client.Game.InGame.Train.RailGraph
{
    /// <summary>
    ///     レール区間の分類（プレイヤーが切れない駅内部の区間か）
    ///     Rail edge classification (whether it is a station-internal edge players cannot cut)
    /// </summary>
    public static class RailEdgeClassifier
    {
        public static bool IsStationInternalEdge(IRailNode from, IRailNode to)
        {
            if (!from.StationRef.HasStation || !to.StationRef.HasStation) return false;
            return from.StationRef.StationBlockInstanceId.Equals(to.StationRef.StationBlockInstanceId);
        }
    }
}
