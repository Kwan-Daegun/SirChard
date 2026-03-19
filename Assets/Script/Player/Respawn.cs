using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    private Vector3 startPosition;
    private Rigidbody rb;
    private PlayerMovement movementScript;

    void Start()
    {
        // Record the unique spawn point for this specific player
        startPosition = transform.position;
        rb = GetComponent<Rigidbody>();
        movementScript = GetComponent<PlayerMovement>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if we hit the hazard
        if (other.CompareTag("Hazard"))
        {
            Respawn();
        }
    }

    public void Respawn()
    {
        // 1. Move the player (Resetting Rigidbody position is best for physics)
        rb.position = startPosition;
        transform.position = startPosition;

        // 2. Clear all physics momentum (prevents sliding after teleport)
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 3. Force rotation back to upright
        transform.rotation = Quaternion.identity;
        rb.freezeRotation = true;

        // 4. Reset your PlayerMovement states
        if (movementScript != null)
        {
            // Use SendMessage to reset the private booleans in your movement script
            gameObject.SendMessage("ResetPlayerStates", SendMessageOptions.DontRequireReceiver);
        }

        Debug.Log($"{gameObject.name} (Tag: {gameObject.tag}) respawned.");
    }
}