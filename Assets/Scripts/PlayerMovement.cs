using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float gravity = -9.81f;

    [Header("References")]
    public Joystick joystick;
    public Animator animator;

    [Tooltip("Kamerayı buraya bağla. Boş bırakılırsa Camera.main kullanılır.")]
    public Transform cameraTransform;

    [Header("Board Boundaries")]
    public bool useBoundaries = true;
    public float minX = -4.5f;
    public float maxX = 4.5f;
    public float minZ = -4.5f;
    public float maxZ = 4.5f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Kamera referansı boşsa otomatik bul
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        // Zemin kontrolü
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        // Joystick girdisi
        float horizontal = joystick.Horizontal;
        float vertical = joystick.Vertical;

        // Kamera yönüne göre hareket vektörü hesapla
        Vector3 move = Vector3.zero;

        if (cameraTransform != null)
        {
            // Kameranın ileri ve sağ vektörlerini al, Y eksenini sıfırla
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            // Joystick girdisini kamera yönüne göre birleştir
            move = (camForward * vertical + camRight * horizontal).normalized;
        }
        else
        {
            // Kamera yoksa düz dünya koordinatlarını kullan
            move = new Vector3(horizontal, 0, vertical).normalized;
        }

        if (move.magnitude >= 0.1f)
        {
            // Karakteri hareket yönüne döndür
            float targetAngle = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0, targetAngle, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            // Karakteri hareket ettir
            controller.Move(move * moveSpeed * Time.deltaTime);

            // Walk animasyonu
            if (animator != null) animator.SetBool("Walk", true);
        }
        else
        {
            // Idle animasyonu
            if (animator != null) animator.SetBool("Walk", false);
        }

        // Yerçekimi uygula
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // --- Sınır Kontrolü (Her karede en son yapılması lazım) ---
        if (useBoundaries)
        {
            ApplyBoundaries();
        }
    }

    private void ApplyBoundaries()
    {
        // Pozisyonu belirlediğin değerlere göre kısıtla (Clamp)
        Vector3 clampedPosition = transform.position;
        clampedPosition.x = Mathf.Clamp(clampedPosition.x, minX, maxX);
        clampedPosition.z = Mathf.Clamp(clampedPosition.z, minZ, maxZ);

        if (transform.position != clampedPosition)
        {
            transform.position = clampedPosition;
        }
    }
}
