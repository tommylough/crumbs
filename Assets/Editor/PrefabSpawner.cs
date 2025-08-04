using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace beezout.Editor
{
    public class PrefabSpawnerEditor : EditorWindow
    {
        private GameObject prefabToSpawn;
        private int spawnCount = 500;
        private float minOverlapDistance = 0.5f;
        
        private float smallestScale = 0.1f;
        private float largestScale = 1f;

        [MenuItem("Tools/Prefab Spawner")]
        public static void ShowWindow()
        {
            GetWindow<PrefabSpawnerEditor>("Prefab Spawner");
        }

        private void OnGUI()
        {
            GUILayout.Label("Prefab Spawner", EditorStyles.boldLabel);

            // Select Prefab
            prefabToSpawn =
                (GameObject)EditorGUILayout.ObjectField("Prefab:", prefabToSpawn, typeof(GameObject), false);

            // Spawn Count
            spawnCount = EditorGUILayout.IntField("Spawn Count:", spawnCount);
            minOverlapDistance = EditorGUILayout.FloatField("Overlap:", minOverlapDistance);
            smallestScale = EditorGUILayout.FloatField("Smallest Scale:", smallestScale);
            largestScale = EditorGUILayout.FloatField("Largest Scale:", largestScale);

            GUILayout.Space(10);

            // Button to Spawn Prefabs
            if (GUILayout.Button("Spawn Prefabs"))
            {
                CreateRocks();
            }

            // Button to Destroy Prefabs 
            if (GUILayout.Button("Destroy Prefabs"))
            {
                DestroyRocks();
            }
        }

        private void CreateRocks()
        {
            if (prefabToSpawn == null || Selection.activeGameObject == null)
            {
                Debug.LogWarning("Please select a prefab and a parent GameObject in the hierarchy.");
                return;
            }

            Transform parent = Selection.activeGameObject.transform;
            Renderer parentRenderer = parent.GetComponent<Renderer>();

            if (parentRenderer == null)
            {
                Debug.LogWarning("Selected object has no Renderer. Using transform scale as bounds.");
                CreateRocksUsingScale();
                return;
            }

            Bounds bounds = parentRenderer.bounds;
            float minX = bounds.min.x;
            float maxX = bounds.max.x;
            float minZ = bounds.min.z;
            float maxZ = bounds.max.z;

            float minDistance = 0.2f; // Reduce to allow more objects to fit
            int maxAttempts = 50; // Increase so it retries more times

            List<Vector3> spawnPositions = new List<Vector3>();

            for (int i = 0; i < spawnCount; i++)
            {
                Vector3 randomPosition;
                int attempts = 0;
                bool validPosition = false;

                do
                {
                    float randomX = Random.Range(minX, maxX);
                    float randomZ = Random.Range(minZ, maxZ);
                    randomPosition = new Vector3(randomX, bounds.min.y, randomZ);

                    // Check if the position is far enough from existing ones
                    validPosition = !spawnPositions.Exists(pos => Vector3.Distance(pos, randomPosition) < minDistance);
                    attempts++;
                } while (!validPosition && attempts < maxAttempts);

                // If too many failed attempts, place anyway
                if (!validPosition)
                {
                    Debug.LogWarning($"Couldn't find a valid spot for object {i}. Placing it anyway.");
                }

                spawnPositions.Add(randomPosition);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabToSpawn);
                if (instance == null) return;

                instance.transform.parent = parent;
                instance.transform.position = randomPosition;
                instance.transform.localScale = Vector3.one * Random.Range(smallestScale, largestScale);
                instance.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            }
        }

        /// <summary>
        /// Fallback method: If no Renderer is found, use transform scale to determine spawn bounds.
        /// </summary>
        private void CreateRocksUsingScale()
        {
            Transform parent = Selection.activeGameObject.transform;

            Vector3 center = parent.position;
            Vector3 size = parent.localScale;

            float minX = -20;//center.x - size.x / 2;
            float maxX = 20;//center.x + size.x / 2;
            float minZ = -20;//center.z - size.z / 2;
            float maxZ = 20;//center.z + size.z / 2;

            float minDistance = 0.5f;
            List<Vector3> spawnPositions = new List<Vector3>();
            int maxAttempts = 10;

            for (int i = 0; i < spawnCount; i++)
            {
                Vector3 randomPosition;
                int attempts = 0;

                do
                {
                    float randomX = Random.Range(minX, maxX);
                    float randomZ = Random.Range(minZ, maxZ);
                    randomPosition = new Vector3(randomX, parent.position.y, randomZ);
                    attempts++;
                } while (spawnPositions.Exists(pos => Vector3.Distance(pos, randomPosition) < minDistance) &&
                         attempts < maxAttempts);

                if (attempts < maxAttempts)
                {
                    spawnPositions.Add(randomPosition);
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabToSpawn);
                    if (instance == null) return;

                    instance.transform.parent = parent;
                    instance.transform.position = randomPosition;
                    instance.transform.localScale = Vector3.one * Random.Range(smallestScale, largestScale);
                    instance.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                }
            }
        }

        private void DestroyRocks()
        {
            if (Selection.activeGameObject == null)
            {
                Debug.LogWarning("Please select a parent GameObject in the hierarchy.");
                return;
            }

            Transform parent = Selection.activeGameObject.transform;

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (child.name.Contains(prefabToSpawn.name))
                {
                    DestroyImmediate(child);
                }
            }
        }
    }
}