using System;
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
        public float rotationSpeed = 10f;
        public float gravity = -9.81f;
        
        [Header("Input Settings (Player Only)")]
        public KeyCode runKey = KeyCode.LeftShift;
        public KeyCode flyKey = KeyCode.Space;
        
        [Header("AI Settings")]
        public PigeonPersonality personality = PigeonPersonality.Balanced;
        public float detectionRadius = 5f;
        public float personalityAggressiveness = 0.5f;
        
        [Header("Animation Control")]
        public bool useRandomIdleAnimations = true;
        public float idleAnimationChangeInterval = 3f;
        
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
        float lastStateChangeTime;
        float nextWanderTime;
        
        // Competition & Interaction
        List<Pigeon> nearbyPigeons = new List<Pigeon>();
        bool isEating = false;
        float eatStartTime;
        float eatDuration = 2f;
        
        // Animation state tracking
        string currentAnimation = "";
        float lastIdleChangeTime;
        int currentIdleIndex = 0;
        
        // Animation parameters
        readonly string SPEED_PARAM = "Speed";
        readonly string IS_MOVING_PARAM = "IsMoving";
        readonly string IS_RUNNING_PARAM = "IsRunning";
        readonly string IS_FLYING_PARAM = "IsFlying";
        
        List<string> animationList = new List<string> 
        {	"Attack",
            "Bounce",
            "Clicked",
            "Death",
            "Eat",
            "Fear",
            "Fly",
            "Hit",
            "Idle_A", "Idle_B", "Idle_C",
            "Jump",
            "Roll",
            "Run",
            "Sit",
            "Spin/Splash",
            "Swim",
            "Walk"
        };
        
        List<string> shapekeyList = new List<string>
        {	"Eyes_Annoyed",
            "Eyes_Blink",
            "Eyes_Cry",
            "Eyes_Dead",
            "Eyes_Excited",
            "Eyes_Happy",
            "Eyes_LookDown",
            "Eyes_LookIn",
            "Eyes_LookOut",
            "Eyes_LookUp",
            "Eyes_Rabid",
            "Eyes_Sad",
            "Eyes_Shrink",
            "Eyes_Sleep",
            "Eyes_Spin",
            "Eyes_Squint",
            "Eyes_Trauma",
            "Sweat_L",
            "Sweat_R",
            "Teardrop_L",
            "Teardrop_R"
        };
        
        List<string> idleAnimations = new List<string> { "Idle_A", "Idle_B", "Idle_C" };
        
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
            SetAnimation("Idle_A");
            lastIdleChangeTime = Time.time;
            lastStateChangeTime = Time.time;
            
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
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            bool isRunning = Input.GetKey(runKey);
            bool isFlying = Input.GetKey(flyKey);
            
            Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;
            
            if (direction.magnitude >= 0.1f)
            {
                MoveInDirection(direction, isRunning);
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
                
                // Check if we reached the food
                float distanceToFood = Vector3.Distance(transform.position, targetFood.transform.position);
                if (distanceToFood < 1f)
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
            float distanceToFood = Vector3.Distance(transform.position, foodPosition);
            
            if (distanceToFood < 0.8f)
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
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float angle = Mathf.LerpAngle(transform.eulerAngles.y, targetAngle, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.up);
            
            float currentSpeed = isRunning ? runSpeed : walkSpeed;
            Vector3 moveDirection = Quaternion.AngleAxis(targetAngle, Vector3.up) * Vector3.forward;
            characterController.Move(moveDirection.normalized * currentSpeed * Time.deltaTime);
        }
        
        void LookForFood()
        {
            // Find nearby food within detection radius
            Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, detectionRadius);
            
            foreach (Collider obj in nearbyObjects)
            {
                if (obj.CompareTag("Food")) // Assuming food has "Food" tag
                {
                    // Found food! Change target if this food is closer or we don't have a target
                    float distanceToNewFood = Vector3.Distance(transform.position, obj.transform.position);
                    
                    if (targetFood == null || 
                        Vector3.Distance(transform.position, targetFood.transform.position) > distanceToNewFood)
                    {
                        targetFood = obj.gameObject;
                        ChangeState(PigeonState.Investigating);
                        break;
                    }
                }
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
            // Play aggressive animation and push others
            SetAnimation("Attack");
            
            // Check personality - aggressive pigeons push harder
            float pushForce = personalityAggressiveness * 2f;
            
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
            ChangeState(PigeonState.Eating);
            isEating = true;
            eatStartTime = Time.time;
            SetAnimation("Eat");
            
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
                    
                    // Destroy the food
                    if (targetFood != null)
                    {
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
            lastStateChangeTime = Time.time;
        }
        
        void HandleAnimation()
        {
            string targetAnimation = "";
            bool isMoving = false;
            bool isRunning = false;
            
            if (isPlayerControlled)
            {
                float horizontal = Input.GetAxis("Horizontal");
                float vertical = Input.GetAxis("Vertical");
                isRunning = Input.GetKey(runKey);
                isMoving = Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;
                bool isFlying = Input.GetKey(flyKey);
                
                if (isFlying || !isGrounded)
                {
                    targetAnimation = "Fly";
                }
                else if (isMoving)
                {
                    targetAnimation = isRunning ? "Run" : "Walk";
                }
            }
            else
            {
                // AI animation based on NavMeshAgent movement
                if (navAgent != null && navAgent.enabled && navAgent.velocity.magnitude > 0.1f)
                {
                    isMoving = true;
                    isRunning = navAgent.velocity.magnitude > walkSpeed * 1.1f;
                    targetAnimation = isRunning ? "Run" : "Walk";
                }
                
                // Override with state-specific animations
                if (currentState == PigeonState.Competing)
                {
                    targetAnimation = "Attack";
                }
                else if (currentState == PigeonState.Eating)
                {
                    targetAnimation = "Eat";
                }
            }
            
            if (!string.IsNullOrEmpty(targetAnimation) && targetAnimation != currentAnimation)
            {
                SetAnimation(targetAnimation);
            }
        }
        
        void HandleRandomIdleAnimations()
        {
            bool isMoving = false;
            bool isFlying = false;
            
            if (isPlayerControlled)
            {
                float horizontal = Input.GetAxis("Horizontal");
                float vertical = Input.GetAxis("Vertical");
                isMoving = Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;
                isFlying = Input.GetKey(flyKey) || !isGrounded;
            }
            else
            {
                isMoving = navAgent != null && navAgent.enabled && navAgent.velocity.magnitude > 0.1f;
            }
            
            if (!isMoving && !isFlying && !isEating && useRandomIdleAnimations)
            {
                if (Time.time - lastIdleChangeTime >= idleAnimationChangeInterval)
                {
                    currentIdleIndex = UnityEngine.Random.Range(0, idleAnimations.Count);
                    SetAnimation(idleAnimations[currentIdleIndex]);
                    lastIdleChangeTime = Time.time;
                }
            }
        }
        
        void HandleDebugControls()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetAnimation("Eat");
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetAnimation("Attack");
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetAnimation("Jump");
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetAnimation("Sit");
            if (Input.GetKeyDown(KeyCode.Alpha5)) SetAnimation("Fear");
            
            if (Input.GetKeyDown(KeyCode.B)) SetShapeKey("Eyes_Blink");
            if (Input.GetKeyDown(KeyCode.H)) SetShapeKey("Eyes_Happy");
            if (Input.GetKeyDown(KeyCode.S)) SetShapeKey("Eyes_Sad");
        }

        void SetAnimation(string animationName)
        {
            if (animator == null || string.IsNullOrEmpty(animationName)) return;

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
            if (animator != null && !string.IsNullOrEmpty(shapeKeyName))
            {
                animator.Play(shapeKeyName);
            }
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
        public List<string> GetAvailableAnimations() => new List<string>(animationList);
        public List<string> GetAvailableShapeKeys() => new List<string>(shapekeyList);
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