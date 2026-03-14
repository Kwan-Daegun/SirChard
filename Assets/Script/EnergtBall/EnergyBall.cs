using UnityEngine;

public class EnergyBall : MonoBehaviour
{
    public float flySpeed = 10f;
    public float dropPopForce = 5f;
    public float pickupCooldown = 1f;

    public GameObject currentOwner;

    private Transform targetPlaceholder;
    private bool isFlying = false;
    private Rigidbody rb;
    private bool canBePickedUp = true;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (isFlying && targetPlaceholder != null)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPlaceholder.position, flySpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPlaceholder.position) < 0.1f)
            {
                transform.SetParent(targetPlaceholder);
                transform.localPosition = Vector3.zero;
                isFlying = false;
            }
        }
    }

    private void OnTriggerEnter(Collider col)
    {
        ProcessTouch(col.gameObject);
    }

    private void OnCollisionEnter(Collision col)
    {
        ProcessTouch(col.gameObject);
    }

    private void ProcessTouch(GameObject hitObj)
    {
        if (!canBePickedUp) return;

        if (hitObj.CompareTag("Player1") || hitObj.CompareTag("Player2"))
        {
            if (currentOwner != null) return;

            Transform newPlaceholder = FindDeepChild(hitObj.transform, "EBPH");

            if (newPlaceholder != null)
            {
                currentOwner = hitObj;
                targetPlaceholder = newPlaceholder;
                isFlying = true;
                transform.SetParent(null);
                
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                }
                
                Collider myCollider = GetComponent<Collider>();
                if (myCollider != null)
                {
                    myCollider.isTrigger = true;
                }
            }
        }
    }

    public void DropBall()
    {
        currentOwner = null;
        targetPlaceholder = null;
        isFlying = false;
        transform.SetParent(null);
        canBePickedUp = false;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(Vector3.up * dropPopForce, ForceMode.Impulse);
        }

        Collider myCollider = GetComponent<Collider>();
        if (myCollider != null)
        {
            myCollider.isTrigger = false;
        }

        Invoke(nameof(EnablePickup), pickupCooldown);
    }

    private void EnablePickup()
    {
        canBePickedUp = true;
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        Transform[] allChildren = parent.GetComponentsInChildren<Transform>(true);
        
        foreach (Transform child in allChildren)
        {
            if (child.name == childName)
            {
                return child;
            }
        }
        
        return null;
    }
}