using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace
{
    public class PigeonEatingSystem : MonoBehaviour
    {
        [Header("Food Spawning Settings")]
        [SerializeField] GameObject[] foodPrefabs = new GameObject[3];
        [SerializeField] string tableTag = "Table";
        [SerializeField] float spawnIntervalMin = 3f;
        [SerializeField] float spawnIntervalMax = 8f;
        [SerializeField] int maxFoodOnGround = 10;
        
        [Header("Food Drop Settings")]
        [SerializeField] float dropHeight = 1f;
        [SerializeField] float tableEdgeOffset = 0.5f;
        [SerializeField] Vector2 dropForceRange = new Vector2(0.1f, 0.3f); // Much smaller forces
        
        [Header("Audio & Effects")]
        [SerializeField] AudioClip[] foodDropSounds;
        [SerializeField] ParticleSystem dropEffect;
        
        // Internal state
        List<GameObject> activeTables = new List<GameObject>();
        List<GameObject> activeFoodItems = new List<GameObject>();
        bool isSpawning = false;
        Coroutine spawnCoroutine;
        AudioSource audioSource;
        
        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        void Start()
        {
            //Debug.Log("PigeonEatingSystem: Start() called");
            FindAllTables();
            CheckGroundColliders();
            //Debug.Log("PigeonEatingSystem: Start() completed");
        }
        
        void CheckGroundColliders()
        {
            // Check if there are objects tagged as "Ground" or "Terrain"
            GameObject[] groundObjects = GameObject.FindGameObjectsWithTag("Ground");
            if (groundObjects.Length == 0)
            {
                groundObjects = GameObject.FindGameObjectsWithTag("Terrain");
            }
            
            if (groundObjects.Length == 0)
            {
                //Debug.LogWarning("PigeonEatingSystem: No ground colliders found! Food may fall through the world. Make sure your ground/terrain has colliders and is tagged 'Ground' or 'Terrain'.");
                
                // Check for any objects named "Ground" or "Terrain" that aren't tagged
                GameObject[] allObjects = FindObjectsOfType<GameObject>();
                foreach (GameObject obj in allObjects)
                {
                    if (obj.name.ToLower().Contains("ground") || obj.name.ToLower().Contains("terrain") || obj.name.ToLower().Contains("plane"))
                    {
                        //Debug.LogWarning($"Found object '{obj.name}' that might be ground but isn't tagged! Tag it as 'Ground' or 'Terrain'");
                        
                        // Check if it has a collider
                        Collider col = obj.GetComponent<Collider>();
                        if (col == null)
                        {
                            //Debug.LogError($"Object '{obj.name}' has NO COLLIDER! Add a MeshCollider or BoxCollider component.");
                        }
                        else if (col.isTrigger)
                        {
                            //Debug.LogError($"Object '{obj.name}' collider is set to TRIGGER! Uncheck 'Is Trigger' in the collider component.");
                        }
                    }
                }
            }
            else
            {
                //Debug.Log($"PigeonEatingSystem: Found {groundObjects.Length} ground objects");
                
                // Check each ground object has proper collider setup
                foreach (GameObject ground in groundObjects)
                {
                    Collider col = ground.GetComponent<Collider>();
                    if (col == null)
                    {
                        //Debug.LogError($"Ground object '{ground.name}' has NO COLLIDER! Add a collider component.");
                    }
                    else if (col.isTrigger)
                    {
                        //Debug.LogError($"Ground object '{ground.name}' collider is set to TRIGGER! Uncheck 'Is Trigger' for physics collision.");
                    }
                    else
                    {
                        //Debug.Log($"Ground object '{ground.name}' has proper collider setup: {col.GetType().Name}");
                    }
                }
            }
        }
        
        public void StartFoodSpawning()
        {
            //Debug.Log($"StartFoodSpawning called: isSpawning={isSpawning}, spawnCoroutine={(spawnCoroutine != null ? "exists" : "null")}");
            
            if (!isSpawning)
            {
                isSpawning = true;
                spawnCoroutine = StartCoroutine(FoodSpawningCoroutine());
                //Debug.Log("PigeonEatingSystem: Food spawning started successfully");
            }
            else
            {
                //Debug.Log("PigeonEatingSystem: Food spawning already active");
            }
        }
        
        public void StopFoodSpawning()
        {
            if (isSpawning)
            {
                isSpawning = false;
                if (spawnCoroutine != null)
                {
                    StopCoroutine(spawnCoroutine);
                    spawnCoroutine = null;
                }
                //Debug.Log("PigeonEatingSystem: Food spawning stopped");
            }
        }
        
        void FindAllTables()
        {
            activeTables.Clear();
            GameObject[] foundTables = GameObject.FindGameObjectsWithTag(tableTag);
            activeTables.AddRange(foundTables);
            //Debug.Log($"PigeonEatingSystem: Found {activeTables.Count} tables");
        }
        
        IEnumerator FoodSpawningCoroutine()
        {
            //Debug.Log("FoodSpawningCoroutine started");
            
            while (isSpawning)
            {
                float waitTime = Random.Range(spawnIntervalMin, spawnIntervalMax);
                //Debug.Log($"Waiting {waitTime} seconds before next spawn attempt...");
                yield return new WaitForSeconds(waitTime);
                
                //Debug.Log($"Spawn check: activeFoodItems.Count={activeFoodItems.Count}, maxFoodOnGround={maxFoodOnGround}, activeTables.Count={activeTables.Count}");
                
                if (activeFoodItems.Count < maxFoodOnGround && activeTables.Count > 0)
                {
                    //Debug.Log("Conditions met, calling SpawnFoodFromRandomTable()");
                    SpawnFoodFromRandomTable();
                }
                else
                {
                    //Debug.Log("Spawn conditions not met - skipping spawn");
                }
            }
            
            //Debug.Log("FoodSpawningCoroutine ended");
        }
        
        void SpawnFoodFromRandomTable()
        {
            //Debug.Log("SpawnFoodFromRandomTable called");
            
            if (activeTables.Count == 0 || foodPrefabs.Length == 0) 
            {
                //Debug.LogWarning("Cannot spawn food: activeTables=" + activeTables.Count + ", foodPrefabs.Length=" + foodPrefabs.Length);
                return;
            }
            
            // Clean up null references from destroyed food
            activeFoodItems.RemoveAll(item => item == null);
            
            // Select random table and food type
            GameObject randomTable = activeTables[Random.Range(0, activeTables.Count)];
            GameObject randomFoodPrefab = GetRandomFoodPrefab();
            
            //Debug.Log("Selected table: " + (randomTable ? randomTable.name : "NULL"));
            //Debug.Log("Selected food prefab: " + (randomFoodPrefab ? randomFoodPrefab.name : "NULL"));
            
            if (randomFoodPrefab == null) 
            {
                //Debug.LogWarning("GetRandomFoodPrefab returned null");
                return;
            }
            
            // Calculate spawn position around table edge
            Vector3 spawnPosition = GetRandomTableEdgePosition(randomTable);
            
            // Instead of using table bounds Y (which might be wrong), use a more reliable method
            float tableVisualTop = randomTable.transform.position.y;
            
            // Check if the table has a renderer to get actual visual bounds
            Renderer tableRenderer = randomTable.GetComponent<Renderer>();
            if (tableRenderer != null)
            {
                tableVisualTop = tableRenderer.bounds.max.y;
                //Debug.Log($"Table '{randomTable.name}' visual top (renderer): {tableVisualTop}");
            }
            else
            {
                // Fallback: use transform Y + reasonable table height estimate
                tableVisualTop = randomTable.transform.position.y + 1f;
                //Debug.Log($"Table '{randomTable.name}' using transform Y + 1f: {tableVisualTop}");
            }
            
            // Set Y position to visual top + dropHeight for reliable spawning
            spawnPosition.y = tableVisualTop + dropHeight;
            
            //Debug.Log($"Final spawn position: {spawnPosition} (visual top {tableVisualTop} + {dropHeight} dropHeight)");
            
            // Safety check - make sure spawn position is reasonable
            if (spawnPosition.y > 50f || spawnPosition.y < 1f)
            {
                //Debug.LogWarning($"Suspicious spawn position: {spawnPosition}. Using safer position.");
                spawnPosition = randomTable.transform.position + Vector3.up * 3f; // 3 units above table center
                //Debug.Log($"Using safe spawn position: {spawnPosition}");
            }
            
            // Spawn the food
            GameObject newFood = Instantiate(randomFoodPrefab, spawnPosition, Random.rotation);
            
            // Add physics if not present
            EnsureFoodPhysics(newFood);
            
            // Add to tracking list
            activeFoodItems.Add(newFood);
            
            // Play audio and effects
            PlayDropEffects(spawnPosition);
            
            //Debug.Log($"PigeonEatingSystem: Successfully spawned {randomFoodPrefab.name} from {randomTable.name} at {spawnPosition}");
        }
        
        GameObject GetRandomFoodPrefab()
        {
            List<GameObject> validPrefabs = new List<GameObject>();
            
            //Debug.Log($"Checking {foodPrefabs.Length} food prefabs:");
            for (int i = 0; i < foodPrefabs.Length; i++)
            {
                //Debug.Log($"  foodPrefabs[{i}]: {(foodPrefabs[i] ? foodPrefabs[i].name : "NULL")}");
                if (foodPrefabs[i] != null)
                {
                    validPrefabs.Add(foodPrefabs[i]);
                }
            }
            
            //Debug.Log($"Found {validPrefabs.Count} valid food prefabs");
            
            if (validPrefabs.Count == 0) 
            {
                //Debug.LogError("No valid food prefabs found! Make sure to assign food prefabs in the inspector.");
                return null;
            }
            
            GameObject selected = validPrefabs[Random.Range(0, validPrefabs.Count)];
            //Debug.Log($"Selected food prefab: {selected.name}");
            return selected;
        }
        
        Vector3 GetRandomTableEdgePosition(GameObject table)
        {
            Collider tableCollider = table.GetComponent<Collider>();
            Vector3 tableCenter = table.transform.position;
            
            //Debug.Log($"Table '{table.name}' position: {tableCenter}");
            
            if (tableCollider != null)
            {
                Bounds bounds = tableCollider.bounds;
                //Debug.Log($"Table bounds: min={bounds.min}, max={bounds.max}, center={bounds.center}");
                
                // Pick a random edge (0=front, 1=back, 2=left, 3=right)
                int edge = Random.Range(0, 4);
                Vector3 position = tableCenter;
                
                switch (edge)
                {
                    case 0: // Front
                        position.z = bounds.min.z - tableEdgeOffset;
                        position.x = Random.Range(bounds.min.x, bounds.max.x);
                        break;
                    case 1: // Back
                        position.z = bounds.max.z + tableEdgeOffset;
                        position.x = Random.Range(bounds.min.x, bounds.max.x);
                        break;
                    case 2: // Left
                        position.x = bounds.min.x - tableEdgeOffset;
                        position.z = Random.Range(bounds.min.z, bounds.max.z);
                        break;
                    case 3: // Right
                        position.x = bounds.max.x + tableEdgeOffset;
                        position.z = Random.Range(bounds.min.z, bounds.max.z);
                        break;
                }
                
                // Use the TOP of the table bounds, but ensure it's reasonable
                position.y = bounds.max.y;
                
                // Safety check - make sure Y position is above ground
                if (position.y < 0.1f)
                {
                    //Debug.LogWarning($"Table Y position too low ({position.y}), using table transform Y instead");
                    position.y = tableCenter.y + 0.5f; // Use table center + offset
                }
                
                //Debug.Log($"Edge position before dropHeight: {position}");
                return position;
            }
            
            // Fallback if no collider - use table center with safe offset
            Vector3 randomOffset = Random.insideUnitCircle * tableEdgeOffset;
            Vector3 fallbackPos = tableCenter + new Vector3(randomOffset.x, 1f, randomOffset.y); // 1f above table center
            //Debug.Log($"Using fallback position: {fallbackPos}");
            return fallbackPos;
        }
        
        void EnsureFoodPhysics(GameObject food)
        {
            // Ensure food has Rigidbody for physics
            Rigidbody rb = food.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = food.AddComponent<Rigidbody>();
            }
            
            // Configure Rigidbody for realistic food behavior
            rb.mass = 0.1f;
            rb.linearDamping = 1f;      // Air resistance
            rb.angularDamping = 3f;     // Rotation resistance
            rb.useGravity = true;       // Make sure gravity is enabled
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // Better collision detection
            
            // Handle existing colliders - remove and replace with proper SphereCollider
            Collider[] existingColliders = food.GetComponents<Collider>();
            foreach (Collider existingCol in existingColliders)
            {
                //Debug.Log($"Removing existing {existingCol.GetType().Name} collider from food");
                DestroyImmediate(existingCol);
            }
            
            // Add a fresh SphereCollider for reliable collision
            SphereCollider sphereCol = food.AddComponent<SphereCollider>();
            sphereCol.radius = 0.1f; // Good size for collision detection
            sphereCol.isTrigger = false; // Must be false for physics collision
            
            // Set up physics material for realistic bouncing
            sphereCol.material = CreateBouncyMaterial();
            
            // Ensure food has proper tag
            if (!food.CompareTag("Food"))
            {
                food.tag = "Food";
            }
            
            // Add very small random force for realistic drop
            Vector3 randomForce = new Vector3(
                Random.Range(-0.1f, 0.1f),  // Even smaller random force
                0,                          // No upward force
                Random.Range(-0.1f, 0.1f)   // Even smaller random force
            );
            
            rb.AddForce(randomForce, ForceMode.Impulse);
            
            // Add collision debugging component (temporary)
            //food.AddComponent<FoodCollisionDebugger>();
            
            //Debug.Log($"Food physics configured: mass={rb.mass}, gravity={rb.useGravity}, collider=SphereCollider, radius={sphereCol.radius}, isTrigger={sphereCol.isTrigger}");
        }
        
        PhysicsMaterial CreateBouncyMaterial()
        {
            PhysicsMaterial material = new PhysicsMaterial("FoodMaterial");
            material.bounciness = 0.1f;          // Less bouncy (food shouldn't bounce much)
            material.dynamicFriction = 0.8f;     // More friction when moving
            material.staticFriction = 0.9f;      // High static friction (food should stay put)
            material.bounceCombine = PhysicsMaterialCombine.Minimum;
            material.frictionCombine = PhysicsMaterialCombine.Maximum;
            return material;
        }
        
        void PlayDropEffects(Vector3 position)
        {
            // Play random drop sound
            if (audioSource != null && foodDropSounds != null && foodDropSounds.Length > 0)
            {
                AudioClip randomSound = foodDropSounds[Random.Range(0, foodDropSounds.Length)];
                if (randomSound != null)
                {
                    audioSource.PlayOneShot(randomSound, 0.5f);
                }
            }
            
            // Play particle effect
            if (dropEffect != null)
            {
                dropEffect.transform.position = position;
                dropEffect.Play();
            }
        }
        
        // Public methods for external use
        public void RemoveFoodItem(GameObject food)
        {
            if (activeFoodItems.Contains(food))
            {
                activeFoodItems.Remove(food);
            }
        }
        
        public int GetActiveFoodCount()
        {
            activeFoodItems.RemoveAll(item => item == null);
            return activeFoodItems.Count;
        }
        
        public List<GameObject> GetNearbyFood(Vector3 position, float radius)
        {
            List<GameObject> nearbyFood = new List<GameObject>();
            
            foreach (GameObject food in activeFoodItems)
            {
                if (food != null && Vector3.Distance(position, food.transform.position) <= radius)
                {
                    nearbyFood.Add(food);
                }
            }
            
            return nearbyFood;
        }
        
        public void ClearAllFood()
        {
            foreach (GameObject food in activeFoodItems)
            {
                if (food != null)
                {
                    Destroy(food);
                }
            }
            activeFoodItems.Clear();
        }
        
        // Debug methods
        public void RefreshTables()
        {
            FindAllTables();
        }
        
        public void ManualSpawnFood()
        {
            //Debug.Log("Manual spawn food called");
            SpawnFoodFromRandomTable();
        }
        
        void OnDrawGizmosSelected()
        {
            // Visualize table spawn areas
            if (activeTables != null)
            {
                Gizmos.color = Color.yellow;
                foreach (GameObject table in activeTables)
                {
                    if (table != null)
                    {
                        Collider tableCollider = table.GetComponent<Collider>();
                        if (tableCollider != null)
                        {
                            Bounds bounds = tableCollider.bounds;
                            Vector3 size = bounds.size + Vector3.one * (tableEdgeOffset * 2);
                            Gizmos.DrawWireCube(bounds.center, size);
                        }
                    }
                }
            }
        }
    }
}
