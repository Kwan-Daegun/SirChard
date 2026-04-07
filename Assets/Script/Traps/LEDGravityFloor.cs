using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LEDGravityFloor : MonoBehaviour
{
    [Header("Wind Settings")]
    public float launchForce = 45f;
    [Tooltip("How high the wind reaches above the floor")]
    public float windHeight = 12f;

    [Header("Wind Timing")]
    public bool useRandomInterval = false;
    [Tooltip("How long the wind stays active each cycle")]
    public float activeDuration = 1.5f;
    [Tooltip("Used when random interval is off")]
    public float intervalSeconds = 5f;
    [Tooltip("Used when random interval is on")]
    public float minIntervalSeconds = 3f;
    [Tooltip("Used when random interval is on")]
    public float maxIntervalSeconds = 7f;

    [Header("Wind VFX")]
    [Tooltip("Optional particle systems to play only while wind is active")]
    public ParticleSystem[] windStreakParticles;

    bool windActive = true;
    float activeTimer;
    float idleTimer;

    void Start()
    {
        // Setup Trigger Zone to cover the floor
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        
        col.isTrigger = true;

        // FIX: We set the center and size relative to the local transform
        // To prevent "extension," we ensure the Y size is calculated correctly relative to parent scale
        float lossyY = transform.lossyScale.y;
        float adjustedHeight = windHeight / (lossyY != 0 ? lossyY : 1f);

        col.center = new Vector3(0, adjustedHeight / 60f, 0);
        col.size = new Vector3(col.size.x, adjustedHeight, col.size.z);

        activeTimer = Mathf.Max(0f, activeDuration);
        idleTimer = GetNextInterval();
        ApplyWindVfxState();
    }

    void Update()
    {
        if (windActive)
        {
            activeTimer -= Time.deltaTime;
            if (activeTimer <= 0f)
            {
                windActive = false;
                idleTimer = GetNextInterval();
                ApplyWindVfxState();
            }
        }
        else
        {
            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0f)
            {
                windActive = true;
                activeTimer = Mathf.Max(0f, activeDuration);
                ApplyWindVfxState();
            }
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (!windActive) return;

        Rigidbody rb = other.GetComponent<Rigidbody>();

        if (rb != null)
        {
            // Override velocity to force lift-off
            Vector3 vel = rb.linearVelocity;
            if (vel.y < launchForce * 0.7f)
            {
                vel.y = launchForce;
                rb.linearVelocity = vel;
            }

            rb.AddForce(Vector3.up * launchForce, ForceMode.Acceleration);

            Animator anim = other.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                // Checks to prevent the "Parameter does not exist" error
                foreach (AnimatorControllerParameter param in anim.parameters)
                {
                    if (param.name == "isRunning")
                    {
                        anim.SetBool("isRunning", false);
                    }
                    if (param.name == "Jump")
                    {
                        anim.SetTrigger("Jump");
                    }
                }
            }
        }
    }

    float GetNextInterval()
    {
        if (useRandomInterval)
        {
            float min = Mathf.Max(0f, minIntervalSeconds);
            float max = Mathf.Max(min, maxIntervalSeconds);
            return Random.Range(min, max);
        }

        return Mathf.Max(0f, intervalSeconds);
    }

    void ApplyWindVfxState()
    {
        if (windStreakParticles == null) return;

        foreach (ParticleSystem ps in windStreakParticles)
        {
            if (ps == null) continue;

            if (windActive)
            {
                if (!ps.isPlaying)
                    ps.Play(true);
            }
            else
            {
                if (ps.isPlaying)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = windActive ? new Color(0.3f, 1f, 0.3f, 0.12f) : new Color(1f, 0.3f, 0.3f, 0.08f);
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            // FIX: Use lossyScale to draw the gizmo exactly where the physics trigger is
            Vector3 worldScale = transform.lossyScale;
            Vector3 size = new Vector3(col.size.x * worldScale.x, col.size.y * worldScale.y, col.size.z * worldScale.z);
            Vector3 pos = transform.TransformPoint(col.center);

            Gizmos.DrawCube(pos, size);
            Gizmos.DrawWireCube(pos, size);
        }
    }
}
