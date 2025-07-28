using UnityEngine;
using Unity.Cinemachine;

namespace DefaultNamespace
{
    public class CameraController : MonoBehaviour
    {
        [Header("Cinemachine Virtual Cameras")]
        [SerializeField] CinemachineCamera followCam;    // Assign your follow camera here
        [SerializeField] CinemachineCamera eatCam;       // Assign your eating camera here
        
        [Header("Target Settings")]
        [SerializeField] Transform target; // The pigeon to follow
        
        [Header("Camera Priorities")]
        [SerializeField] int followCameraPriority = 10;
        [SerializeField] int eatingCameraPriority = 20;
        
        // Internal state
        bool isTargetEating = false;
        PigeonEvents targetPigeonEvents;
        
        void Start()
        {
            // Validate camera assignments
            if (followCam == null || eatCam == null)
            {
                Debug.LogError("CameraController: Please assign both followCam and eatCam in the Inspector!");
                return;
            }
            
            if (target == null)
            {
                Debug.LogWarning("CameraController: No target pigeon found!");
                return;
            }
            
            // Setup cameras
            SetupCameras();
            
            // Subscribe to pigeon events
            targetPigeonEvents = target.GetComponent<PigeonEvents>();
            if (targetPigeonEvents != null)
            {
                targetPigeonEvents.OnStateChanged += OnPigeonStateChanged;
                targetPigeonEvents.OnEatingEvent += OnPigeonEatingEvent;
            }
            else
            {
                Debug.LogWarning("CameraController: Target pigeon doesn't have PigeonEvents component!");
            }
        }
        
        void SetupCameras()
        {
            // Setup Follow Camera
            followCam.Priority.Value = followCameraPriority;
            followCam.Follow = target;
            followCam.LookAt = target;
            
            // Setup Eating Camera (start inactive)
            eatCam.Priority.Value = 0; // Lower priority = inactive
            eatCam.Follow = target;
            eatCam.LookAt = target;
        }
        
        #region Event Handlers
        
        void OnPigeonStateChanged(PigeonStateChangeArgs args)
        {
            Debug.Log($"Camera: Pigeon state changed from {args.OldState} to {args.NewState}, isTargetEating={isTargetEating}");
            
            switch (args.NewState)
            {
                case PigeonState.Eating:
                    SwitchToEatingCamera();
                    break;
                    
                case PigeonState.Wandering:
                case PigeonState.Investigating:
                case PigeonState.Competing:
                case PigeonState.Retreating:
                    if (isTargetEating) // Just stopped eating
                    {
                        Debug.Log("Camera: Detected transition away from eating state - switching to follow camera");
                        SwitchToFollowCamera();
                    }
                    else
                    {
                        Debug.Log("Camera: State changed but pigeon wasn't eating, no camera switch needed");
                    }
                    break;
            }
        }
        
        void OnPigeonEatingEvent(PigeonEatingArgs args)
        {
            switch (args.EventType)
            {
                case EatingEventType.StartedEating:
                    Debug.Log("Camera: StartedEating event received - switching to eating camera");
                    SwitchToEatingCamera();
                    break;
                    
                case EatingEventType.FinishedEating:
                    Debug.Log("Camera: FinishedEating event received - switching to follow camera");
                    SwitchToFollowCamera();
                    break;
                    
                case EatingEventType.CompetingForFood:
                    Debug.Log("Camera: Competition detected!");
                    break;
            }
        }
        
        #endregion
        
        #region Camera Switching
        
        void SwitchToEatingCamera()
        {
            if (eatCam == null) 
            {
                Debug.LogError("Camera: eatCam is null! Cannot switch to eating camera.");
                return;
            }
            
            isTargetEating = true;
            eatCam.Priority.Value = eatingCameraPriority;      // Make eating camera active
            followCam.Priority.Value = followCameraPriority - 10; // Lower follow camera priority
            
            Debug.Log($"Camera: Switched to eating camera - EatCam priority: {eatCam.Priority.Value}, FollowCam priority: {followCam.Priority.Value}");
        }
        
        void SwitchToFollowCamera()
        {
            if (followCam == null) 
            {
                Debug.LogError("Camera: followCam is null! Cannot switch to follow camera.");
                return;
            }
            
            isTargetEating = false;
            followCam.Priority.Value = followCameraPriority;    // Make follow camera active
            eatCam.Priority.Value = eatingCameraPriority - 10;  // Lower eating camera priority
            
            Debug.Log($"Camera: Switched to follow camera - FollowCam priority: {followCam.Priority.Value}, EatCam priority: {eatCam.Priority.Value}");
        }
        
        #endregion
        
        #region Public Methods for Testing
        
        [ContextMenu("Test Eating Camera")]
        public void ForceEatingCamera()
        {
            if (Application.isPlaying)
                SwitchToEatingCamera();
        }
        
        [ContextMenu("Test Follow Camera")]
        public void ForceFollowCamera()
        {
            if (Application.isPlaying)
                SwitchToFollowCamera();
        }
        
        /// <summary>
        /// Get the currently active camera
        /// </summary>
        public CinemachineCamera GetActiveCamera()
        {
            if (eatCam != null && eatCam.Priority.Value > followCam.Priority.Value)
                return eatCam;
            return followCam;
        }
        
        #endregion
        
        void OnDestroy()
        {
            // Unsubscribe from events to prevent memory leaks
            if (targetPigeonEvents != null)
            {
                targetPigeonEvents.OnStateChanged -= OnPigeonStateChanged;
                targetPigeonEvents.OnEatingEvent -= OnPigeonEatingEvent;
            }
        }
        
        void OnValidate()
        {
            // Ensure priorities make sense
            if (eatingCameraPriority <= followCameraPriority)
            {
                Debug.LogWarning("CameraController: Eating camera priority should be higher than follow camera priority!");
            }
            
            // Update camera priorities in real-time if playing
            if (Application.isPlaying && followCam != null && eatCam != null)
            {
                if (!isTargetEating)
                {
                    followCam.Priority.Value = followCameraPriority;
                    eatCam.Priority.Value = 0;
                }
                else
                {
                    eatCam.Priority.Value = eatingCameraPriority;
                    followCam.Priority.Value = followCameraPriority - 10;
                }
            }
        }
    }
}