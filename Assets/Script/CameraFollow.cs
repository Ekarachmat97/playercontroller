using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Camera Settings")]
    public GameObject player;
    public FixedTouchField touchField;
    public float rotationSpeed = 5f; 
    public float maxVerticalAngle = 60f; 
    public float minVerticalAngle = -30f;

    private Vector3 offset;   
    private float verticalRotation = 0f; 

    void Start()
    {
        offset = transform.position - player.transform.position;
    }
    
    void LateUpdate()
    {
        transform.position = player.transform.position + offset;

        // Kontrol rotasi kamera dengan input dari FixedTouchField
        if (touchField.Pressed)
        {
            float rotationX = touchField.TouchDist.x * rotationSpeed * Time.deltaTime; // Rotasi horizontal
            float rotationY = touchField.TouchDist.y * rotationSpeed * Time.deltaTime;

            transform.RotateAround(player.transform.position, Vector3.up, rotationX);
            verticalRotation -= rotationY;
            verticalRotation = Mathf.Clamp(verticalRotation, minVerticalAngle, maxVerticalAngle);

            Quaternion rotation = Quaternion.Euler(verticalRotation, transform.eulerAngles.y, 0);

            offset = rotation * (Vector3.back * offset.magnitude);
        }

        transform.LookAt(player.transform);
    }
}
