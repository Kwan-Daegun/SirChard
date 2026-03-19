using System.Collections;
using UnityEngine;

// ============================================================
//  PlayerTackle — with PlayerVisuals hooks added.
//  Replace your existing PlayerTackle.cs with this.
//  Only 4 lines were added (marked with // ++ VISUALS)
// ============================================================

public class PlayerTackle : MonoBehaviour
{
    public float tacklePower = 20f;
    public float tackleDuration = 0.3f;
    public float tackleCooldown = 1.5f;
    public float tackleStunOnHit = 2f;
    public float tackleRadius = 1.5f;
    public LayerMask playerLayer;

    private Rigidbody _rb;
    private float _lastTackleTime;
    private PlayerVisuals _visuals; // ++ VISUALS

    public bool IsTackling { get; private set; }

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _visuals = GetComponent<PlayerVisuals>(); // ++ VISUALS
    }

    public void TryTackle()
    {
        if (Time.time - _lastTackleTime < tackleCooldown || IsTackling) return;
        StartCoroutine(PerformTackle());
    }

    private IEnumerator PerformTackle()
    {
        IsTackling = true;
        _lastTackleTime = Time.time;

        _visuals?.OnTackleStart(); // ++ VISUALS

        Vector3 tackleDir = transform.forward;
        _rb.linearVelocity = tackleDir * tacklePower;

        float timer = 0f;
        bool hitSomething = false;

        while (timer < tackleDuration)
        {
            timer += Time.deltaTime;

            if (!hitSomething)
            {
                Collider[] hits = Physics.OverlapSphere(
                    transform.position + transform.forward, tackleRadius, playerLayer);

                foreach (Collider hit in hits)
                {
                    if (hit.gameObject == gameObject) continue;

                    var victim = hit.GetComponent<PlayerMovement>();
                    var victimRb = hit.GetComponent<Rigidbody>();

                    if (victim != null && victimRb != null)
                    {
                        victimRb.AddForce(transform.forward * (tacklePower * 0.5f), ForceMode.Impulse);
                        victim.ApplyPush(tackleStunOnHit);

                        // ++ VISUALS — fire impact effect on both attacker and victim
                        Vector3 hitPoint = hit.ClosestPoint(transform.position);
                        _visuals?.OnTackleImpact(hitPoint);
                        hit.GetComponent<PlayerVisuals>()?.OnKnockdown();

                        // Drop ball if victim carries one
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