using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Identidad del Jugador")]
    public int playerId = 1;

    [Header("Animaciones")]
    public Animator anim;

    [Header("Configuración del Radar (BoxCast)")]
    public Transform castPoint;
    public Vector3 boxSize = new Vector3(0.5f, 0.5f, 0.5f);
    public float castDistance = 1.5f;
    public LayerMask interactableLayer;

    [Header("Sistema de Manos")]
    public Transform itemSocket;
    public bool isHoldingItem = false;
    public GameObject heldItem = null;

    [Header("Físicas y Colisiones (Anti-Traspaso)")]
    public BoxCollider playerCollider;

    [Header("Valores sin objeto")]
    public Vector3 tamañoNormal    = new Vector3(1f, 2f, 1f);
    public Vector3 centroNormal    = new Vector3(0f, 0f, 0f);

    [Header("Valores cargando objeto")]
    public Vector3 tamañoConObjeto = new Vector3(1f, 2f, 1.5f);
    public Vector3 centroConObjeto = new Vector3(0f, 0f, 0.25f);

    // ── Estado detectado por el radar ─────────────────────────────────────────
    private InteractableTable _currentTable;
    private GameObject        _currentItemOnFloor;

    private bool        _usingPlayerInput = false;
    private InputAction interactAction;
    private InputAction _useAction;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        PlayerInput pi = GetComponent<PlayerInput>();
        if (pi != null)
        {
            _usingPlayerInput = true;
            var map      = pi.actions.FindActionMap("Player");
            interactAction = map.FindAction("Interact");
            _useAction     = map.FindAction("Secondary");
        }
        else
        {
            _usingPlayerInput = false;
            interactAction = new InputAction("Interact", InputActionType.Button);
            interactAction.AddBinding("<Keyboard>/e");
            interactAction.AddBinding("<Gamepad>/buttonSouth");

            _useAction = new InputAction("Use", InputActionType.Button);
            _useAction.AddBinding("<Keyboard>/q");
            _useAction.AddBinding("<Gamepad>/buttonWest");
        }
    }

    void OnEnable()
    {
        if (!_usingPlayerInput)
        {
            interactAction?.Enable();
            _useAction?.Enable();
        }
    }

    void OnDisable()
    {
        if (!_usingPlayerInput)
        {
            interactAction?.Disable();
            _useAction?.Disable();
        }
    }

    void Update()
    {
        DetectAhead();

        if (interactAction != null && interactAction.WasPressedThisFrame())
        {
            if (_currentTable != null)
            {
                _currentTable.Interact(this);
            }
            else if (_currentItemOnFloor != null && !isHoldingItem)
            {
                Laptop laptopEnSuelo = _currentItemOnFloor.GetComponent<Laptop>();
                if (laptopEnSuelo != null && !laptopEnSuelo.PuedeRecogerse())
                    Debug.Log("PlayerInteraction: ¡Está en llamas! Usa el extintor primero.");
                else
                    RecogerDelSuelo(_currentItemOnFloor);
            }
            else if (isHoldingItem)
            {
                SoltarObjetoEnElSuelo();
            }
        }

        // Interacción secundaria (Q / X): avisar a la mesa que este jugador apunta.
        // Excepción: si carga el extintor, Q es para rociar, no para la mesa.
        bool cargaExtintor = isHoldingItem && heldItem != null &&
                             heldItem.GetComponent<Extinguisher>() != null;

        if (!cargaExtintor && _useAction != null && _useAction.WasPressedThisFrame() && _currentTable != null)
        {
            _currentTable.SecondaryInteract(this);
        }

        ActualizarUsoExtintor();
    }

    // ── Detección ─────────────────────────────────────────────────────────────

    void DetectAhead()
    {
        RaycastHit hit;
        bool hasHit = Physics.BoxCast(
            castPoint.position, boxSize / 2,
            castPoint.forward, out hit,
            castPoint.rotation, castDistance,
            interactableLayer);

        if (hasHit)
        {
            InteractableTable seenTable = hit.collider.GetComponent<InteractableTable>();
            if (seenTable != null)
            {
                ClearItemOnFloor();
                if (seenTable != _currentTable)
                {
                    if (_currentTable != null) _currentTable.OnLookExit(playerId);
                    _currentTable = seenTable;
                    _currentTable.OnLookEnter(playerId);
                }
                return;
            }

            LaptopController seenLaptop = hit.collider.GetComponent<LaptopController>();
            if (seenLaptop != null && seenLaptop.isInteractable)
            {
                if (_currentTable != null) { _currentTable.OnLookExit(playerId); _currentTable = null; }
                _currentItemOnFloor = seenLaptop.gameObject;
                return;
            }

            PickableItem seenItem = hit.collider.GetComponent<PickableItem>();
            if (seenItem != null && seenItem.isInteractable)
            {
                if (_currentTable != null) { _currentTable.OnLookExit(playerId); _currentTable = null; }
                _currentItemOnFloor = seenItem.gameObject;
                return;
            }
        }

        if (_currentTable != null) { _currentTable.OnLookExit(playerId); _currentTable = null; }
        ClearItemOnFloor();
    }

    void ClearItemOnFloor() => _currentItemOnFloor = null;

    // ── Acciones ──────────────────────────────────────────────────────────────

    void RecogerDelSuelo(GameObject item)
    {
        isHoldingItem = true;
        heldItem      = item;

        Vector3 escalaOriginal = item.transform.lossyScale;
        item.transform.SetParent(itemSocket);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
        item.transform.localScale = new Vector3(
            escalaOriginal.x / itemSocket.lossyScale.x,
            escalaOriginal.y / itemSocket.lossyScale.y,
            escalaOriginal.z / itemSocket.lossyScale.z);

        if (item.GetComponent<Rigidbody>() != null)
            item.GetComponent<Rigidbody>().isKinematic = true;
        if (item.GetComponent<Collider>() != null)
            item.GetComponent<Collider>().enabled = false;

        LaptopController lc = item.GetComponent<LaptopController>();
        if (lc != null) lc.enabled = false;

        PickableItem pi = item.GetComponent<PickableItem>();
        if (pi != null) pi.isInteractable = false;

        if (anim != null) anim.SetBool("isHolding", true);
        AjustarCollider(true);

        Laptop laptopRecogida = item.GetComponent<Laptop>();
        if (laptopRecogida != null) laptopRecogida.OnRecogida(this);

        _currentItemOnFloor = null;
    }

    void SoltarObjetoEnElSuelo()
    {
        heldItem.transform.SetParent(null);
        heldItem.transform.localScale = Vector3.one;

        Vector3 dropPos = transform.position + transform.forward * 0.8f + Vector3.up * 0.5f;
        heldItem.transform.position = dropPos;

        if (heldItem.GetComponent<Collider>() != null)
            heldItem.GetComponent<Collider>().enabled = true;

        Rigidbody rb = heldItem.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = false; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

        LaptopController lc = heldItem.GetComponent<LaptopController>();
        if (lc != null) { lc.enabled = true; lc.isInteractable = true; }

        PickableItem pi = heldItem.GetComponent<PickableItem>();
        if (pi != null) pi.isInteractable = true;

        if (anim != null) anim.SetBool("isHolding", false);
        AjustarCollider(false);

        Laptop laptopTirada = heldItem.GetComponent<Laptop>();
        if (laptopTirada != null) laptopTirada.OnDejada();

        Extinguisher extTirado = heldItem.GetComponent<Extinguisher>();
        if (extTirado != null && extTirado.espumaParticles != null)
            extTirado.espumaParticles.Stop();

        isHoldingItem = false;
        heldItem      = null;
    }

    // ── Uso del extintor ──────────────────────────────────────────────────────

    void ActualizarUsoExtintor()
    {
        if (!isHoldingItem) return;

        Extinguisher ext = heldItem.GetComponent<Extinguisher>();
        if (ext == null) return;

        bool usando = _useAction != null && _useAction.IsPressed() && ext.TieneCarga();

        if (ext.espumaParticles != null)
        {
            if (usando  && !ext.espumaParticles.isPlaying) ext.espumaParticles.Play();
            if (!usando &&  ext.espumaParticles.isPlaying) ext.espumaParticles.Stop();
        }

        if (!usando) return;

        ext.Consumir(Time.deltaTime);

        Collider[] hits = Physics.OverlapSphere(castPoint.position, ext.radioEfecto);
        foreach (Collider col in hits)
        {
            IExtinguishable objetivo = col.GetComponent<IExtinguishable>();
            if (objetivo == null || !objetivo.EstaEnPeligro()) continue;

            Vector3 dir    = (col.transform.position - castPoint.position).normalized;
            float   angulo = Vector3.Angle(castPoint.forward, dir);
            if (angulo <= ext.anguloEfecto)
                objetivo.RecibirChorroExtintor(Time.deltaTime);
        }
    }

    // ── Gizmos y utilidades ───────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (castPoint == null) return;
        Gizmos.color  = Color.cyan;
        Gizmos.matrix = Matrix4x4.TRS(castPoint.position, castPoint.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.forward * (castDistance / 2),
                            new Vector3(boxSize.x, boxSize.y, castDistance));
    }

    public void AjustarCollider(bool tieneObjeto)
    {
        if (playerCollider == null) return;
        playerCollider.size   = tieneObjeto ? tamañoConObjeto : tamañoNormal;
        playerCollider.center = tieneObjeto ? centroConObjeto : centroNormal;
    }
}