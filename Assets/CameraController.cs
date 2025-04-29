using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float movementSpeed = 5f;
    public float mouseSensitivity = 2f;
    public float clampAngle = 80f;

    private float rotY = 0f; // Yaw
    private float rotX = 0f; // Pitch

    void Start()
    {
        Vector3 rot = transform.localRotation.eulerAngles;
        rotY = rot.y;
        rotX = rot.x;
        Cursor.lockState = CursorLockMode.Locked; // Hide and lock cursor
        Cursor.visible = false;
    }

    void Update()
    {
        // Mouse rotation
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = -Input.GetAxis("Mouse Y");

        rotY += mouseX * mouseSensitivity;
        rotX += mouseY * mouseSensitivity;
        rotX = Mathf.Clamp(rotX, -clampAngle, clampAngle);

        transform.localRotation = Quaternion.Euler(rotX, rotY, 0f);

        // Movement
        float moveX = Input.GetAxis("Horizontal"); // A/D
        float moveZ = Input.GetAxis("Vertical");   // W/S

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        transform.position += move * movementSpeed * Time.deltaTime;
    }
}
