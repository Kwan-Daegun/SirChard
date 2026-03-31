using System.Collections;
using UnityEngine;

// ============================================================
//  PlayerTackle — Added Knockdown Check
// ============================================================

[RequireComponent(typeof(AudioSource))]
public class PlayerTackle : MonoBehaviour
{
    public float tacklePower = 20f;
    public float tackleDuration = 0.3f;
    public float tackleCooldown = 1.5f;
    public float tackleStunOnHit = 2f;
    public float tackleRadius = 1.5f;
    public LayerMask playerLayer;

    [Header("Audio")]
    public AudioClip hitSFX;   // Sound when you successfully hit someone
    public AudioClip missSFX;  // Plays immediately on tackle (like a dash/whoosh)

    private Rigidbody _rb;
    private float _lastTackleTime;
    private PlayerVisuals _visuals;
    private AudioSource _audioSource;
    private PlayerMovement _movement; // ++ ADDED: Reference to movement script

    public bool IsTackling { get; private set; }

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _visuals = GetComponent<PlayerVisuals>();
        _audioSource = GetComponent<AudioSource>();
        _movement = GetComponent<PlayerMovement>(); // ++ ADDED
    }

    public void TryTackle()
    {
        if (GameState.IsGameplayLocked) return;

        if (_movement != null && _movement.isKnockedDown) return;

        if (Time.time - _lastTackleTime < tackleCooldown || IsTackling) return;
        StartCoroutine(PerformTackle());
    }

    private IEnumerator PerformTackle()
    {
        IsTackling = true;
        _lastTackleTime = Time.time;

        _visuals?.OnTackleStart();

        // ++ AUDIO: Play the dash/whoosh sound immediately every time you tackle
        if (missSFX != null)
        {
            _audioSource.PlayOneShot(missSFX);
        }

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

                        // ++ AUDIO: Play Hit Sound right on impact
                        if (hitSFX != null)
                        {
                            _audioSource.PlayOneShot(hitSFX);
                        }

                        // ++ VISUALS
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