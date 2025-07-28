using UnityEngine;
using UnityEngine.AI;

namespace DefaultNamespace
{
    /// <summary>
    /// Pure movement script responsible for moving, animating and sending movement events.
    /// Used by both PlayerPigeonController and AIPigeonController.
    /// 
    /// NOTE: PigeonAnimationData should be assigned to the Pigeon component, not this component.
    /// For Player pigeons: Add CharacterController component manually.
    /// For AI pigeons: Add NavMeshAgent component manually.
    /// </summary>
    [RequireComponent(typeof(PigeonEvents), typeof(Animator))]
    public class PigeonMovement : MonoBehaviour
    {
        [Header("Movement Parameters")]
        [SerializeField] float walkSpeed = 2f;
        [SerializeField] float runSpeed = 5f;
        [SerializeField] float rotationSpeed = 10f;
        [SerializeField] float gravity = -9.81f;
        [SerializeField] float jumpForce = 5f;
        
        [Header("Ground Detection")]
        [SerializeField] LayerMask groundLayers = 1;
        [SerializeField] float groundCheckDistance = 0.1f;
        
        // Components
        CharacterController characterController;
        NavMeshAgent navAgent;
        PigeonEvents pigeonEvents;
        Animator animator;
        
        // Animation data (retrieved from Pigeon component)
        PigeonAnimationData animationData;
        
        // Movement state
        Vector3 velocity;
        bool isGrounded;
        bool isMoving;
        bool isRunning;
        bool isFlying;
        
        // State tracking for events
        bool wasMoving;
        bool wasRunning;
        bool wasFlying;
        
        // Animation state tracking
        string currentAnimation = "";
        float lastIdleChangeTime;
        
        // AI movement targets
        Vector3 aiTargetPosition;
        bool hasAITarget;
        
        // Movement mode
        bool useCharacterController = true;
        
        // Public properties
        public bool IsMoving => isMoving;
        public bool IsRunning => isRunning;
        public bool IsFlying => isFlying;
        public bool IsGrounded => isGrounded;
        public float CurrentSpeed => GetCurrentSpeed();
        public Vector3 Velocity => GetVelocity();
        
        void Awake()
        {
            pigeonEvents = GetComponent<PigeonEvents>();
            animator = GetComponent<Animator>();
            
            // Get existing components (these should be added in Inspector)
            characterController = GetComponent<CharacterController>();
            navAgent = GetComponent<NavMeshAgent>();
            
            // Determine movement mode based on which component exists
            useCharacterController = characterController != null;
        }
        
        void Start()
        {
            // Get animation data from Pigeon component
            Pigeon pigeonComponent = GetComponent<Pigeon>();
            if (pigeonComponent != null)
            {
                animationData = pigeonComponent.GetAnimationData();
            }
            
            SetAnimation("Idle_A");
            lastIdleChangeTime = Time.time;
        }
        
        void Update()
        {
            if (useCharacterController)
            {
                HandleGroundCheck();
                ApplyGravity();
            }
            else
            {
                HandleNavMeshMovement();
            }
            
            UpdateMovementState();
            HandleAnimation();
            HandleRandomIdleAnimations();
            CheckForMovementStateChanges();
        }
        
        #region Public Movement Interface
        
        /// <summary>
        /// Move using character controller (for player)
        /// </summary>
        public void MoveWithCharacterController(Vector3 moveDirection, bool shouldRun, bool shouldJump)
        {
            if (!useCharacterController || characterController == null || !characterController.enabled) return;
            
            float currentSpeed = shouldRun ? runSpeed : walkSpeed;
            Vector3 movement = moveDirection * currentSpeed;
            
            isMoving = moveDirection.magnitude > 0.1f;
            isRunning = shouldRun && isMoving;
            
            // Handle jumping/flying
            if (shouldJump && isGrounded)
            {
                velocity.y = jumpForce;
                isFlying = true;
            }
            
            characterController.Move(movement * Time.deltaTime);
        }
        
        /// <summary>
        /// Rotate the pigeon (for player)
        /// </summary>
        public void RotatePigeon(float rotationInput)
        {
            if (Mathf.Abs(rotationInput) > 0.1f)
            {
                float rotationAmount = rotationInput * rotationSpeed * 60f * Time.deltaTime;
                transform.Rotate(0, rotationAmount, 0);
            }
        }
        
        /// <summary>
        /// Set target for AI movement
        /// </summary>
        public void SetAITarget(Vector3 targetPosition)
        {
            if (useCharacterController || navAgent == null || !navAgent.enabled) 
                return;
            
            navAgent.SetDestination(targetPosition);
            aiTargetPosition = targetPosition;
            hasAITarget = true;
        }
        
        /// <summary>
        /// Stop AI movement
        /// </summary>
        public void StopAIMovement()
        {
            if (useCharacterController || navAgent == null || !navAgent.enabled) return;
            
            navAgent.isStopped = true;
            hasAITarget = false;
        }
        
        /// <summary>
        /// Resume AI movement
        /// </summary>
        public void ResumeAIMovement()
        {
            if (useCharacterController || navAgent == null || !navAgent.enabled) return;
            
            navAgent.isStopped = false;
        }
        
        /// <summary>
        /// Check if AI has reached target
        /// </summary>
        public bool HasReachedAITarget()
        {
            if (useCharacterController || navAgent == null || !hasAITarget) return false;
            
            return !navAgent.hasPath || navAgent.remainingDistance < 0.5f;
        }
        
        /// <summary>
        /// Set running state for AI
        /// </summary>
        public void SetAIRunning(bool shouldRun)
        {
            if (useCharacterController || navAgent == null) return;
            
            navAgent.speed = shouldRun ? runSpeed : walkSpeed;
        }
        
        /// <summary>
        /// Face a specific position
        /// </summary>
        public void FacePosition(Vector3 targetPosition, bool instant = false)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            direction.y = 0; // Keep upright
            
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                
                if (instant)
                {
                    transform.rotation = targetRotation;
                }
                else
                {
                    float rotateSpeed = rotationSpeed * 60f * Time.deltaTime;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotateSpeed);
                }
            }
        }
        
        #endregion
        
        #region Animation Control
        
        /// <summary>
        /// Play a specific animation
        /// </summary>
        public void PlayAnimation(string animationName)
        {
            SetAnimation(animationName);
        }
        
        /// <summary>
        /// Play a shape key animation
        /// </summary>
        public void PlayShapeKey(string shapeKeyName)
        {
            SetShapeKey(shapeKeyName);
        }
        
        void HandleAnimation()
        {
            // Get animation data from Pigeon component if we don't have it
            if (animationData == null)
            {
                Pigeon pigeonComponent = GetComponent<Pigeon>();
                if (pigeonComponent != null)
                {
                    animationData = pigeonComponent.GetAnimationData();
                }
            }
            
            if (animationData == null) return;
            
            string targetAnimation = "";
            
            // Determine animation based on movement state
            if (isFlying)
            {
                targetAnimation = animationData.FlyAnimation;
            }
            else if (isMoving)
            {
                targetAnimation = isRunning ? animationData.RunAnimation : animationData.WalkAnimation;
            }
            
            if (!string.IsNullOrEmpty(targetAnimation) && targetAnimation != currentAnimation)
            {
                SetAnimation(targetAnimation);
            }
        }
        
        void HandleRandomIdleAnimations()
        {
            if (animationData == null || !animationData.UseRandomIdleAnimations) return;
            
            if (!isMoving && !isFlying && !isRunning)
            {
                if (Time.time - lastIdleChangeTime >= animationData.IdleAnimationChangeInterval)
                {
                    SetAnimation(animationData.GetRandomIdleAnimation());
                    lastIdleChangeTime = Time.time;
                }
            }
        }
        
        void SetAnimation(string animationName)
        {
            if (animator == null || string.IsNullOrEmpty(animationName)) return;
            
            if (animationData != null && !animationData.IsValidAnimation(animationName))
                return;
            
            string oldAnimation = currentAnimation;
            
            if (animationName == "Spin/Splash")
            {
                if (animator.HasState(0, Animator.StringToHash("Spin")))
                {
                    animator.Play("Spin");
                    currentAnimation = "Spin";
                }
                else if (animator.HasState(0, Animator.StringToHash("Splash")))
                {
                    animator.Play("Splash");
                    currentAnimation = "Splash";
                }
            }
            else
            {
                animator.Play(animationName);
                currentAnimation = animationName;
            }
            
            // Fire animation change event
            if (pigeonEvents != null && oldAnimation != currentAnimation)
            {
                pigeonEvents.TriggerAnimationChanged(oldAnimation, currentAnimation);
            }
        }
        
        void SetShapeKey(string shapeKeyName)
        {
            if (animator == null || string.IsNullOrEmpty(shapeKeyName)) return;
            
            if (animationData != null && !animationData.IsValidShapeKey(shapeKeyName))
                return;
            
            animator.Play(shapeKeyName);
        }
        
        #endregion
        
        #region Movement State Management
        
        void HandleGroundCheck()
        {
            if (characterController == null || !characterController.enabled) return;
            
            isGrounded = characterController.isGrounded;
            
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
                if (isFlying)
                    isFlying = false;
            }
            else if (!isGrounded)
            {
                isFlying = velocity.y > 0;
            }
        }
        
        void ApplyGravity()
        {
            if (characterController == null || !characterController.enabled) return;
            
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }
        
        void HandleNavMeshMovement()
        {
            if (navAgent == null || !navAgent.enabled) return;
            
            // NavMesh is always grounded
            isGrounded = true;
            isFlying = false;
        }
        
        void UpdateMovementState()
        {
            if (useCharacterController)
            {
                // Character controller movement state is set in MoveWithCharacterController
            }
            else if (navAgent != null && navAgent.enabled)
            {
                // Update state based on NavMeshAgent
                float velocityMagnitude = navAgent.velocity.magnitude;
                isMoving = velocityMagnitude > 0.1f;
                isRunning = velocityMagnitude > walkSpeed * 1.1f;
            }
        }
        
        void CheckForMovementStateChanges()
        {
            if (pigeonEvents != null && (isMoving != wasMoving || isRunning != wasRunning || isFlying != wasFlying))
            {
                pigeonEvents.TriggerMovementChanged(isMoving, isRunning, isFlying, CurrentSpeed);
                
                wasMoving = isMoving;
                wasRunning = isRunning;
                wasFlying = isFlying;
            }
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Set movement speeds
        /// </summary>
        public void SetMovementSpeeds(float newWalkSpeed, float newRunSpeed)
        {
            walkSpeed = newWalkSpeed;
            runSpeed = newRunSpeed;
            
            // Update NavMeshAgent speed if we're using it
            if (!useCharacterController && navAgent != null)
            {
                navAgent.speed = isRunning ? runSpeed : walkSpeed;
            }
        }
        
        /// <summary>
        /// Warp to position
        /// </summary>
        public void WarpToPosition(Vector3 position)
        {
            if (useCharacterController && characterController != null)
            {
                characterController.enabled = false;
                transform.position = position;
                characterController.enabled = true;
            }
            else if (!useCharacterController && navAgent != null && navAgent.enabled)
            {
                navAgent.Warp(position);
            }
            else
            {
                transform.position = position;
            }
        }
        
        float GetCurrentSpeed()
        {
            if (useCharacterController && characterController != null)
            {
                return characterController.velocity.magnitude;
            }
            else if (!useCharacterController && navAgent != null && navAgent.enabled)
            {
                return navAgent.velocity.magnitude;
            }
            return 0f;
        }
        
        Vector3 GetVelocity()
        {
            if (useCharacterController && characterController != null)
            {
                return characterController.velocity;
            }
            else if (!useCharacterController && navAgent != null && navAgent.enabled)
            {
                return navAgent.velocity;
            }
            return Vector3.zero;
        }
        
        public string GetCurrentAnimation() => currentAnimation;
        
        #endregion
        
        void OnDrawGizmosSelected()
        {
            // Draw AI target if we have one
            if (!useCharacterController && hasAITarget)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(aiTargetPosition, 0.5f);
                Gizmos.DrawLine(transform.position, aiTargetPosition);
            }
            
            // Draw ground check
            if (useCharacterController)
            {
                Gizmos.color = isGrounded ? Color.green : Color.red;
                Gizmos.DrawRay(transform.position, Vector3.down * groundCheckDistance);
            }
        }
    }
}