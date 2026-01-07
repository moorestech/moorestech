using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    public class RailConnectPreviewObject : MonoBehaviour
    {
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        public void ShowPreview(TrainRailConnectPreviewData data)
        {
            Debug.Log($"ShowPreview {data.P0} {data.P1} {data.P2} {data.P3}");
            // TODO
        }
    }
}