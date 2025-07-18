using UnityEngine;

namespace Client.Skit.SkitObject
{
    public class SkitControllableObject : MonoBehaviour
    {
        public string ObjectId => objectId;
        [SerializeField] private string objectId;
        
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
    }
}