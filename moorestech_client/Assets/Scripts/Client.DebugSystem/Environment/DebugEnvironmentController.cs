using UnityEngine;

namespace Client.DebugSystem.Environment
{
    public class DebugEnvironmentController : MonoBehaviour
    {
        [SerializeField] private GameObject debugEnvironment;
        [SerializeField] private GameObject pureNatureEnvironment;
        
        public static void SetEnvironment(DebugEnvironmentType environmentType)
        {
            var debugEnvironmentController = FindObjectOfType<DebugEnvironmentController>();
            
            if (debugEnvironmentController == null || 
                debugEnvironmentController.debugEnvironment == null || 
                debugEnvironmentController.pureNatureEnvironment == null)
            {
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
                case DebugEnvironmentType.Other:
                    break;
            }
            
            debugEnvironmentController.debugEnvironment.SetActive(debugEnvironment);
            debugEnvironmentController.pureNatureEnvironment.SetActive(pureNatureEnvironment);
        }
    }
    
    public enum DebugEnvironmentType
    {
        Debug,
        PureNature,
        Other,
    }
}