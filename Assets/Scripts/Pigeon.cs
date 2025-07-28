using UnityEngine;

namespace DefaultNamespace
{
    /// <summary>
    /// Shared pigeon functionality - eating system, events, and basic properties.
    /// Movement and animations are handled by PigeonMovement component.
    /// AI/Player logic is handled by respective controller components.
    /// </summary>
    [RequireComponent(typeof(PigeonEvents))]
    public class Pigeon : MonoBehaviour
    {
        [Header("Eating Settings")]
        [SerializeField] Vector3 beakOffset = new Vector3(0f, 0.2f, 0.4f);
        public float beakEatingDistance = 0.8f;
        [SerializeField] float eatDuration = 2f;
        
        [Header("Animation Data - ASSIGN HERE!")]
        [SerializeField] PigeonAnimationData animationData;
        
        // System references
        PigeonEatingSystem eatingSystem;
        PigeonEvents pigeonEvents;
        
        // Eating state
        bool isEating;
        float eatStartTime;
        GameObject currentFood;
        
        // Component references (determined at runtime)
        PlayerPigeonController playerController;
        AIPigeonController aiController;
        
        void Awake()
        {
            pigeonEvents = GetComponent<PigeonEvents>();
            if (pigeonEvents == null)
            {
                pigeonEvents = gameObject.AddComponent<PigeonEvents>();
            }
            
            // Check what type of controller we have
            playerController = GetComponent<PlayerPigeonController>();
            aiController = GetComponent<AIPigeonController>();
        }
        
        void Start()
        {
            // Find eating system
            eatingSystem = FindFirstObjectByType<PigeonEatingSystem>();
            if (eatingSystem == null)
            {
                Debug.LogWarning("PigeonEatingSystem not found in scene!");
            }
        }
        
        void Update()
        {
            HandleEating();
            
            // Check for nearby food if player is idle
            if (IsPlayerControlled() && !IsMoving())
            {
                CheckForNearbyFoodToEat();
            }
        }
        
        #region Eating System
        
        /// <summary>
        /// Start eating a specific food item
        /// </summary>
        public void StartEatingFood(GameObject food)
        {
            if (food == null || isEating) return;
            
            isEating = true;
            eatStartTime = Time.time;
            currentFood = food;
            
            // Face the food
            PigeonMovement movement = GetComponent<PigeonMovement>();
            if (movement != null)
            {
                movement.FacePosition(food.transform.position, true);
                movement.PlayAnimation(animationData?.EatAnimation ?? "");
            }
            
            // Fire eating started event
            pigeonEvents?.TriggerEatingEvent(EatingEventType.StartedEating, food);
        }
        
        void HandleEating()
        {
            if (!isEating) return;
            
            if (Time.time - eatStartTime >= eatDuration)
            {
                FinishEating();
            }
        }
        
        void FinishEating()
        {
            if (!isEating) return;
            
            isEating = false;
            
            // Fire eating finished event
            pigeonEvents?.TriggerEatingEvent(EatingEventType.FinishedEating, currentFood);
            
            // Notify eating system and destroy the food
            if (currentFood != null)
            {
                eatingSystem?.RemoveFoodItem(currentFood);
                Destroy(currentFood);
                currentFood = null;
            }
            
            // Notify AI controller if this is an AI pigeon
            aiController?.OnEatingFinished();
        }
        
        void CheckForNearbyFoodToEat()
        {
            if (eatingSystem == null || isEating) return;
            
            // Find nearby food within eating range
            var nearbyFood = eatingSystem.GetNearbyFood(GetBeakPosition(), beakEatingDistance);
            
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
            
            // Start eating if we found food nearby
            if (closestFood != null)
            {
                StartEatingFood(closestFood);
            }
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Calculate beak position based on pigeon's rotation and offset
        /// </summary>
        public Vector3 GetBeakPosition()
        {
            return transform.position + transform.TransformDirection(beakOffset);
        }
        
        /// <summary>
        /// Check if this pigeon is player controlled
        /// </summary>
        public bool IsPlayerControlled()
        {
            return playerController != null;
        }
        
        /// <summary>
        /// Check if this pigeon is AI controlled
        /// </summary>
        public bool IsAIControlled()
        {
            return aiController != null;
        }
        
        /// <summary>
        /// Check if the pigeon is currently moving
        /// </summary>
        public bool IsMoving()
        {
            PigeonMovement movement = GetComponent<PigeonMovement>();
            return movement != null && movement.IsMoving;
        }
        
        /// <summary>
        /// Check if the pigeon is currently eating
        /// </summary>
        public bool IsEating()
        {
            return isEating;
        }
        
        /// <summary>
        /// Get the current state (for AI pigeons)
        /// </summary>
        public PigeonState GetCurrentState()
        {
            return aiController?.GetCurrentState() ?? PigeonState.Wandering;
        }
        
        #endregion
        
        #region Public Properties and Methods
        
        /// <summary>
        /// Get animation data (for controllers to access)
        /// </summary>
        public PigeonAnimationData GetAnimationData()
        {
            return animationData;
        }
        
        /// <summary>
        /// Set eating duration
        /// </summary>
        public void SetEatingDuration(float duration)
        {
            eatDuration = duration;
        }
        
        /// <summary>
        /// Set beak eating distance
        /// </summary>
        public void SetBeakEatingDistance(float distance)
        {
            beakEatingDistance = distance;
        }
        
        #endregion
        
        void OnDrawGizmosSelected()
        {
            // Draw beak position
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(GetBeakPosition(), 0.1f);
            
            // Draw eating range
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(GetBeakPosition(), beakEatingDistance);
            
            // Draw beak offset line
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, GetBeakPosition());
        }
    }
    
    // Keep these enums in the Pigeon file since they're shared
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