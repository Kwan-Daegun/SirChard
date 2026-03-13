using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAi : MonoBehaviour
{
   public Transform player;
    public float speed = 5f;
    public float jumpForce = 6f;
    public LayerMask groundLayer;

    private Rigidbody rb;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        
        Vector3 direction = (player.position - transform.position);
        direction.y = 0;
        direction.Normalize();

        rb.linearVelocity = new Vector3(direction.x * speed, rb.linearVelocity.y, direction.z * speed);

        
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f, groundLayer);

        
        if (isGrounded && ShouldJump())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    bool ShouldJump()
    {
        return false; 
    }
}
