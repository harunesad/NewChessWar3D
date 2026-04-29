using UnityEngine;

public class TPSCamera : MonoBehaviour
{
    [Header("Settings")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 5, -7);
    public float smoothSpeed = 0.125f;
    public float rotationSpeed = 5f;

    [Header("Orbit (Optional)")]
    public bool allowOrbit = true;
    public float orbitSpeed = 2f;
    
    private float currentX = 0f;
    private float currentY = 0f;

    void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Follow logic
        Vector3 desiredPosition = target.position + target.TransformDirection(offset);
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        // Always look at target (or slightly above)
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }
}
