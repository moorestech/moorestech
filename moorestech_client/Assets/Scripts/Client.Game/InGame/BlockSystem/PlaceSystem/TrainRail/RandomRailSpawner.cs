using UnityEngine;

/// <summary>
/// シーン上でランダムな制御点を持つレールプレハブを複数スポーンさせるユーティリティ。
/// </summary>
/// 
namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    public class RandomRailSpawner : MonoBehaviour
    {
        [SerializeField] private BezierRailChain _railPrefab;
        [SerializeField, Range(2, 7)] private int _railCount = 14;
        [SerializeField] private Vector2 _xRange = new(-15f, 15f);
        [SerializeField] private Vector2 _yRange = new(-2f, 2f);
        [SerializeField] private Vector2 _zRange = new(-15f, 15f);
        [SerializeField] private Vector2 _curveOffsetRange = new(1f, 18f);
        [SerializeField] private float _maxUpDown = 3f;

        int cnt = 0;
        GameObject[] rails;
        private void Start()
        {
            if (_railPrefab == null)
            {
                Debug.LogWarning("[RandomRailSpawner] Rail prefab is not assigned.", this);
                return;
            }

            var count = Mathf.Clamp(_railCount, 2, 7);
            rails = new GameObject[count];
            for (var i = 0; i < count; i++)
                rails[i] = SpawnRail(i);
        }
        private void Update()
        {
            if (cnt == 110)
            {
                //ランダムに消す   
                int indexToRemove = Random.Range(0, rails.Length);
                Destroy(rails[indexToRemove]);
            }
            cnt++;

        }

        private GameObject SpawnRail(int index)
        {
            var instance = Instantiate(_railPrefab, transform);

            var start = new Vector3(
                Random.Range(_xRange.x, _xRange.y),
                Random.Range(_yRange.x, _yRange.y),
                Random.Range(_zRange.x, _zRange.y)
            );

            var forward = Random.insideUnitSphere;
            forward.y = Mathf.Clamp(forward.y, -0.2f, 0.2f);
            forward.Normalize();

            var offset = Random.Range(_curveOffsetRange.x, _curveOffsetRange.y);
            var control1 = start + forward * offset;
            var control2 = control1 + new Vector3(
                Random.Range(-3f, 3f),
                Random.Range(-_maxUpDown, _maxUpDown),
                Random.Range(-3f, 3f)
            );
            var end = control2 + forward * offset;

            instance.SetControlPoints(start, control1, control2, end);
            instance.Rebuild();
            instance.name = $"RandomRail_{index}";
            return instance.gameObject;
        }
    }
}