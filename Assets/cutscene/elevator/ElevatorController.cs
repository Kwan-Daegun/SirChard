using UnityEngine;

public class ElevatorController : MonoBehaviour
{
    [Header("Movement")]
    public float height = 10f;
    public float duration = 3f;

    [Header("Arena Collider (Disable during lift)")]
    public Collider arenaCollider;

    private Vector3 startPos;
    private Vector3 endPos;
    private float timer;
    private bool isMoving = false;
    private bool finished = false;

    void Start()
    {
        startPos = transform.position;
        endPos = startPos + Vector3.up * height;
    }

    public void StartElevator()
    {
        // Disable arena collider only while the lift is actually moving.
        if (arenaCollider != null)
            arenaCollider.enabled = false;

        timer = 0f;
        isMoving = true;
        finished = false;
    }

    void Update()
    {
        if (!isMoving) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);

        // 🎬 Ease-in-out motion
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        transform.position = Vector3.Lerp(startPos, endPos, smoothT);

        if (t >= 1f)
        {
            isMoving = false;
            finished = true;

            // Re-enable arena collider AFTER lift
            if (arenaCollider != null)
                arenaCollider.enabled = true;
        }
    }

    public bool IsFinished()
    {
        return finished;
    }
}
