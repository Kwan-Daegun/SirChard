using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    #region  to be removed when input system is fully implemented
    float stunTimer;
    bool isStunned = false;
    #endregion

    // NEW: knockdown variables
    bool isKnockedDown = false;
    float knockdownTimer;
    public float knockdownDuration = 2f;

    public float moveSpeed;
    public float rotationSpeed = 5000f;
    public float groundDrag;
    public float jumpForce;
    public float jumpCooldown;
    public float airMultiplier;
    bool readyToJump;

    [HideInInspector] public float walkSpeed;
    [HideInInspector] public float sprintSpeed;

    public float playerHeight;
    public LayerMask whatIsGround;
    bool grounded;

    float horizontalInput;
    float verticalInput;
    bool jumpInput;

    Vector3 moveDirection;

    Rigidbody rb;
    public Transform cameraTransform;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        readyToJump = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        #region to be removed when input system is fully implemented
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;

            if (stunTimer <= 0f)
                isStunned = false;
        }
        #endregion

        // handle knockdown timer
        if (isKnockedDown)
        {
            knockdownTimer -= Time.deltaTime;

            if (knockdownTimer <= 0f)
            {
                isKnockedDown = false;

                rb.angularVelocity = Vector3.zero;
                rb.freezeRotation = true;

                transform.rotation = Quaternion.identity;
            }
        }

        grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.3f, whatIsGround);

        MyInput();
        SpeedControl();

        if (grounded)
            rb.linearDamping = groundDrag;
        else
            rb.linearDamping = 0;
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 inputVec = context.ReadValue<Vector2>();
        horizontalInput = inputVec.x;
        verticalInput = inputVec.y;
        Debug.Log($"Moving: {inputVec}");
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        jumpInput = context.action.IsPressed();
    }

    private void MyInput()
    {
        if (jumpInput && readyToJump && grounded)
        {
            readyToJump = false;
            Jump();
            Invoke(nameof(ResetJump), jumpCooldown);
        }
    }

    private void MovePlayer()
    {
        #region to be removed when input system is fully implemented
        if (isStunned)
            return;
        #endregion

        if (isKnockedDown)
            return;

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;

        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        moveDirection = (camForward * verticalInput + camRight * horizontalInput).normalized;

        if (moveDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }

        float currentMultiplier = grounded ? 10f : 10f * airMultiplier;
        rb.AddForce(moveDirection * moveSpeed * currentMultiplier, ForceMode.Force);
    }

    private void SpeedControl()
    {
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        if (flatVel.magnitude > moveSpeed)
        {
            Vector3 limitedVel = flatVel.normalized * moveSpeed;
            rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
        }
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private void ResetJump()
    {
        readyToJump = true;
    }

    #region to be removed when input system is fully implemented
    public void SetInput(Vector2 moveInput, bool jumpPressed)
    {
        horizontalInput = moveInput.x;
        verticalInput = moveInput.y;
        jumpInput = jumpPressed;
    }

    public void ApplyPush(float duration)
    {
        isStunned = true;
        stunTimer = duration;

        isKnockedDown = true;
        knockdownTimer = knockdownDuration;

        rb.freezeRotation = false;

        // physics tumble
        rb.AddTorque(transform.right * 15f, ForceMode.Impulse);
        rb.AddForce(Vector3.up * 3f, ForceMode.Impulse);
    }
    #endregion
}