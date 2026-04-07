using System.Collections;
using UnityEngine;

public class GameStartCutscene : MonoBehaviour
{
    [Header("References")]
    public ElevatorController elevator;

    [Header("Camera")]
    public CameraControl cameraControl;
    public Transform orbitCenter;

    [Header("Orbit Settings")]
    public float orbitSpeed = 25f;
    public float orbitDuration = 4f;
    public int orbitPreviewSegments = 40;

    [Header("Return Settings")]
    public float returnDuration = 1.5f;

    [Header("Camera Distance Control")]
    public float introMinDistance = 30f;
    public float introMaxDistance = 40f;

    [Header("Timing")]
    public float countdownDelay = 1f;
    public float postElevatorDelay = 0.5f;

    [Header("Ball")]
    public GameObject ball;

    [Header("Audio (Cutscene Only)")]
    public string introBGMName;
    public string countdownBeepName;
    public string goSFXName;
    public string gameplayLayerName;

    private bool orbiting = false;

    private Vector3 originalPos;
    private Quaternion originalRot;

    private float originalMinDistance;
    private float originalMaxDistance;

    void Start()
    {
        StartCoroutine(WaitForReadyAndRunCutscene());
    }

    IEnumerator WaitForReadyAndRunCutscene()
    {
        while (!GameState.ArePlayersReady)
            yield return null;

        yield return StartCoroutine(RunCutscene());
    }

    IEnumerator RunCutscene()
    {
        GameState.IsGameplayLocked = true;

        if (elevator != null)
            elevator.StartElevator();

        Camera cam = Camera.main;

        // Save camera state
        originalPos = cam.transform.position;
        originalRot = cam.transform.rotation;

        originalMinDistance = cameraControl.m_MinDistance;
        originalMaxDistance = cameraControl.m_MaxDistance;

        // Force zoom out during intro
        cameraControl.m_MinDistance = introMinDistance;
        cameraControl.m_MaxDistance = introMaxDistance;

        // 🎵 Play intro music
        if (CutsceneAudioManager.Instance != null)
        {
            CutsceneAudioManager.Instance.PlayMusic(introBGMName);
        }

        // Disable ball
        if (ball != null)
            ball.SetActive(false);

        // 🛗 Wait elevator
        if (elevator != null)
        {
            while (!elevator.IsFinished())
                yield return null;
        }

        yield return new WaitForSeconds(postElevatorDelay);

        // Take camera control
        cameraControl.enabled = false;

        // Orbit
        orbiting = true;
        yield return new WaitForSeconds(orbitDuration);
        orbiting = false;

        // Return camera
        yield return StartCoroutine(ReturnCamera(cam));

        // Countdown
        yield return StartCoroutine(Countdown());

        // Enable ball
        if (ball != null)
            ball.SetActive(true);

        // 🎵 Start gameplay layer AFTER intro finishes
        if (CutsceneAudioManager.Instance != null)
        {
            CutsceneAudioManager.Instance.PlayLayerAfterIntro(gameplayLayerName);
        }

        // Restore camera
        cameraControl.m_MinDistance = originalMinDistance;
        cameraControl.m_MaxDistance = originalMaxDistance;

        cameraControl.enabled = true;
        cameraControl.SetStartPositionAndSize();

        GameState.IsGameplayLocked = false;
    }

    void Update()
    {
        if (orbiting && orbitCenter != null)
        {
            Camera cam = Camera.main;

            cam.transform.RotateAround(
                orbitCenter.position,
                Vector3.up,
                orbitSpeed * Time.deltaTime
            );

            cam.transform.LookAt(orbitCenter);
        }
    }

    IEnumerator ReturnCamera(Camera cam)
    {
        float t = 0f;

        Vector3 startPos = cam.transform.position;
        Quaternion startRot = cam.transform.rotation;

        while (t < returnDuration)
        {
            t += Time.deltaTime;
            float p = t / returnDuration;
            float smoothP = Mathf.SmoothStep(0f, 1f, p);

            cam.transform.position = Vector3.Lerp(startPos, originalPos, smoothP);
            cam.transform.rotation = Quaternion.Slerp(startRot, originalRot, smoothP);

            yield return null;
        }

        cam.transform.position = originalPos;
        cam.transform.rotation = originalRot;
    }

    IEnumerator Countdown()
    {
        for (int i = 3; i > 0; i--)
        {
            if (CutsceneAudioManager.Instance != null)
            {
                CutsceneAudioManager.Instance.PlaySFX(countdownBeepName);
            }

            yield return new WaitForSeconds(countdownDelay);
        }

        if (CutsceneAudioManager.Instance != null)
        {
            CutsceneAudioManager.Instance.PlaySFX(goSFXName);
        }

        yield return new WaitForSeconds(0.3f);
    }

    void OnDrawGizmos()
    {
        if (orbitCenter == null || Camera.main == null) return;

        Camera cam = Camera.main;

        Vector3 center = orbitCenter.position;
        Vector3 camPos = cam.transform.position;

        float radius = Vector3.Distance(camPos, center);

        Gizmos.color = Color.green;

        Vector3 prevPoint = center + (Vector3.forward * radius);

        for (int i = 1; i <= orbitPreviewSegments; i++)
        {
            float angle = (i / (float)orbitPreviewSegments) * Mathf.PI * 2f;

            Vector3 nextPoint = center + new Vector3(
                Mathf.Sin(angle) * radius,
                0,
                Mathf.Cos(angle) * radius
            );

            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(center, 0.3f);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(originalPos == Vector3.zero ? camPos : originalPos, 0.3f);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(camPos, originalPos);
    }
}
