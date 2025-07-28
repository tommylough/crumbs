using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace
{
    /// <summary>
    /// Handles AI behavior and controls the pigeon through the PigeonMovement component
    /// </summary>
    [RequireComponent(typeof(PigeonMovement), typeof(Pigeon))]
    public class AIPigeonController : MonoBehaviour
    {
        [Header("AI Settings")]
        public PigeonPersonality personality = PigeonPersonality.Balanced;
        public float detectionRadius = 5f;
        public float personalityAggressiveness = 0.5f;
        
        [Header("Movement Settings")]
        [SerializeField] float baseWalkSpeed = 2f;
        [SerializeField] float baseRunSpeed = 5f;
        
        // Component references
        PigeonMovement pigeonMovement;
        Pigeon pigeon;
        PigeonEvents pigeonEvents;
        PigeonEatingSystem eatingSystem;
        PigeonAnimationData animationData;
        
        // AI State Management
        public PigeonState currentState = PigeonState.Wandering;
        GameObject targetFood;
        float nextWanderTime;
        
        // Competition & Interaction
        List<Pigeon> nearbyPigeons = new List<Pigeon>();
        
        void Awake()
        {
            pigeonMovement = GetComponent<PigeonMovement>();
            pigeon = GetComponent<Pigeon>();
            pigeonEvents = GetComponent<PigeonEvents>();
        }
        
        void Start()
        {
            // Initialize movement for NavMesh
            pigeonMovement.InitializeForNavMesh();
            
            // Find eating system
            eatingSystem = FindFirstObjectByType<PigeonEatingSystem>();
            if (eatingSystem == null)
            {
                Debug.LogWarning("PigeonEatingSystem not found in scene!");
            }
            
            // Get animation data
            animationData = GetAnimationDataFromPigeon();
            
            // Apply personality traits first
            ApplyPersonalityTraits();
            
            // Delay initial AI behavior to let NavMesh initialize properly
            Invoke(nameof(InitializeAI), 0.1f);
        }
        
        void InitializeAI()
        {
            // Initialize AI after NavMesh is ready
            SetRandomWanderTarget();
        }
        
        void Update()
        {
            HandleAIBehavior();
        }
        
        #region AI Behavior Logic
        
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
                    // Eating is handled by the Pigeon component
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
        }
        
        void HandleWandering()
        {
            // Check if we've reached our wander target or need a new one
            if (pigeonMovement.HasReachedAITarget() || Time.time > nextWanderTime)
            {
                SetRandomWanderTarget();
            }
        }
        
        void HandleInvestigating()
        {
            if (targetFood == null)
            {
                ChangeState(PigeonState.Wandering);
                return;
            }
            
            pigeonMovement.SetAITarget(targetFood.transform.position);
            
            // Check if we're close enough to the food
            float distanceToFood = Vector3.Distance(pigeon.GetBeakPosition(), targetFood.transform.position);
            if (distanceToFood <= pigeon.beakEatingDistance)
            {
                // Check if other pigeons are competing for same food
                if (IsCompetitionForFood(targetFood))
                {
                    ChangeState(PigeonState.Competing);
                }
                else
                {
                    // Let the Pigeon component handle the eating
                    pigeon.StartEatingFood(targetFood);
                    ChangeState(PigeonState.Eating);
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
            float distanceToFood = Vector3.Distance(pigeon.GetBeakPosition(), foodPosition);
            
            if (distanceToFood <= pigeon.beakEatingDistance)
            {
                // We're close enough to compete - do aggressive behavior
                PerformCompetitiveBehavior();
                
                // Check if we can start eating (maybe we scared others away)
                if (!IsCompetitionForFood(targetFood))
                {
                    pigeon.StartEatingFood(targetFood);
                    ChangeState(PigeonState.Eating);
                }
            }
            else
            {
                // Still approaching
                pigeonMovement.SetAITarget(foodPosition);
            }
        }
        
        void HandleRetreating()
        {
            // Check if we've reached our retreat target
            if (pigeonMovement.HasReachedAITarget())
            {
                // Set a retreat destination away from the food
                if (targetFood != null)
                {
                    Vector3 retreatDirection = (transform.position - targetFood.transform.position).normalized;
                    Vector3 retreatTarget = transform.position + retreatDirection * 5f;
                    pigeonMovement.SetAITarget(retreatTarget);
                }
                
                targetFood = null; // Give up on this food
                ChangeState(PigeonState.Wandering);
            }
        }
        
        #endregion
        
        #region Food Detection and Competition
        
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
        
        bool IsCompetitionForFood(GameObject food)
        {
            foreach (Pigeon pigeon in nearbyPigeons)
            {
                // Check if other pigeon has AI controller and is targeting same food
                AIPigeonController otherAI = pigeon.GetComponent<AIPigeonController>();
                if (otherAI != null && otherAI.targetFood == food && 
                    Vector3.Distance(pigeon.transform.position, food.transform.position) < 2f)
                {
                    return true;
                }
            }
            return false;
        }
        
        void PerformCompetitiveBehavior()
        {
            if (animationData == null) return;
            
            // Play aggressive animation
            pigeonMovement.PlayAnimation(animationData.AttackAnimation);
            
            foreach (Pigeon pigeon in nearbyPigeons)
            {
                float distance = Vector3.Distance(transform.position, pigeon.transform.position);
                if (distance < 1.5f)
                {
                    AIPigeonController otherAI = pigeon.GetComponent<AIPigeonController>();
                    if (otherAI != null && otherAI.targetFood == targetFood)
                    {
                        // Make weaker pigeons retreat
                        if (otherAI.personalityAggressiveness < personalityAggressiveness * 0.7f)
                        {
                            otherAI.ChangeState(PigeonState.Retreating);
                            otherAI.SetRandomWanderTarget();
                        }
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
                if (pigeon != null && pigeon != this.pigeon)
                {
                    nearbyPigeons.Add(pigeon);
                }
            }
        }
        
        #endregion
        
        #region Movement and Navigation
        
        void SetRandomWanderTarget()
        {
            // Set a random destination
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * 10f;
            randomDirection += transform.position;
            randomDirection.y = transform.position.y;
            
            pigeonMovement.SetAITarget(randomDirection);
            nextWanderTime = Time.time + UnityEngine.Random.Range(3f, 8f);
        }
        
        #endregion
        
        #region Personality and Configuration
        
        void ApplyPersonalityTraits()
        {
            float walkSpeed = baseWalkSpeed;
            float runSpeed = baseRunSpeed;
            
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
                    break;
                case PigeonPersonality.Balanced:
                default:
                    personalityAggressiveness = 0.5f;
                    break;
            }
            
            // Apply speeds to movement component
            pigeonMovement.SetMovementSpeeds(walkSpeed, runSpeed);
        }
        
        #endregion
        
        #region State Management
        
        void ChangeState(PigeonState newState)
        {
            PigeonState oldState = currentState;
            currentState = newState;
            
            // Handle state-specific movement behaviors
            switch (newState)
            {
                case PigeonState.Eating:
                    // Stop movement while eating
                    pigeonMovement.StopAIMovement();
                    // Face the food
                    if (targetFood != null)
                        pigeonMovement.FacePosition(targetFood.transform.position, true);
                    break;
                    
                case PigeonState.Competing:
                    // Set running for competition
                    pigeonMovement.SetAIRunning(true);
                    break;
                    
                case PigeonState.Retreating:
                    // Set running for retreat
                    pigeonMovement.SetAIRunning(true);
                    break;
                    
                default:
                    // Resume normal movement
                    pigeonMovement.ResumeAIMovement();
                    pigeonMovement.SetAIRunning(false);
                    break;
            }
            
            // Fire state change event
            if (pigeonEvents != null)
            {
                pigeonEvents.TriggerStateChanged(oldState, newState);
            }
        }
        
        #endregion
        
        #region Utility Methods
        
        PigeonAnimationData GetAnimationDataFromPigeon()
        {
            return pigeon?.GetAnimationData();
        }
        
        #endregion
        
        #region Public Interface
        
        /// <summary>
        /// Get the current AI state
        /// </summary>
        public PigeonState GetCurrentState() => currentState;
        
        /// <summary>
        /// Get the current target food (if any)
        /// </summary>
        public GameObject GetTargetFood() => targetFood;
        
        /// <summary>
        /// Force the AI to investigate specific food
        /// </summary>
        public void SetTargetFood(GameObject food)
        {
            targetFood = food;
            if (food != null)
            {
                ChangeState(PigeonState.Investigating);
            }
        }
        
        /// <summary>
        /// Check if AI is currently moving
        /// </summary>
        public bool IsMoving => pigeonMovement != null && pigeonMovement.IsMoving;
        
        /// <summary>
        /// Notify that eating has finished (called by Pigeon component)
        /// </summary>
        public void OnEatingFinished()
        {
            targetFood = null;
            pigeonMovement.ResumeAIMovement();
            ChangeState(PigeonState.Wandering);
        }
        
        #endregion
    }
}