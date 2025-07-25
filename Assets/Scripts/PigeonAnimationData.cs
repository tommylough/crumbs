using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace
{
    [CreateAssetMenu(fileName = "PigeonAnimationData", menuName = "Pigeon/Animation Data")]
    public class PigeonAnimationData : ScriptableObject
    {
        [Header("Available Animations")]
        [SerializeField] List<string> allAnimations = new List<string>
        {
            "Attack",
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

        [Header("Available Shape Keys")]
        [SerializeField] List<string> allShapeKeys = new List<string>
        {
            "Eyes_Annoyed",
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

        [Header("Idle Animation Settings")]
        [SerializeField] List<string> idleAnimations = new List<string> { "Idle_A", "Idle_B", "Idle_C" };
        [SerializeField] float idleAnimationChangeInterval = 3f;
        [SerializeField] bool useRandomIdleAnimations = true;

        [Header("Movement Animation Mapping")]
        [SerializeField] string walkAnimation = "Walk";
        [SerializeField] string runAnimation = "Run";
        [SerializeField] string flyAnimation = "Fly";
        [SerializeField] string eatAnimation = "Eat";
        [SerializeField] string attackAnimation = "Attack";

        [Header("Debug Animation Keys")]
        [SerializeField] List<DebugAnimationKey> debugKeys = new List<DebugAnimationKey>
        {
            new DebugAnimationKey { keyCode = KeyCode.Alpha1, animationName = "Eat" },
            new DebugAnimationKey { keyCode = KeyCode.Alpha2, animationName = "Attack" },
            new DebugAnimationKey { keyCode = KeyCode.Alpha3, animationName = "Jump" },
            new DebugAnimationKey { keyCode = KeyCode.Alpha4, animationName = "Sit" },
            new DebugAnimationKey { keyCode = KeyCode.Alpha5, animationName = "Fear" }
        };

        [Header("Debug Shape Key Keys")]
        [SerializeField] List<DebugShapeKey> debugShapeKeys = new List<DebugShapeKey>
        {
            new DebugShapeKey { keyCode = KeyCode.B, shapeKeyName = "Eyes_Blink" },
            new DebugShapeKey { keyCode = KeyCode.H, shapeKeyName = "Eyes_Happy" },
            new DebugShapeKey { keyCode = KeyCode.S, shapeKeyName = "Eyes_Sad" }
        };

        // Public read-only access
        public IReadOnlyList<string> AllAnimations => allAnimations;
        public IReadOnlyList<string> AllShapeKeys => allShapeKeys;
        public IReadOnlyList<string> IdleAnimations => idleAnimations;
        public IReadOnlyList<DebugAnimationKey> DebugKeys => debugKeys;
        public IReadOnlyList<DebugShapeKey> DebugShapeKeys => debugShapeKeys;

        // Settings properties
        public float IdleAnimationChangeInterval => idleAnimationChangeInterval;
        public bool UseRandomIdleAnimations => useRandomIdleAnimations;

        // Animation name getters
        public string WalkAnimation => walkAnimation;
        public string RunAnimation => runAnimation;
        public string FlyAnimation => flyAnimation;
        public string EatAnimation => eatAnimation;
        public string AttackAnimation => attackAnimation;

        // Utility methods
        public bool IsValidAnimation(string animationName)
        {
            return allAnimations.Contains(animationName);
        }

        public bool IsValidShapeKey(string shapeKeyName)
        {
            return allShapeKeys.Contains(shapeKeyName);
        }

        public bool IsIdleAnimation(string animationName)
        {
            return idleAnimations.Contains(animationName);
        }

        public string GetRandomIdleAnimation()
        {
            if (idleAnimations.Count == 0) return "Idle_A";
            return idleAnimations[Random.Range(0, idleAnimations.Count)];
        }

        public string GetAnimationForMovementState(MovementState state, bool isRunning = false)
        {
            switch (state)
            {
                case MovementState.Walking:
                    return isRunning ? runAnimation : walkAnimation;
                case MovementState.Flying:
                    return flyAnimation;
                case MovementState.Eating:
                    return eatAnimation;
                case MovementState.Attacking:
                    return attackAnimation;
                case MovementState.Idle:
                default:
                    return GetRandomIdleAnimation();
            }
        }

        // Validation in editor
        void OnValidate()
        {
            // Ensure idle animations are valid
            for (int i = idleAnimations.Count - 1; i >= 0; i--)
            {
                if (!allAnimations.Contains(idleAnimations[i]))
                {
                    Debug.LogWarning($"Idle animation '{idleAnimations[i]}' not found in all animations list!");
                }
            }

            // Ensure movement animations are valid
            if (!string.IsNullOrEmpty(walkAnimation) && !allAnimations.Contains(walkAnimation))
                Debug.LogWarning($"Walk animation '{walkAnimation}' not found in all animations list!");
            
            if (!string.IsNullOrEmpty(runAnimation) && !allAnimations.Contains(runAnimation))
                Debug.LogWarning($"Run animation '{runAnimation}' not found in all animations list!");
            
            if (!string.IsNullOrEmpty(flyAnimation) && !allAnimations.Contains(flyAnimation))
                Debug.LogWarning($"Fly animation '{flyAnimation}' not found in all animations list!");
            
            if (!string.IsNullOrEmpty(eatAnimation) && !allAnimations.Contains(eatAnimation))
                Debug.LogWarning($"Eat animation '{eatAnimation}' not found in all animations list!");
            
            if (!string.IsNullOrEmpty(attackAnimation) && !allAnimations.Contains(attackAnimation))
                Debug.LogWarning($"Attack animation '{attackAnimation}' not found in all animations list!");
        }
    }

    [System.Serializable]
    public class DebugAnimationKey
    {
        public KeyCode keyCode;
        public string animationName;
    }

    [System.Serializable]
    public class DebugShapeKey
    {
        public KeyCode keyCode;
        public string shapeKeyName;
    }

    public enum MovementState
    {
        Idle,
        Walking,
        Flying,
        Eating,
        Attacking
    }
}