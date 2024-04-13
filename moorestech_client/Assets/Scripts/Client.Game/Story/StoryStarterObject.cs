using UnityEngine;

namespace Client.Game.Story
{
    public class StoryStarterObject : MonoBehaviour
    {
        public TextAsset ScenarioCsv => scenarioCsv;
        [SerializeField] private TextAsset scenarioCsv;
    }
}