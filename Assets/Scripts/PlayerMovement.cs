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
    public float staminaMaxima = 100f;
    public float staminaActual = 100f;
    public float consumoPorSegundo = 10f;

    [HideInInspector] public bool movimientoBloqueado = false;

    // ── Variables internas ────────────────────────────────────────────────────
    private Rigidbody rb;
    private Vector3 movement;
    private bool _sprintando = false;
    private bool _usingPlayerInput = false;

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
            var map = pi.actions.FindActionMap("Player");
            moveAction = map.FindAction("Move");
            sprintAction = map.FindAction("Sprint");
        }
        else
        {
            // Fallback: crear acciones manualmente (para pruebas sin PlayerInput)
            _usingPlayerInput = false;
            moveAction = new InputAction("Move", InputActionType.Value, "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
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
        Vector3 delta = movement * velocidadActual * Time.fixedDeltaTime;

        float distancia = delta.magnitude;
        if (distancia > 0.00001f)
            delta = RecortarPaso(delta / distancia, distancia);

        rb.MovePosition(rb.position + delta);
    }

    // ── Colisiones: nunca dar un paso que meta al personaje dentro de una pared ──

    [Header("Colisiones")]
    [Tooltip("Separación mínima que se deja con la pared (unidades del mundo)")]
    public float margenPared = 0.02f;

    /// <summary>
    /// Lanza el cuerpo del personaje hacia donde va. Si choca con algo sólido, recorta
    /// el paso hasta justo antes del choque y desliza lo que sobra a lo largo de la pared.
    /// </summary>
    Vector3 RecortarPaso(Vector3 dir, float dist)
    {
        if (!BuscarChoque(dir, dist, out RaycastHit hit))
            return dir * dist;

        float permitido = Mathf.Clamp(hit.distance - margenPared, 0f, dist);
        Vector3 resultado = dir * permitido;
        float restante = dist - permitido;

        // Deslizar a lo largo de la pared con lo que sobró del paso
        Vector3 deslizar = Vector3.ProjectOnPlane(dir, hit.normal);
        deslizar.y = 0f;
        if (restante > 0.00001f && deslizar.sqrMagnitude > 0.0001f)
        {
            deslizar.Normalize();
            float d2 = restante * Mathf.Abs(Vector3.Dot(dir, deslizar)); // menos paso cuanto más de frente se choca
            if (BuscarChoque(deslizar, d2, out RaycastHit hit2))
                d2 = Mathf.Clamp(hit2.distance - margenPared, 0f, d2);
            resultado += deslizar * d2;
        }

        return resultado;
    }

    /// <summary>Devuelve el choque sólido más cercano en esa dirección (ignora triggers y suelo).</summary>
    bool BuscarChoque(Vector3 dir, float dist, out RaycastHit mejor)
    {
        mejor = default;
        if (dist <= 0.00001f) return false;

        RaycastHit[] choques = rb.SweepTestAll(dir, dist + margenPared, QueryTriggerInteraction.Ignore);
        float masCercano = float.MaxValue;
        bool hay = false;

        foreach (RaycastHit h in choques)
        {
            if (h.distance <= 0f) continue;                 // ya se solapaba: lo resuelve la física
            if (Mathf.Abs(h.normal.y) > 0.5f) continue;     // suelo / techo, no es pared
            if (h.distance < masCercano)
            {
                masCercano = h.distance;
                mejor = h;
                hay = true;
            }
        }
        return hay;
    }

    public float PorcentajeStamina() => staminaActual / staminaMaxima;

    /// <summary>Deja al personaje listo para empezar un nivel: stamina llena, sin movimiento pendiente.</summary>
    public void ReiniciarEstado()
    {
        staminaActual = staminaMaxima;
        movimientoBloqueado = false;
        movement = Vector3.zero;
        _sprintando = false;

        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}