using UnityEngine;
using System.Collections;

public class PopupTrap : MonoBehaviour
{
    [Header("Timing Settings")]
    public float minIdleTime = 2f;
    public float maxIdleTime = 5f;
    public float activeDuration = 2f;

    [Header("Movement Settings")]
    public Vector3 hiddenPositionOffset = new Vector3(0, -1.5f, 0);
    public float moveSpeed = 10f;

    private Vector3 upPosition;
    private Vector3 downPosition;
    private bool isUp = false;

    void Start()
    {
        // Store the starting (Up) position and the hidden (Down) position
        upPosition = transform.position;
        downPosition = transform.position + hiddenPositionOffset;

        // Start the trap cycle
        transform.position = downPosition;
        StartCoroutine(TrapCycle());
    }

    IEnumerator TrapCycle()
    {
        while (true)
        {
            // 1. Wait underground for a random amount of time
            float randomWait = Random.Range(minIdleTime, maxIdleTime);
            yield return new WaitForSeconds(randomWait);

            // 2. Move Up
            yield return MoveTrap(upPosition);
            isUp = true;

            // 3. Stay up for a bit
            yield return new WaitForSeconds(activeDuration);

            // 4. Move Down
            yield return MoveTrap(downPosition);
            isUp = false;
        }
    }

    IEnumerator MoveTrap(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            transform.position = Vector3.Lerp(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
    }
}