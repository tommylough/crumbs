using UnityEngine;

namespace DefaultNamespace
{
    public class FoodCollisionDebugger : MonoBehaviour
    {
        void OnCollisionEnter(Collision collision)
        {
            Debug.Log($"Food {gameObject.name} collided with {collision.gameObject.name} (tag: {collision.gameObject.tag})");
        }
        
        void OnTriggerEnter(Collider other)
        {
            Debug.Log($"Food {gameObject.name} triggered with {other.gameObject.name} (tag: {other.gameObject.tag})");
        }
        
        void Start()
        {
            // Auto-destroy this component after 10 seconds to avoid spam
            Destroy(this, 10f);
        }
    }
}
