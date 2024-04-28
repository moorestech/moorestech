using UnityEngine;

namespace Client.Game.Story
{
    public class SkitStarterObject : MonoBehaviour
    {
        public TextAsset ScenarioCsv => scenarioCsv;
        [SerializeField] private TextAsset scenarioCsv;
    }
}