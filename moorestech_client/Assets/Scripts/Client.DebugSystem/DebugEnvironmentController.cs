using UnityEngine;

namespace Client.DebugSystem
{
    public class DebugEnvironmentController : MonoBehaviour
    {
        [SerializeField] private GameObject debugEnvironment;
        [SerializeField] private GameObject pureNatureEnvironment;
        
        public static void SetEnvironment(DebugEnvironmentType environmentType)
        {
            var debugEnvironmentController = FindObjectOfType<DebugEnvironmentController>();
            if (debugEnvironmentController == null)
            {
                Debug.LogError("DebugEnvironmentController not found");
                return;
            }
            
            var debugEnvironment = false;
            var pureNatureEnvironment = false;
            switch (environmentType)
            {
                case DebugEnvironmentType.Debug:
                    debugEnvironment = true;
                    break;
                case DebugEnvironmentType.PureNature:
                    pureNatureEnvironment = true;
                    break;
            }
            
            debugEnvironmentController.debugEnvironment.SetActive(debugEnvironment);
            debugEnvironmentController.pureNatureEnvironment.SetActive(pureNatureEnvironment);
        }
    }
    
    public enum DebugEnvironmentType
    {
        Debug,
        PureNature
    }
}