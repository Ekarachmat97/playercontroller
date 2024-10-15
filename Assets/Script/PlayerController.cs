using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    [Header("Pengaturan Pemain")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;
    public float fallThreshold = -10f;
    public float fallMultiplier = 2.5f;

    [Header("Ledge Grab Settings")]
    [SerializeField] CapsuleCollider capsuleCollider;
    [SerializeField] LayerMask geometryMask = ~0;
    [SerializeField] float grabHeight = 1.75f;
    [SerializeField] float horizontalDistanceCheck = 0.75f;
    [SerializeField] float verticalDistanceCheck = 0.75f;
    [SerializeField] float maxSlopeAngle = 20;
    [SerializeField] float capsuleCastCheckDistance = 0.75f;
    Collider[] collisionResults = new Collider[20];
    private bool isLedgeGrabbing = false;

    [Header("Joystick dan Animator")]
    public FixedJoystick joystick;
    public Animator animator;
    public Transform playerModel;
    public Camera mainCamera;

    private CharacterController characterController;
    private Vector3 velocity;
    private bool isGrounded;
    private int jumpCount = 0;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (!isLedgeGrabbing) // Hanya jika tidak sedang grab ledge
        {
            UpdateGroundedState();
            HandleMovement();
            HandleJump();
            ApplyGravity();
            MoveCharacter();
        }
        else
        {
            // Hentikan pergerakan saat ledge grab aktif
            animator.SetBool("isLedgeGrabbing", true);
        }

        animator.SetBool("isJumping", !isGrounded);
        if (transform.position.y <= fallThreshold) Restart();

        // Cek ledge grab jika player jatuh atau mendekati tepi
        if (!isGrounded && velocity.y < 0) TryLedgeGrab();
    }

    private void UpdateGroundedState()
    {
        isGrounded = characterController.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            jumpCount = 0;
        }
    }

    private void HandleMovement()
    {
        float horizontal = joystick.Horizontal + Input.GetAxis("Horizontal");
        float vertical = joystick.Vertical + Input.GetAxis("Vertical");
        Vector3 moveDirection = CalculateMoveDirection(horizontal, vertical);

        if (moveDirection.magnitude >= 0.1f)
        {
            float speed = (Mathf.Abs(horizontal) > 0.3f || Mathf.Abs(vertical) > 0.3f) ? runSpeed : walkSpeed;
            animator.SetBool("isRunning", speed == runSpeed);
            animator.SetBool("isWalking", speed == walkSpeed);
            characterController.Move(moveDirection * speed * Time.deltaTime);
            playerModel.forward = moveDirection;
        }
        else
        {
            animator.SetBool("isWalking", false);
            animator.SetBool("isRunning", false);
        }
    }

    private Vector3 CalculateMoveDirection(float horizontal, float vertical)
    {
        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 cameraRight = mainCamera.transform.right;
        cameraForward.y = 0;
        cameraRight.y = 0;
        return (cameraForward.normalized * vertical + cameraRight.normalized * horizontal).normalized;
    }

    private void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && (isGrounded || jumpCount < 2))
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpCount++;

            if (jumpCount == 2 && !isGrounded) 
            {
                animator.SetTrigger("isRolling");
            }
        }
    }

    private void ApplyGravity()
    {
        if (velocity.y < 0)
            velocity.y += gravity * fallMultiplier * Time.deltaTime;
        else
            velocity.y += gravity * Time.deltaTime;
    }

    private void MoveCharacter()
    {
        characterController.Move(velocity * Time.deltaTime);
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void TryLedgeGrab()
    {
        Vector3 ledgePoint, ledgeNormal;

        if (TryGetLedgeGrabPoint(out ledgePoint, out ledgeNormal))
        {
            isLedgeGrabbing = true;
            StartCoroutine(PerformLedgeGrab(ledgePoint, ledgeNormal));
        }
    }

    private bool TryGetLedgeGrabPoint(out Vector3 ledgePoint, out Vector3 ledgeNormal)
    {
        ledgePoint = Vector3.zero;
        ledgeNormal = Vector3.zero;

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position + new Vector3(0, grabHeight, 0),
            capsuleCollider.radius,
            collisionResults,
            geometryMask,
            QueryTriggerInteraction.Ignore
        );

        if (hitCount != 0) return false;

        int verticalHits = 0;

        for (int i = -1; i <= 1; i++)
        {
            Vector3 origin = transform.position + new Vector3(0, grabHeight, 0) + transform.right * capsuleCollider.radius * i;

            if (Physics.Raycast(origin, transform.forward, horizontalDistanceCheck, geometryMask))
            {
                return false;
            }
            else
            {
                origin += transform.forward * horizontalDistanceCheck;
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, verticalDistanceCheck, geometryMask) &&
                    Vector3.Angle(hit.normal, Vector3.up) < maxSlopeAngle)
                {
                    verticalHits++;
                }
            }
        }

        if (verticalHits < 2) return false;

        Vector3 capsuleTop = transform.position + (capsuleCollider.height + capsuleCollider.radius * 2) * Vector3.up;
        Vector3 capsuleBottom = capsuleTop - Vector3.up * capsuleCollider.radius * 2 - transform.forward * capsuleCollider.radius * 2;
        Vector3 dir = (Vector3.down + transform.forward) / 2;

        if (Physics.CapsuleCast(capsuleTop, capsuleBottom, capsuleCollider.radius, dir, out RaycastHit capsuleHit, capsuleCastCheckDistance, geometryMask))
        {
            ledgePoint = capsuleHit.point;
            ledgeNormal = capsuleHit.normal;
            return true;
        }

        return false;
    }

    private IEnumerator PerformLedgeGrab(Vector3 ledgePoint, Vector3 ledgeNormal)
{
    Vector3 targetPosition = ledgePoint - new Vector3(0, grabHeight, 0) - transform.forward * capsuleCollider.radius;
    transform.position = targetPosition; // Pindah ke posisi awal ledge
    transform.rotation = Quaternion.LookRotation(new Vector3(-ledgeNormal.x, 0, -ledgeNormal.z));

    yield return new WaitForSeconds(3.5f); // Waktu tunggu sebelum pull up

    // Gerak maju 1f ke arah depan
    Vector3 moveForward = transform.forward * 1f; // 1f ke depan
    transform.position += moveForward; // Pindahkan posisi ke depan

    // Sekarang pindah ke posisi di atas ledge
    transform.position = new Vector3(transform.position.x, ledgePoint.y + capsuleCollider.height, transform.position.z);

    isLedgeGrabbing = false;
    animator.SetBool("isLedgeGrabbing", false);
}


    private void OnDrawGizmos()
{
    if (capsuleCollider == null) return;

    // Warna untuk menunjukkan area deteksi
    Gizmos.color = Color.yellow;

    // Posisi atas untuk OverlapSphere
    Vector3 sphereCenter = transform.position + Vector3.up * grabHeight;
    Gizmos.DrawWireSphere(sphereCenter, capsuleCollider.radius);

    // Garis horizontal ke depan (horizontal ray checks)
    for (int i = -1; i <= 1; i++)
    {
        Vector3 origin = sphereCenter + transform.right * capsuleCollider.radius * i;
        Vector3 direction = transform.forward * horizontalDistanceCheck;
        Gizmos.DrawLine(origin, origin + direction);
    }

    // Posisi dan arah untuk CapsuleCast
    Vector3 capsuleTop = transform.position + (capsuleCollider.height + capsuleCollider.radius * 2) * Vector3.up;
    Vector3 capsuleBottom = capsuleTop - Vector3.up * capsuleCollider.radius * 2 - transform.forward * capsuleCollider.radius * 2;
    Vector3 capsuleDir = (Vector3.down + transform.forward).normalized;

    // Gambar kapsul dan arah cast
    Gizmos.color = Color.blue;
    Gizmos.DrawLine(capsuleTop, capsuleBottom); // Kapsul garis tengah
    Gizmos.DrawWireSphere(capsuleTop, capsuleCollider.radius);
    Gizmos.DrawWireSphere(capsuleBottom, capsuleCollider.radius);

    Gizmos.color = Color.green;
    Gizmos.DrawRay(capsuleBottom, capsuleDir * capsuleCastCheckDistance); // Arah kapsul
}

}
