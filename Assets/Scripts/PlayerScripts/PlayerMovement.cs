using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    private float moveSpeed;
    public float walkSpeed = 5f;
    public float sprintSpeed = 10f;
    public float dodgeSpeed = 15f; // Скорость уклонения
    public float dodgeDistance = 3f; // Расстояние уклонения
    public float dodgeDuration = 0.5f; // Длительность уклонения
    public float dodgeCooldown = 1f; // Кулдаун уклонения
    private bool isDodging = false; // Флаг уклонения
    private float lastDodgeTime; // Время последнего уклонения

    public float groundDrag = 4f;

    [Header("Jumping")]
    public float jumpForce = 10f;
    public float jumpCooldown = 0.25f;
    public float airMultiplier = 0.4f;
    private bool readyToJump;

    [Header("Crouching")]
    public float crouchSpeed = 2f;
    public float crouchYScale = 0.5f;
    private float startYScale;
    public float crouchTransitionSpeed = 2f; // Скорость перехода в crouch

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.LeftControl;
    public KeyCode dodgeKey = KeyCode.V;

    [Header("Ground Check")]
    public float playerHeight = 2f;
    public LayerMask whatIsGround;
    private bool grounded;

    [Header("Slope Handling")]
    public float maxSlopeAngle = 45f;
    private RaycastHit slopeHit;
    private bool exitingSlope;

    public Transform orientation;

    private float horizontalInput;
    private float verticalInput;

    private Vector3 moveDirection;

    private Rigidbody rb;

    public MovementState state;
    public enum MovementState
    {
        walking,
        sprinting,
        crouching,
        air
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // Плавность
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // Предотвращение пролетов
        readyToJump = true;
        startYScale = transform.localScale.y;
        lastDodgeTime = -dodgeCooldown; // Инициализация времени уклонения
    }

    private void Update()
    {
        // Ground check
        grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

        MyInput();
        SpeedControl();
        StateHandler();

        // Handle drag
        rb.drag = grounded ? groundDrag : 0;
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void MyInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        // Jump
        if (Input.GetKey(jumpKey) && readyToJump && grounded)
        {
            readyToJump = false;
            Jump();
            Invoke(nameof(ResetJump), jumpCooldown);
        }

        // Start crouch
        if (Input.GetKeyDown(crouchKey))
        {
            StartCoroutine(Crouch(true));
        }

        // Stop crouch
        if (Input.GetKeyUp(crouchKey))
        {
            StartCoroutine(Crouch(false));
        }

        // Dodge
        if (Input.GetKeyDown(dodgeKey) && grounded && !isDodging && Time.time >= lastDodgeTime + dodgeCooldown)
        {
            StartCoroutine(Dodge());
        }
    }

    private IEnumerator Crouch(bool crouch)
    {
        float targetYScale = crouch ? crouchYScale : startYScale;
        float currentYScale = transform.localScale.y;

        while (Mathf.Abs(currentYScale - targetYScale) > 0.01f)
        {
            currentYScale = Mathf.MoveTowards(currentYScale, targetYScale, crouchTransitionSpeed * Time.deltaTime);
            transform.localScale = new Vector3(transform.localScale.x, currentYScale, transform.localScale.z);
            yield return null;
        }

        transform.localScale = new Vector3(transform.localScale.x, targetYScale, transform.localScale.z);
    }

    private IEnumerator Dodge()
    {
        isDodging = true;
        lastDodgeTime = Time.time; // Сохраняем время уклонения

        // Определяем направление уклонения
        Vector3 dodgeDirection = (orientation.forward * verticalInput + orientation.right * horizontalInput).normalized;

        // Проверка на наличие препятствий
        RaycastHit hit;
        if (Physics.Raycast(transform.position, dodgeDirection, out hit, dodgeDistance, whatIsGround))
        {
            dodgeDistance = hit.distance; // Уменьшаем расстояние уклонения до препятствия
        }

        float elapsed = 0f;

        while (elapsed < dodgeDuration)
        {
            // Плавное движение с использованием силы
            rb.AddForce(dodgeDirection * dodgeSpeed, ForceMode.VelocityChange);

            elapsed += Time.fixedDeltaTime;
            yield return null;
        }

        isDodging = false;
    }

    private void StateHandler()
    {
        // Mode - Crouching
        if (Input.GetKey(crouchKey))
        {
            state = MovementState.crouching;
            moveSpeed = crouchSpeed;
        }
        // Mode - Sprinting
        else if (grounded && Input.GetKey(sprintKey))
        {
            state = MovementState.sprinting;
            moveSpeed = sprintSpeed;
        }
        // Mode - Walking
        else if (grounded)
        {
            state = MovementState.walking;
            moveSpeed = walkSpeed;
        }
        // Mode - Air
        else
        {
            state = MovementState.air;
        }
    }

    private void MovePlayer()
    {
        // Calculate movement direction
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        // On slope
        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection() * moveSpeed * 20f, ForceMode.Force);

            if (rb.velocity.y > 0)
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }
        // On ground
        else if (grounded)
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        }
        // In air
        else if (!grounded)
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);
        }

        // Turn gravity off while on slope
        rb.useGravity = !OnSlope();

        // Плавное изменение скорости
        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        if (flatVel.magnitude > moveSpeed)
        {
            Vector3 limitedVel = flatVel.normalized * moveSpeed;
            rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
        }
    }

    private void SpeedControl()
    {
        // Limiting speed on slope
        if (OnSlope() && !exitingSlope)
        {
            if (rb.velocity.magnitude > moveSpeed)
                rb.velocity = rb.velocity.normalized * moveSpeed;
        }
    }

    private void Jump()
    {
        exitingSlope = true;

        // Reset y velocity
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    private void ResetJump()
    {
        readyToJump = true;
        exitingSlope = false;
    }

    private bool OnSlope()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }

        return false;
    }

    private Vector3 GetSlopeMoveDirection()
    {
        return Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
    }

    private void AdjustPositionOnGround()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, playerHeight * 0.5f + 1f, whatIsGround))
        {
            transform.position = new Vector3(transform.position.x, hit.point.y + (playerHeight * 0.5f), transform.position.z);
        }
    }
}
