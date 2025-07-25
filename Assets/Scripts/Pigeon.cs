using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DefaultNamespace
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class Pigeon : MonoBehaviour
    {
        [Header("Control Type")]
        public bool isPlayerControlled = true;
        
        [Header("Movement Settings")]
        public float walkSpeed = 2f;
        public float runSpeed = 5f;
        public float rotationSpeed = 10f; // Degrees per second for A/D turning
        public float gravity = -9.81f;
        
        [Header("Input Settings (Player Only)")]
        public KeyCode runKey = KeyCode.LeftShift;
        public KeyCode flyKey = KeyCode.Space;
        
        [Header("AI Settings")]
        public PigeonPersonality personality = PigeonPersonality.Balanced;
        public float detectionRadius = 5f;
        public float personalityAggressiveness = 0.5f;
        
        [Header("Animation Control")]
        [SerializeField] PigeonAnimationData animationData;
        
        [Header("Eating Settings")]
        [SerializeField] Vector3 beakOffset = new Vector3(0f, 0.2f, 0.4f); // Forward and up from center
        public float beakEatingDistance = 0.8f;
        
        // System references
        PigeonEatingSystem eatingSystem;
        
        // Components
        CharacterController characterController;
        Animator animator;
        NavMeshAgent navAgent;
        Vector3 velocity;
        bool isGrounded;
        
        // AI State Management
        public PigeonState currentState = PigeonState.Wandering;
        GameObject targetFood;
        Vector3 wanderTarget;
        float nextWanderTime;
        
        // Competition & Interaction
        List<Pigeon> nearbyPigeons = new List<Pigeon>();
        bool isEating;
        float eatStartTime;
        float eatDuration = 2f;
        
        // Animation state tracking
        string currentAnimation = "";
        float lastIdleChangeTime;
        int currentIdleIndex;
        
        void Awake()
        {
            animator = GetComponent<Animator>();
            characterController = GetComponent<CharacterController>();
            
            // Setup based on control type
            if (isPlayerControlled)
            {
                // Player: Use CharacterController, disable NavMeshAgent
                navAgent = GetComponent<NavMeshAgent>();
                if (navAgent != null)
                {
                    navAgent.enabled = false;
                }
                
                // Setup CharacterController for player
                if (characterController == null)
                {
                    characterController = gameObject.AddComponent<CharacterController>();
                }
                characterController.radius = 0.3f;
                characterController.height = 0.8f;
                characterController.center = new Vector3(0, 0.4f, 0);
            }
            else
            {
                // AI: Use NavMeshAgent, disable CharacterController
                navAgent = GetComponent<NavMeshAgent>();
                if (navAgent == null)
                {
                    navAgent = gameObject.AddComponent<NavMeshAgent>();
                }
                
                // Configure NavMeshAgent
                navAgent.speed = walkSpeed;
                navAgent.angularSpeed = rotationSpeed * 60f;
                navAgent.acceleration = 8f;
                navAgent.stoppingDistance = 0.5f;
                navAgent.autoBraking = true;
                navAgent.radius = 0.3f;
                navAgent.height = 0.8f;
                navAgent.enabled = true;
                
                // Disable CharacterController for AI
                if (characterController != null)
                {
                    characterController.enabled = false;
                }
            }
        }

        void Start()
        {
            // Validate required components
            if (animationData == null)
            {
                Debug.LogError($"PigeonAnimationData not assigned to {gameObject.name}! Please assign it in the inspector.");
            }
            
            // Find eating system
            eatingSystem = FindFirstObjectByType<PigeonEatingSystem>();
            if (eatingSystem == null)
            {
                Debug.LogWarning("PigeonEatingSystem not found in scene!");
            }
            
            SetAnimation("Idle_A");
            lastIdleChangeTime = Time.time;
            
            // Initialize AI state
            if (!isPlayerControlled)
            {
                SetRandomWanderTarget();
                ApplyPersonalityTraits();
            }
        }

        void Update()
        {
            HandleGroundCheck();
            
            if (isPlayerControlled)
            {
                HandlePlayerInput();
                HandleDebugControls();
            }
            else
            {
                HandleAIBehavior();
            }
            
            HandleAnimation();
            HandleRandomIdleAnimations();
            HandleEating();
        }
        
        void HandleGroundCheck()
        {
            isGrounded = characterController.isGrounded;
            
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }
        }
        
        void HandlePlayerInput()
        {
            // Don't allow movement input while eating
            if (isEating)
            {
                velocity.y += gravity * Time.deltaTime;
                characterController.Move(velocity * Time.deltaTime);
                return;
            }
            
            float horizontal = Input.GetAxis("Horizontal"); // A/D keys for turning
            float vertical = Input.GetAxis("Vertical");     // W/S keys for forward/back
            bool isRunning = Input.GetKey(runKey);
            bool isFlying = Input.GetKey(flyKey);
            
            // Handle rotation (A/D keys)
            if (Mathf.Abs(horizontal) > 0.1f)
            {
                float rotationAmount = horizontal * rotationSpeed * 60f * Time.deltaTime; // Convert to degrees per second
                transform.Rotate(0, rotationAmount, 0);
            }
            
            // Handle forward/backward movement (W/S keys)
            if (Mathf.Abs(vertical) > 0.1f)
            {
                float currentSpeed = isRunning ? runSpeed : walkSpeed;
                Vector3 moveDirection = transform.forward * vertical; // Move along facing direction
                characterController.Move(moveDirection * currentSpeed * Time.deltaTime);
            }
            else
            {
                // Player is idle - check for nearby food to eat
                CheckForNearbyFoodToEat();
            }
            
            if (isFlying && isGrounded)
            {
                velocity.y = 5f;
            }
            
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }
        
        void HandleAIBehavior()
        {
            // Update nearby pigeons list
            UpdateNearbyPigeons();
            
            switch (currentState)
            {
                case PigeonState.Wandering:
                    HandleWandering();
                    break;
                case PigeonState.Investigating:
                    HandleInvestigating();
                    break;
                case PigeonState.Competing:
                    HandleCompeting();
                    break;
                case PigeonState.Eating:
                    // Handled in HandleEating()
                    break;
                case PigeonState.Retreating:
                    HandleRetreating();
                    break;
            }
            
            // Always look for food while wandering or investigating
            if (currentState == PigeonState.Wandering || currentState == PigeonState.Investigating)
            {
                LookForFood();
            }
            
            // Don't apply gravity for AI pigeons - NavMeshAgent handles all movement
        }
        
        void HandleWandering()
        {
            if (navAgent.enabled && navAgent.isOnNavMesh)
            {
                // Check if we've reached our wander target or need a new one
                if (!navAgent.hasPath || navAgent.remainingDistance < 0.5f || Time.time > nextWanderTime)
                {
                    SetRandomWanderTarget();
                }
            }
        }
        
        void HandleInvestigating()
        {
            if (targetFood == null)
            {
                ChangeState(PigeonState.Wandering);
                return;
            }
            
            if (navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.SetDestination(targetFood.transform.position);
                
                // Check if beak reached the food
                float distanceToFood = Vector3.Distance(GetBeakPosition(), targetFood.transform.position);
                if (distanceToFood <= beakEatingDistance)
                {
                    // Check if other pigeons are competing for same food
                    if (IsCompetitionForFood(targetFood))
                    {
                        ChangeState(PigeonState.Competing);
                    }
                    else
                    {
                        StartEating();
                    }
                }
            }
        }
        
        void HandleCompeting()
        {
            if (targetFood == null)
            {
                ChangeState(PigeonState.Wandering);
                return;
            }
            
            // Move toward food but with competition behavior
            Vector3 foodPosition = targetFood.transform.position;
            float distanceToFood = Vector3.Distance(GetBeakPosition(), foodPosition);
            
            if (distanceToFood <= beakEatingDistance)
            {
                // We're close enough to compete - do aggressive behavior
                PerformCompetitiveBehavior();
                
                // Check if we can start eating (maybe we scared others away)
                if (!IsCompetitionForFood(targetFood))
                {
                    StartEating();
                }
            }
            else
            {
                // Still approaching
                if (navAgent.enabled && navAgent.isOnNavMesh)
                {
                    navAgent.SetDestination(foodPosition);
                }
            }
        }
        
        void HandleRetreating()
        {
            // Move away from competition
            if (navAgent != null && navAgent.enabled)
            {
                if (!navAgent.hasPath || navAgent.remainingDistance < 0.5f)
                {
                    // Set a retreat destination away from the food
                    if (targetFood != null)
                    {
                        Vector3 retreatDirection = (transform.position - targetFood.transform.position).normalized;
                        Vector3 retreatTarget = transform.position + retreatDirection * 5f;
                        
                        NavMeshHit hit;
                        if (NavMesh.SamplePosition(retreatTarget, out hit, 5f, NavMesh.AllAreas))
                        {
                            navAgent.SetDestination(hit.position);
                        }
                    }
                    
                    targetFood = null; // Give up on this food
                    ChangeState(PigeonState.Wandering);
                }
            }
        }
        
        void MoveInDirection(Vector3 direction, bool isRunning = false)
        {
            // This method is now only used by AI pigeons
            // Calculate target rotation using Quaternion
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            
            // Smooth rotation for AI pigeons
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            
            // Move in the direction the pigeon is actually facing
            float currentSpeed = isRunning ? runSpeed : walkSpeed;
            Vector3 moveDirection = transform.forward;
            characterController.Move(moveDirection * currentSpeed * Time.deltaTime);
        }
        
        void LookForFood()
        {
            if (eatingSystem == null) return;
            
            // Use eating system to find nearby food
            List<GameObject> nearbyFood = eatingSystem.GetNearbyFood(transform.position, detectionRadius);
            
            foreach (GameObject food in nearbyFood)
            {
                if (food != null)
                {
                    // Found food! Change target if this food is closer or we don't have a target
                    float distanceToNewFood = Vector3.Distance(transform.position, food.transform.position);
                    
                    if (targetFood == null || 
                        Vector3.Distance(transform.position, targetFood.transform.position) > distanceToNewFood)
                    {
                        targetFood = food;
                        ChangeState(PigeonState.Investigating);
                        break;
                    }
                }
            }
        }
        
        void CheckForNearbyFoodToEat()
        {
            if (eatingSystem == null) return;
            
            // Use eating system to find nearby food within eating range
            List<GameObject> nearbyFood = eatingSystem.GetNearbyFood(GetBeakPosition(), beakEatingDistance);
            
            GameObject closestFood = null;
            float closestDistance = float.MaxValue;
            
            foreach (GameObject food in nearbyFood)
            {
                if (food != null)
                {
                    float distance = Vector3.Distance(GetBeakPosition(), food.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestFood = food;
                    }
                }
            }
            
            // If we found food nearby and beak is close enough, start eating it
            if (closestFood != null)
            {
                targetFood = closestFood;
                StartEating();
            }
        }
        
        Vector3 GetBeakPosition()
        {
            // Calculate beak position based on pigeon's rotation and offset
            return transform.position + transform.TransformDirection(beakOffset);
        }
        
        void FaceFood()
        {
            if (targetFood == null) return;
            
            Vector3 directionToFood = (targetFood.transform.position - transform.position).normalized;
            directionToFood.y = 0; // Keep pigeon upright, only rotate on Y axis
            
            if (directionToFood != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToFood);
                transform.rotation = targetRotation; // Instant turn while eating
            }
        }
        
        bool IsCompetitionForFood(GameObject food)
        {
            foreach (Pigeon pigeon in nearbyPigeons)
            {
                if (pigeon.targetFood == food && Vector3.Distance(pigeon.transform.position, food.transform.position) < 2f)
                {
                    return true;
                }
            }
            return false;
        }
        
        void PerformCompetitiveBehavior()
        {
            if (animationData == null) return;
            
            // Play aggressive animation and push others
            SetAnimation(animationData.AttackAnimation);
            
            foreach (Pigeon pigeon in nearbyPigeons)
            {
                float distance = Vector3.Distance(transform.position, pigeon.transform.position);
                if (distance < 1.5f && pigeon.targetFood == targetFood)
                {
                    // Instead of directly moving transform, make weaker pigeons retreat
                    if (pigeon.personalityAggressiveness < personalityAggressiveness * 0.7f)
                    {
                        pigeon.ChangeState(PigeonState.Retreating);
                        pigeon.SetRandomWanderTarget();
                    }
                }
            }
        }
        
        void UpdateNearbyPigeons()
        {
            nearbyPigeons.Clear();
            Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, 3f);
            
            foreach (Collider col in nearbyColliders)
            {
                Pigeon pigeon = col.GetComponent<Pigeon>();
                if (pigeon != null && pigeon != this)
                {
                    nearbyPigeons.Add(pigeon);
                }
            }
        }
        
        void StartEating()
        {
            if (animationData == null) return;
            
            ChangeState(PigeonState.Eating);
            isEating = true;
            eatStartTime = Time.time;
            
            // Face the food before eating
            FaceFood();
            
            SetAnimation(animationData.EatAnimation);
            
            if (navAgent.enabled)
            {
                navAgent.isStopped = true;
            }
        }
        
        void HandleEating()
        {
            if (isEating)
            {
                if (Time.time - eatStartTime >= eatDuration)
                {
                    // Finished eating
                    isEating = false;
                    
                    // Notify eating system and destroy the food
                    if (targetFood != null)
                    {
                        if (eatingSystem != null)
                        {
                            eatingSystem.RemoveFoodItem(targetFood);
                        }
                        Destroy(targetFood);
                        targetFood = null;
                    }
                    
                    if (navAgent.enabled)
                    {
                        navAgent.isStopped = false;
                    }
                    
                    ChangeState(PigeonState.Wandering);
                }
            }
        }
        
        void SetRandomWanderTarget()
        {
            if (navAgent == null || !navAgent.enabled) return;
            
            // Make sure we're on the NavMesh first
            if (!navAgent.isOnNavMesh)
            {
                // Try to warp to a valid position
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
                {
                    navAgent.Warp(hit.position);
                }
                else
                {
                    return; // Can't find valid NavMesh position
                }
            }
            
            // Now set a random destination
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * 10f;
            randomDirection += transform.position;
            randomDirection.y = transform.position.y;
            
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(randomDirection, out navHit, 10f, NavMesh.AllAreas))
            {
                navAgent.SetDestination(navHit.position);
                nextWanderTime = Time.time + UnityEngine.Random.Range(3f, 8f);
            }
        }
        
        void ApplyPersonalityTraits()
        {
            switch (personality)
            {
                case PigeonPersonality.Aggressive:
                    personalityAggressiveness = 0.9f;
                    detectionRadius = 6f;
                    walkSpeed *= 1.2f;
                    runSpeed *= 1.2f;
                    break;
                case PigeonPersonality.Cautious:
                    personalityAggressiveness = 0.2f;
                    detectionRadius = 4f;
                    walkSpeed *= 0.8f;
                    break;
                case PigeonPersonality.Opportunist:
                    personalityAggressiveness = 0.6f;
                    detectionRadius = 7f;
                    break;
                case PigeonPersonality.Territorial:
                    personalityAggressiveness = 0.7f;
                    detectionRadius = 5f;
                    // Could add territory claiming logic later
                    break;
                case PigeonPersonality.Balanced:
                default:
                    personalityAggressiveness = 0.5f;
                    break;
            }
            
            if (navAgent != null)
            {
                navAgent.speed = walkSpeed;
            }
        }
        
        void ChangeState(PigeonState newState)
        {
            currentState = newState;
        }
        
        void HandleAnimation()
        {
            if (animationData == null) 
            {
                Debug.LogWarning($"{gameObject.name}: PigeonAnimationData is null! Please assign it in the inspector.");
                return;
            }
            
            string targetAnimation = "";
            bool isMoving = false;
            bool isRunning = false;
            
            if (isPlayerControlled)
            {
                float horizontal = Input.GetAxis("Horizontal"); // A/D turning
                float vertical = Input.GetAxis("Vertical");     // W/S movement
                isRunning = Input.GetKey(runKey);
                isMoving = Mathf.Abs(vertical) > 0.1f; // Only W/S counts as movement
                bool isFlying = Input.GetKey(flyKey);
                
                if (isFlying || !isGrounded)
                {
                    targetAnimation = animationData.FlyAnimation;
                }
                else if (isMoving)
                {
                    targetAnimation = isRunning ? animationData.RunAnimation : animationData.WalkAnimation;
                }
            }
            else
            {
                // AI animation based on NavMeshAgent movement
                if (navAgent != null && navAgent.enabled && navAgent.velocity.magnitude > 0.1f)
                {
                    isMoving = true;
                    isRunning = navAgent.velocity.magnitude > walkSpeed * 1.1f;
                    targetAnimation = isRunning ? animationData.RunAnimation : animationData.WalkAnimation;
                }
                
                // Override with state-specific animations
                if (currentState == PigeonState.Competing)
                {
                    targetAnimation = animationData.AttackAnimation;
                }
                else if (currentState == PigeonState.Eating)
                {
                    targetAnimation = animationData.EatAnimation;
                }
            }
            
            if (!string.IsNullOrEmpty(targetAnimation) && targetAnimation != currentAnimation)
            {
                Debug.Log($"{gameObject.name}: Playing animation '{targetAnimation}' (was '{currentAnimation}')");
                SetAnimation(targetAnimation);
            }
            else if (string.IsNullOrEmpty(targetAnimation) && isMoving)
            {
                Debug.LogWarning($"{gameObject.name}: Should be moving but no target animation set! isMoving={isMoving}, isRunning={isRunning}");
            }
        }
        
        void HandleRandomIdleAnimations()
        {
            if (animationData == null) return;
            
            bool isMoving = false;
            bool isFlying = false;
            
            if (isPlayerControlled)
            {
                float vertical = Input.GetAxis("Vertical");     // Only W/S counts as movement
                isMoving = Mathf.Abs(vertical) > 0.1f;
                isFlying = Input.GetKey(flyKey) || !isGrounded;
            }
            else
            {
                isMoving = navAgent != null && navAgent.enabled && navAgent.velocity.magnitude > 0.1f;
            }
            
            if (!isMoving && !isFlying && !isEating && animationData.UseRandomIdleAnimations)
            {
                if (Time.time - lastIdleChangeTime >= animationData.IdleAnimationChangeInterval)
                {
                    SetAnimation(animationData.GetRandomIdleAnimation());
                    lastIdleChangeTime = Time.time;
                }
            }
        }
        
        void HandleDebugControls()
        {
            if (animationData == null) return;
            
            // Handle debug animation keys
            foreach (var debugKey in animationData.DebugKeys)
            {
                if (Input.GetKeyDown(debugKey.keyCode))
                {
                    SetAnimation(debugKey.animationName);
                }
            }
            
            // Handle debug shape key keys
            foreach (var debugShapeKey in animationData.DebugShapeKeys)
            {
                if (Input.GetKeyDown(debugShapeKey.keyCode))
                {
                    SetShapeKey(debugShapeKey.shapeKeyName);
                }
            }
        }

        void SetAnimation(string animationName)
        {
            if (animator == null || string.IsNullOrEmpty(animationName)) return;
            
            // Validate animation name if we have animation data
            if (animationData != null && !animationData.IsValidAnimation(animationName))
            {
                Debug.LogWarning($"Animation '{animationName}' not found in PigeonAnimationData!");
                return;
            }

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
        }
        
        void SetShapeKey(string shapeKeyName)
        {
            if (animator == null || string.IsNullOrEmpty(shapeKeyName)) return;
            
            // Validate shape key name if we have animation data
            if (animationData != null && !animationData.IsValidShapeKey(shapeKeyName))
            {
                Debug.LogWarning($"Shape key '{shapeKeyName}' not found in PigeonAnimationData!");
                return;
            }
            
            animator.Play(shapeKeyName);
        }
        
        // Public methods for external control
        public void PlayAnimation(string animationName) => SetAnimation(animationName);
        public void PlayShapeKey(string shapeKeyName) => SetShapeKey(shapeKeyName);
        public void SetMovementSpeed(float newWalkSpeed, float newRunSpeed)
        {
            walkSpeed = newWalkSpeed;
            runSpeed = newRunSpeed;
            if (navAgent != null) navAgent.speed = walkSpeed;
        }
        
        public bool IsMoving()
        {
            if (isPlayerControlled)
            {
                return characterController.velocity.magnitude > 0.1f;
            }
            else
            {
                return navAgent != null && navAgent.enabled && navAgent.velocity.magnitude > 0.1f;
            }
        }
        public bool IsGrounded() => isGrounded;
        public string GetCurrentAnimation() => currentAnimation;
        public List<string> GetAvailableAnimations() 
        {
            if (animationData == null) return new List<string>();
            return new List<string>(animationData.AllAnimations);
        }
        
        public List<string> GetAvailableShapeKeys() 
        {
            if (animationData == null) return new List<string>();
            return new List<string>(animationData.AllShapeKeys);
        }
    }
    
    [System.Serializable]
    public enum PigeonPersonality
    {
        Aggressive,
        Cautious, 
        Opportunist,
        Territorial,
        Balanced
    }
    
    [System.Serializable]
    public enum PigeonState
    {
        Wandering,
        Investigating,
        Competing,
        Eating,
        Retreating
    }
}