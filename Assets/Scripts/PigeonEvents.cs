using System;
using UnityEngine;

namespace DefaultNamespace
{
    /// <summary>
    /// Event system for Pigeon component communication
    /// </summary>
    public class PigeonEvents : MonoBehaviour
    {
        [Header("Event Settings")]
        [SerializeField] bool logEvents = false;

        // Static events for global listening (useful for UI, camera, etc.)
        public static event Action<Pigeon, PigeonStateChangeArgs> OnAnyPigeonStateChanged;
        public static event Action<Pigeon, PigeonAnimationArgs> OnAnyPigeonAnimationChanged;
        public static event Action<Pigeon, PigeonMovementArgs> OnAnyPigeonMovementChanged;
        public static event Action<Pigeon, PigeonEatingArgs> OnAnyPigeonEatingEvent;

        // Instance events for this specific pigeon
        public event Action<PigeonStateChangeArgs> OnStateChanged;
        public event Action<PigeonAnimationArgs> OnAnimationChanged;
        public event Action<PigeonMovementArgs> OnMovementChanged;
        public event Action<PigeonEatingArgs> OnEatingEvent;

        // Reference to the pigeon this belongs to
        Pigeon pigeon;

        void Awake()
        {
            pigeon = GetComponent<Pigeon>();
            if (pigeon == null)
            {
                Debug.LogError($"PigeonEvents on {gameObject.name} requires a Pigeon component!");
            }
        }

        #region State Change Events
        
        public void TriggerStateChanged(PigeonState oldState, PigeonState newState, float timestamp = 0f)
        {
            if (timestamp <= 0f) timestamp = Time.time;
            
            var args = new PigeonStateChangeArgs
            {
                OldState = oldState,
                NewState = newState,
                Timestamp = timestamp,
                Position = transform.position
            };

            if (logEvents)
                Debug.Log($"[{gameObject.name}] State: {oldState} → {newState}");

            OnStateChanged?.Invoke(args);
            OnAnyPigeonStateChanged?.Invoke(pigeon, args);
        }

        #endregion

        #region Animation Events

        public void TriggerAnimationChanged(string oldAnimation, string newAnimation, float timestamp = 0f)
        {
            if (timestamp <= 0f) timestamp = Time.time;
            
            var args = new PigeonAnimationArgs
            {
                OldAnimation = oldAnimation,
                NewAnimation = newAnimation,
                Timestamp = timestamp,
                Position = transform.position
            };

            if (logEvents)
                Debug.Log($"[{gameObject.name}] Animation: {oldAnimation} → {newAnimation}");

            OnAnimationChanged?.Invoke(args);
            OnAnyPigeonAnimationChanged?.Invoke(pigeon, args);
        }

        #endregion

        #region Movement Events

        public void TriggerMovementChanged(bool isMoving, bool isRunning, bool isFlying, float speed = 0f)
        {
            /*var args = new PigeonMovementArgs
            {
                IsMoving = isMoving,
                IsRunning = isRunning,
                IsFlying = isFlying,
                Speed = speed,
                Timestamp = Time.time,
                Position = transform.position,
                Velocity = pigeon != null && pigeon.IsPlayerControlled ? 
                          pigeon.GetComponent<CharacterController>()?.velocity ?? Vector3.zero :
                          pigeon?.GetComponent<UnityEngine.AI.NavMeshAgent>()?.velocity ?? Vector3.zero
            };

            if (logEvents && args.IsMoving)
                Debug.Log($"[{gameObject.name}] Movement: Moving={isMoving}, Running={isRunning}, Flying={isFlying}, Speed={speed:F1}");

            OnMovementChanged?.Invoke(args);
            OnAnyPigeonMovementChanged?.Invoke(pigeon, args);*/
        }

        #endregion

        #region Eating Events

        public void TriggerEatingEvent(EatingEventType eventType, GameObject food = null)
        {
            var args = new PigeonEatingArgs
            {
                EventType = eventType,
                Food = food,
                Timestamp = Time.time,
                Position = transform.position,
                BeakPosition = pigeon != null ? pigeon.GetBeakPosition() : transform.position
            };

            if (logEvents)
                Debug.Log($"[{gameObject.name}] Eating: {eventType}" + (food ? $" (food: {food.name})" : ""));

            OnEatingEvent?.Invoke(args);
            OnAnyPigeonEatingEvent?.Invoke(pigeon, args);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Check if this is the player pigeon
        /// </summary>
        //public bool IsPlayerPigeon => pigeon != null && pigeon.IsPlayerControlled;

        /// <summary>
        /// Get the current pigeon state
        /// </summary>
        public PigeonState CurrentState => pigeon != null ? pigeon.GetCurrentState() : PigeonState.Wandering;

        /// <summary>
        /// Get the current animation
        /// </summary>
        //public string CurrentAnimation => pigeon != null ? pigeon.GetCurrentAnimation() : "";

        #endregion

        void OnDestroy()
        {
            // Clean up to prevent memory leaks
            OnStateChanged = null;
            OnAnimationChanged = null;
            OnMovementChanged = null;
            OnEatingEvent = null;
        }
    }

    #region Event Argument Classes

    [System.Serializable]
    public class PigeonStateChangeArgs
    {
        public PigeonState OldState;
        public PigeonState NewState;
        public float Timestamp;
        public Vector3 Position;
    }

    [System.Serializable]
    public class PigeonAnimationArgs
    {
        public string OldAnimation;
        public string NewAnimation;
        public float Timestamp;
        public Vector3 Position;
    }

    [System.Serializable]
    public class PigeonMovementArgs
    {
        public bool IsMoving;
        public bool IsRunning;
        public bool IsFlying;
        public float Speed;
        public float Timestamp;
        public Vector3 Position;
        public Vector3 Velocity;
    }

    [System.Serializable]
    public class PigeonEatingArgs
    {
        public EatingEventType EventType;
        public GameObject Food;
        public float Timestamp;
        public Vector3 Position;
        public Vector3 BeakPosition;
    }

    #endregion

    #region Enums

    public enum EatingEventType
    {
        StartedEating,
        FinishedEating,
        FoodDetected,
        FoodLost,
        CompetingForFood
    }

    #endregion
}