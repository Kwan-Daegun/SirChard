using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))] // Ensures we have the shared AudioSource
public class PlayerMovement : MonoBehaviour
{
    #region to be removed when input system is fully implemented
    float stunTimer;
    bool isStunned = false;
    #endregion

    public bool isKnockedDown = false; // ++ CHANGED: Made public so PlayerTackle can read it
    float knockdownTimer;
    public float knockdownDuration = 2f;

    [Header("Animation Settings")]
    public Animator animator;

    [Header("Movement Settings")]
    public float moveSpeed;
    public float rotationSpeed = 5000f;
    public float groundDrag;

    [Header("Jumping Settings")]
    public float jumpForce;
    public float jumpCooldown;
    public float airMultiplier;
    bool readyToJump;

    [Header("Natural Jump Polish")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    [Header("Audio Settings")]
    public AudioClip runningSFX;
    public float footstepSpeed = 0.3f;
    private float footstepTimer;

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

    private PlayerVisuals _visuals;
    private AudioSource _audioSource;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        readyToJump = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        _visuals = GetComponent<PlayerVisuals>();
        _audioSource = GetComponent<AudioSource>();

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
            if (stunTimer <= 0f) isStunned = false;
        }
        #endregion

        if (isKnockedDown)
        {
            knockdownTimer -= Time.deltaTime;
            if (knockdownTimer <= 0f)
            {
                isKnockedDown = false;
                rb.angularVelocity = Vector3.zero;
                rb.freezeRotation = true;
                transform.rotation = Quaternion.identity;
                _visuals?.OnGetUp();
            }
        }

        grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.3f, whatIsGround);

        MyInput();
        SpeedControl();
        UpdateAnimations();
        HandleRunningSFX(); // Handles our footstep timer

        if (grounded)
            rb.linearDamping = groundDrag;
        else
            rb.linearDamping = 0;
    }

    private void FixedUpdate()
    {
        MovePlayer();
        BetterJumpPhysics();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 inputVec = context.ReadValue<Vector2>();
        horizontalInput = inputVec.x;
        verticalInput = inputVec.y;
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
        if (GetComponent<PlayerTackle>() != null && GetComponent<PlayerTackle>().IsTackling)
            return;

        #region to be removed when input system is fully implemented
        if (isStunned) return;
        #endregion

        if (isKnockedDown) return;

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

    private void BetterJumpPhysics()
    {
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !jumpInput)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    private void SpeedControl()
    {
        if (GetComponent<PlayerTackle>() != null && GetComponent<PlayerTackle>().IsTackling)
            return;

        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        if (flatVel.magnitude > moveSpeed)
        {
            Vector3 limitedVel = flatVel.normalized * moveSpeed;
            rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
        }
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        if (isKnockedDown || (GetComponent<PlayerTackle>() != null && GetComponent<PlayerTackle>().IsTackling))
        {
            animator.SetFloat("Speed", 0f);
            return;
        }

        float inputMagnitude = new Vector2(horizontalInput, verticalInput).magnitude;
        float clampedInput = Mathf.Clamp01(inputMagnitude);

        animator.SetFloat("Speed", clampedInput, 0.1f, Time.deltaTime);
        animator.SetBool("IsGrounded", grounded);
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        if (animator != null) animator.SetTrigger("Jump");
    }

    private void ResetJump()
    {
        readyToJump = true;
    }

    private void HandleRunningSFX()
    {
        if (runningSFX == null || _audioSource == null) return;

        bool isTackling = GetComponent<PlayerTackle>() != null && GetComponent<PlayerTackle>().IsTackling;
        float inputMagnitude = new Vector2(horizontalInput, verticalInput).magnitude;

        bool isMovingAndGrounded = (inputMagnitude > 0.1f) && grounded && !isKnockedDown && !isStunned && !isTackling;

        if (isMovingAndGrounded)
        {
            footstepTimer -= Time.deltaTime;

            if (footstepTimer <= 0f)
            {
                _audioSource.PlayOneShot(runningSFX, 0.6f);
                footstepTimer = footstepSpeed;
            }
        }
        else
        {
            footstepTimer = 0f;
        }
    }

    public void ResetPlayerStates()
    {
        isKnockedDown = false;
        isStunned = false;
        stunTimer = 0f;
        knockdownTimer = 0f;
        rb.freezeRotation = true;
        transform.rotation = Quaternion.identity;
        _visuals?.OnGetUp();
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

        rb.AddTorque(transform.right * 15f, ForceMode.Impulse);
        rb.AddForce(Vector3.up * 3f, ForceMode.Impulse);
    }
    #endregion
}