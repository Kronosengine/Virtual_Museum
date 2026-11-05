using UnityEngine;

// Простой контроллер от первого лица: ходьба WASD, обзор мышью, Shift - бег, Пробел - прыжок.
// Повесить на объект Player (с компонентом Character Controller).
// Камера (Main Camera) должна быть дочерним объектом Player.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Ссылки")]
    public Transform cameraTransform;

    [Header("Настройки")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float mouseSensitivity = 2f;
    public float gravity = -20f;
    public float jumpHeight = 1.2f;      // высота прыжка в метрах

    private CharacterController controller;
    private float verticalVelocity;
    private float cameraPitch;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        LockCursor(true);
    }

    void Update()
    {
        // Esc - освободить курсор, клик - снова захватить
        if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);

        if (Cursor.lockState != CursorLockMode.Locked) return;

        Look();
        Move();
    }

    void Look()
    {
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0f, mx, 0f);

        cameraPitch = Mathf.Clamp(cameraPitch - my, -85f, 85f);
        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    void Move()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

        Vector3 move = (transform.right * x + transform.forward * z) * speed;

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;

        // Прыжок: только когда стоим на земле
        if (controller.isGrounded && Input.GetButtonDown("Jump"))
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}