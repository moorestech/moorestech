using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject
{
    public class InventoryConnectorLineView : MonoBehaviour
    {
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform endPoint;
        [SerializeField] private LineRenderer lineRenderer;
        
        public void SetPoints(Vector3Int startConnect, Vector3Int endConnect)
        {
            startPoint.localPosition = startConnect + new Vector3(0.5f, 0.5f, 0.5f);
            endPoint.localPosition = endConnect + new Vector3(0.5f, 0.5f, 0.5f);
        }
        
        private void Update()
        {
            var start = startPoint.position;
            var end = endPoint.position;
            
            // ポジションを5分割する
            lineRenderer.positionCount = 5;
            for (var i = 0; i < 5; i++)
            {
                var rate = (float)i / 5;
                var point = Vector3.Lerp(start, end, rate);
                lineRenderer.SetPosition(i, point);
            }
        }
    }
}