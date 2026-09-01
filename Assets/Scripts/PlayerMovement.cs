using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    public float speed = 5f;
    public float sprintSpeed = 7f;

    [Header("Configuración de Rotación")]
    public float turnSpeed = 15f;

    [Header("Stamina")]
    public float staminaMaxima    = 100f;
    public float staminaActual    = 100f;
    public float consumoPorSegundo = 10f;

    [HideInInspector] public bool movimientoBloqueado = false;

    // ── Variables internas ────────────────────────────────────────────────────
    private Rigidbody   rb;
    private Vector3     movement;
    private bool        _sprintando    = false;
    private bool        _usingPlayerInput = false;

    private InputAction moveAction;
    private InputAction sprintAction;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Intentar usar las acciones del PlayerInput del mismo objeto
        PlayerInput pi = GetComponent<PlayerInput>();
        if (pi != null)
        {
            _usingPlayerInput = true;
            var map   = pi.actions.FindActionMap("Player");
            moveAction   = map.FindAction("Move");
            sprintAction = map.FindAction("Sprint");
        }
        else
        {
            // Fallback: crear acciones manualmente (para pruebas sin PlayerInput)
            _usingPlayerInput = false;
            moveAction = new InputAction("Move", InputActionType.Value, "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up",    "<Keyboard>/w")
                .With("Down",  "<Keyboard>/s")
                .With("Left",  "<Keyboard>/a")
                .With("Right", "<Keyboard>/d")
                .With("Up",    "<Keyboard>/upArrow")
                .With("Down",  "<Keyboard>/downArrow")
                .With("Left",  "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");

            sprintAction = new InputAction("Sprint", InputActionType.Button);
            sprintAction.AddBinding("<Keyboard>/leftShift");
            sprintAction.AddBinding("<Gamepad>/rightTrigger");
        }
    }

    void OnEnable()
    {
        if (!_usingPlayerInput)
        {
            moveAction?.Enable();
            sprintAction?.Enable();
        }
    }

    void OnDisable()
    {
        if (!_usingPlayerInput)
        {
            moveAction?.Disable();
            sprintAction?.Disable();
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (movimientoBloqueado) { movement = Vector3.zero; return; }

        Vector2 inputValues = moveAction != null
            ? moveAction.ReadValue<Vector2>()
            : Vector2.zero;

        movement = new Vector3(inputValues.x, 0f, inputValues.y).normalized;

        bool quiereSprintar = sprintAction != null && sprintAction.IsPressed();
        bool estaMoviendose = movement != Vector3.zero;
        _sprintando = quiereSprintar && estaMoviendose && staminaActual > 0f;

        if (_sprintando)
        {
            staminaActual -= consumoPorSegundo * Time.deltaTime;
            staminaActual = Mathf.Clamp(staminaActual, 0f, staminaMaxima);
            if (staminaActual <= 0f) _sprintando = false;
        }

        if (movement != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movement);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }

    void FixedUpdate()
    {
        if (movimientoBloqueado) { rb.MovePosition(rb.position); return; }
        float velocidadActual = _sprintando ? sprintSpeed : speed;
        rb.MovePosition(rb.position + movement * velocidadActual * Time.fixedDeltaTime);
    }

    public float PorcentajeStamina() => staminaActual / staminaMaxima;
}