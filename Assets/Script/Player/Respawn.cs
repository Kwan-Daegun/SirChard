using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    private Vector3 startPosition;
    private Rigidbody rb;
    private PlayerMovement movementScript;

    void Start()
    {
        startPosition = transform.position;
        rb = GetComponent<Rigidbody>();
        movementScript = GetComponent<PlayerMovement>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Hazard"))
        {
            Respawn();
        }
    }

    public void Respawn()
    {
        // --- BALL LOGIC START ---
        // Find the ball in the scene. 
        // If the ball script is named 'BallScript', replace 'BallScript' below with that name.
        EnergyBall ball = FindFirstObjectByType<EnergyBall>();

        if (ball != null && ball.currentOwner == gameObject)
        {
            ball.DropBall();
        }
        // --- BALL LOGIC END ---

        PlayerManager playerManager = FindFirstObjectByType<PlayerManager>();
        if (playerManager != null)
        {
            playerManager.ApplyDeathPenalty(gameObject);
        }

        // Teleport Player
        rb.position = startPosition;
        transform.position = startPosition;

        // Reset Physics
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.rotation = Quaternion.identity;

        // Reset Movement Script States
        gameObject.SendMessage("ResetPlayerStates", SendMessageOptions.DontRequireReceiver);

        Debug.Log($"{gameObject.name} hit hazard, dropped ball, and respawned.");
    }
}
