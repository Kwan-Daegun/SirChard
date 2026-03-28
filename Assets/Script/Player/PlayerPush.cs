using UnityEngine;

public class PlayerPush : MonoBehaviour
{
    public float pushForce = 8f;
    public float pushRange = 2f;
    public float stunDuration = 2f;
    public float pushCooldown = 0.5f;
    public float pushAngle = 60f;
    public float coneLength = 1f;

    public LayerMask playerLayer;

    private float lastPushTime = 0f;

    public void TryPush()
    {
        if (GameState.IsGameplayLocked) return;

        if (Time.time - lastPushTime < pushCooldown)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, pushRange, playerLayer);

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject)
                continue;

            Vector3 toTarget = hit.transform.position - transform.position;
            Vector3 dirToTarget = toTarget.normalized;

            float angle = Vector3.Angle(transform.forward, dirToTarget);
            if (angle > pushAngle)
                continue;

            float forwardDist = Vector3.Dot(transform.forward, toTarget);
            if (forwardDist < 0f || forwardDist > coneLength)
                continue;

            Rigidbody rb = hit.GetComponent<Rigidbody>();
            PlayerMovement movement = hit.GetComponent<PlayerMovement>();

            if (rb != null && movement != null)
            {
                rb.AddForce(transform.forward * pushForce, ForceMode.Impulse);
                movement.ApplyPush(stunDuration);
            }
        }

        lastPushTime = Time.time;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.7f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, pushRange);

        // draw cone edges
        Vector3 leftDir = Quaternion.Euler(0f, -pushAngle, 0f) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0f, pushAngle, 0f) * transform.forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + leftDir * coneLength);
        Gizmos.DrawLine(transform.position, transform.position + rightDir * coneLength);

        Gizmos.DrawWireSphere(transform.position + transform.forward * coneLength, 0.05f);

        // simple arc removed; visual shows only edges and range
    }
}