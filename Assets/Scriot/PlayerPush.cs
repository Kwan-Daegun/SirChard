using UnityEngine;

public class PlayerPush : MonoBehaviour
{
    public float pushForce = 8f;
    public float pushRange = 2f;
    public float stunDuration = 2f;

    public LayerMask playerLayer;

    public void TryPush()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, pushRange, playerLayer);

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject)
                continue;

            Rigidbody rb = hit.GetComponent<Rigidbody>();
            PlayerMovement movement = hit.GetComponent<PlayerMovement>();

            if (rb != null && movement != null)
            {
                Vector3 dir = (hit.transform.position - transform.position).normalized;

                rb.AddForce(dir * pushForce, ForceMode.Impulse);
                movement.ApplyPush(stunDuration);
            }
        }
    }
}