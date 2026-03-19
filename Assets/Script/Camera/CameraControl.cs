using UnityEngine;

public class CameraControl : MonoBehaviour
{
    public float m_DampTime = 0.15f;          // movement smoothness
    public float m_ZoomDampTime = 0.08f;      // faster zoom response
    public float m_ScreenEdgeBuffer = 5f;

    public float m_MinDistance = 15f;         // closest zoom
    public float m_MaxDistance = 50f;         // farthest zoom

    public Transform[] m_Targets;

    private Camera m_Camera;
    private Vector3 m_MoveVelocity;
    private Vector3 m_DesiredPosition;
    private Vector3 m_AimToRig;

    private void Awake()
    {
        m_Camera = GetComponentInChildren<Camera>();

        // Calculate offset from camera to rig
        Plane plane = new Plane(Vector3.up, transform.position);
        Ray ray = new Ray(m_Camera.transform.position, m_Camera.transform.forward);

        if (plane.Raycast(ray, out float distance))
        {
            Vector3 aimPoint = ray.GetPoint(distance);
            m_AimToRig = transform.position - aimPoint;
        }
    }

    private void LateUpdate()
    {
        if (m_Targets == null || m_Targets.Length == 0)
            return;

        FindCenterPosition();
        MoveAndZoom();
    }

    private void FindCenterPosition()
    {
        Bounds bounds = new Bounds(m_Targets[0].position, Vector3.zero);

        for (int i = 0; i < m_Targets.Length; i++)
        {
            if (m_Targets[i] == null || !m_Targets[i].gameObject.activeSelf)
                continue;

            bounds.Encapsulate(m_Targets[i].position);
        }

        m_DesiredPosition = bounds.center;
    }

    private void MoveAndZoom()
    {
        float requiredSize = FindRequiredSize();

        // 🔥 Faster, more responsive zoom
        float zoomFactor = requiredSize * 1.2f;

        float targetDistance = Mathf.Clamp(
            zoomFactor,
            m_MinDistance,
            m_MaxDistance
        );

        Vector3 dir = m_Camera.transform.forward;

        Vector3 desiredPosition = m_DesiredPosition + m_AimToRig - dir * targetDistance;

        // 🔥 Faster smoothing (less delay feeling)
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref m_MoveVelocity,
            m_ZoomDampTime
        );
    }

    private float FindRequiredSize()
    {
        Bounds bounds = new Bounds(m_Targets[0].position, Vector3.zero);

        for (int i = 0; i < m_Targets.Length; i++)
        {
            if (m_Targets[i] == null || !m_Targets[i].gameObject.activeSelf)
                continue;

            bounds.Encapsulate(m_Targets[i].position);
        }

        float size = Mathf.Max(bounds.size.x, bounds.size.z);
        size += m_ScreenEdgeBuffer;

        return size;
    }

    public void SetStartPositionAndSize()
    {
        FindCenterPosition();
        MoveAndZoom();
    }

    public void SetTargets(Transform p1, Transform p2, Transform p3, Transform p4, int playerCount)
    {
        switch (playerCount)
        {
            case 1:
                m_Targets = new Transform[] { p1 };
                break;
            case 2:
                m_Targets = new Transform[] { p1, p2 };
                break;
            case 3:
                m_Targets = new Transform[] { p1, p2, p3 };
                break;
            case 4:
                m_Targets = new Transform[] { p1, p2, p3, p4 };
                break;
        }
    }
}