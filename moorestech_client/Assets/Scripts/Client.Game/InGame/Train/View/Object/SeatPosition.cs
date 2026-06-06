using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    public sealed class SeatPosition : MonoBehaviour
    {
        // Prefab上で乗車座席のindexを指定する
        // Specify the riding seat index on the Prefab
        [SerializeField] private int seatIndex;

        public int GetSeatIndex()
        {
            return seatIndex;
        }
    }
}
