using UnityEngine;

namespace DefaultNamespace
{
    /// <summary>
    /// Handles player input and controls the pigeon through the PigeonMovement component
    /// </summary>
    [RequireComponent(typeof(PigeonMovement), typeof(Pigeon))]
    public class PlayerPigeonController : MonoBehaviour
    {
        [Header("Input Settings")]
        [SerializeField] KeyCode runKey = KeyCode.LeftShift;
        [SerializeField] KeyCode flyKey = KeyCode.Space;
        
        // Component references
        PigeonMovement pigeonMovement;
        Pigeon pigeon;
        PigeonAnimationData animationData;
        
        void Awake()
        {
            pigeonMovement = GetComponent<PigeonMovement>();
            pigeon = GetComponent<Pigeon>();
        }
        
        void Start()
        {
            // Initialize movement for character controller
            pigeonMovement.InitializeForCharacterController();
            
            // Get animation data from pigeon if available
            animationData = GetAnimationDataFromPigeon();
        }
        
        void Update()
        {
            HandleMovementInput();
            HandleDebugControls();
        }
        
        void HandleMovementInput()
        {
            // Get input
            float horizontal = Input.GetAxis("Horizontal"); // A/D keys for turning
            float vertical = Input.GetAxis("Vertical");     // W/S keys for forward/back
            bool runPressed = Input.GetKey(runKey);
            bool flyPressed = Input.GetKey(flyKey);
            
            // Handle rotation (A/D keys)
            pigeonMovement.RotatePigeon(horizontal);
            
            // Handle forward/backward movement (W/S keys)
            Vector3 moveDirection = Vector3.zero;
            if (Mathf.Abs(vertical) > 0.1f)
            {
                moveDirection = transform.forward * vertical;
            }
            
            // Apply movement through PigeonMovement
            pigeonMovement.MoveWithCharacterController(moveDirection, runPressed, flyPressed);
        }
        
        void HandleDebugControls()
        {
            if (animationData == null) return;
            
            // Handle debug animation keys
            foreach (var debugKey in animationData.DebugKeys)
            {
                if (Input.GetKeyDown(debugKey.keyCode))
                {
                    pigeonMovement.PlayAnimation(debugKey.animationName);
                }
            }
            
            // Handle debug shape key keys
            foreach (var debugShapeKey in animationData.DebugShapeKeys)
            {
                if (Input.GetKeyDown(debugShapeKey.keyCode))
                {
                    pigeonMovement.PlayShapeKey(debugShapeKey.shapeKeyName);
                }
            }
        }
        
        PigeonAnimationData GetAnimationDataFromPigeon()
        {
            return pigeon?.GetAnimationData();
        }
        
        #region Public Interface
        
        /// <summary>
        /// Check if the player is currently moving
        /// </summary>
        public bool IsMoving => pigeonMovement != null && pigeonMovement.IsMoving;
        
        /// <summary>
        /// Check if the player is currently running
        /// </summary>
        public bool IsRunning => pigeonMovement != null && pigeonMovement.IsRunning;
        
        /// <summary>
        /// Check if the player is currently flying
        /// </summary>
        public bool IsFlying => pigeonMovement != null && pigeonMovement.IsFlying;
        
        /// <summary>
        /// Check if the player is grounded
        /// </summary>
        public bool IsGrounded => pigeonMovement != null && pigeonMovement.IsGrounded;
        
        /// <summary>
        /// Get current movement speed
        /// </summary>
        public float CurrentSpeed => pigeonMovement != null ? pigeonMovement.CurrentSpeed : 0f;
        
        #endregion
    }
}