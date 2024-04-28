using UnityEngine;

namespace Client.Game.Skit.Starter
{
    public class SkitStarterObject : MonoBehaviour
    {
        [SerializeField] private TextAsset scenarioCsv;
        public TextAsset ScenarioCsv => scenarioCsv;
    }
}