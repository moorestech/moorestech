using UnityEngine;

namespace Client.DebugSystem.Environment
{
    public class DebugEnvironmentController : MonoBehaviour
    {
        [SerializeField] private GameObject debugEnvironment;
        [SerializeField] private GameObject pureNatureEnvironment;
        [SerializeField] private GameObject otherEnvironment;
        
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
            var otherEnvironment = false;
            switch (environmentType)
            {
                case DebugEnvironmentType.Debug:
                    debugEnvironment = true;
                    break;
                case DebugEnvironmentType.PureNature:
                    pureNatureEnvironment = true;
                    break;
                case DebugEnvironmentType.Other:
                    otherEnvironment = true;
                    break;
            }
            
            debugEnvironmentController.debugEnvironment.SetActive(debugEnvironment);
            debugEnvironmentController.pureNatureEnvironment.SetActive(pureNatureEnvironment);
            debugEnvironmentController.otherEnvironment.SetActive(otherEnvironment);
        }
    }
    
    public enum DebugEnvironmentType
    {
        Debug,
        PureNature,
        Other,
    }
}