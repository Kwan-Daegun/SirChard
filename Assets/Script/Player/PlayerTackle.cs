using System.Collections;
using UnityEngine;

public class PlayerTackle : MonoBehaviour
{
    public float tacklePower = 20f;
    public float tackleDuration = 0.3f;
    public float tackleCooldown = 1.5f;
    public float tackleStunOnHit = 2f;
    public float tackleRadius = 1.5f;
    public LayerMask playerLayer;

    private Rigidbody rb;
    private float lastTackleTime = 0f;

    public bool IsTackling { get; private set; }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void TryTackle()
    {
        if (Time.time - lastTackleTime < tackleCooldown || IsTackling)
            return;

        StartCoroutine(PerformTackle());
    }

    private IEnumerator PerformTackle()
    {
        IsTackling = true;
        lastTackleTime = Time.time;

        Vector3 tackleDir = transform.forward;
        rb.linearVelocity = tackleDir * tacklePower;

        float timer = 0;
        bool hitSomething = false;

        while (timer < tackleDuration)
        {
            timer += Time.deltaTime;

            if (!hitSomething)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward, tackleRadius, playerLayer);
                foreach (Collider hit in hits)
                {
                    if (hit.gameObject == gameObject) continue;

                    PlayerMovement victim = hit.GetComponent<PlayerMovement>();
                    Rigidbody victimRb = hit.GetComponent<Rigidbody>();

                    if (victim != null && victimRb != null)
                    {
                        victimRb.AddForce(transform.forward * (tacklePower * 0.5f), ForceMode.Impulse);
                        victim.ApplyPush(tackleStunOnHit);

                        EnergyBall[] allBalls = FindObjectsOfType<EnergyBall>();
                        foreach (EnergyBall ball in allBalls)
                        {
                            if (ball.currentOwner != null)
                            {
                                if (ball.transform.IsChildOf(victim.transform) ||
                                    ball.currentOwner == victim.gameObject ||
                                    ball.currentOwner.transform.IsChildOf(victim.transform))
                                {
                                    ball.DropBall();
                                }
                            }
                        }

                        hitSomething = true;
                        break;
                    }
                }
            }
            yield return null;
        }

        IsTackling = false;
    }
}