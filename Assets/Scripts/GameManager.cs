using UnityEngine;

namespace DefaultNamespace
{
    public class GameManager : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] PigeonEatingSystem eatingSystem;
        
        [Header("Game Controls")]
        [SerializeField] bool startFoodSpawningOnStart = true;
        [SerializeField] KeyCode toggleFoodSpawningKey = KeyCode.F;
        
        bool isFoodSpawningActive = false;
        
        void Start()
        {
            //Debug.Log("GameManager Start() called");
            
            // Find eating system if not assigned
            if (eatingSystem == null)
            {
                eatingSystem = FindFirstObjectByType<PigeonEatingSystem>();
                //Debug.Log("GameManager: Found eating system: " + (eatingSystem ? eatingSystem.name : "NULL"));
            }
            
            if (eatingSystem == null)
            {
                //Debug.LogError("GameManager: PigeonEatingSystem not found!");
                return;
            }
            
            // Start food spawning if requested
            if (startFoodSpawningOnStart)
            {
                //Debug.Log("GameManager: Auto-starting food spawning");
                StartFoodSpawning();
            }
            else
            {
                //Debug.Log("GameManager: Not auto-starting food spawning (startFoodSpawningOnStart = false)");
            }
        }
        
        void Update()
        {
            // Handle debug input
            if (Input.GetKeyDown(toggleFoodSpawningKey))
            {
                ToggleFoodSpawning();
            }
            
            // Manual spawn for testing
            if (Input.GetKeyDown(KeyCode.G))
            {
                //Debug.Log("Manual spawn triggered (G key)");
                if (eatingSystem != null)
                {
                    eatingSystem.ManualSpawnFood();
                }
            }
            
            // Clear all food for testing
            if (Input.GetKeyDown(KeyCode.C))
            {
                //Debug.Log("Clear all food triggered (C key)");
                if (eatingSystem != null)
                {
                    eatingSystem.ClearAllFood();
                }
            }
        }
        
        public void StartFoodSpawning()
        {
            if (eatingSystem != null && !isFoodSpawningActive)
            {
                eatingSystem.StartFoodSpawning();
                isFoodSpawningActive = true;
                //Debug.Log("GameManager: Food spawning started");
            }
        }
        
        public void StopFoodSpawning()
        {
            if (eatingSystem != null && isFoodSpawningActive)
            {
                eatingSystem.StopFoodSpawning();
                isFoodSpawningActive = false;
                //Debug.Log("GameManager: Food spawning stopped");
            }
        }
        
        public void ToggleFoodSpawning()
        {
            if (isFoodSpawningActive)
            {
                StopFoodSpawning();
            }
            else
            {
                StartFoodSpawning();
            }
        }
        
        public void ClearAllFood()
        {
            if (eatingSystem != null)
            {
                eatingSystem.ClearAllFood();
                //Debug.Log("GameManager: All food cleared");
            }
        }
        
        // Public getters for UI or other systems
        public bool IsFoodSpawningActive() => isFoodSpawningActive;
        public int GetActiveFoodCount() => eatingSystem != null ? eatingSystem.GetActiveFoodCount() : 0;
        
        // Debug methods
        void OnGUI()
        {
            if (eatingSystem == null) return;
            
            // Simple debug UI in top-left corner
            GUILayout.BeginArea(new Rect(10, 10, 250, 140));
            GUILayout.Label($"Food Spawning: {(isFoodSpawningActive ? "ON" : "OFF")}");
            GUILayout.Label($"Active Food: {eatingSystem.GetActiveFoodCount()}");
            GUILayout.Label($"Press {toggleFoodSpawningKey} to toggle");
            GUILayout.Label("Press G for manual spawn");
            GUILayout.Label("Press C to clear all food");
            GUILayout.EndArea();
        }
    }
}
