using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Qué tipo de objeto tiene el jugador en la mano, para elegir la animación correcta.</summary>
public enum TipoObjetoSostenido { Ninguno, Laptop, Extintor, RAM, HDD, Procesador }

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

    [Header("Objeto bajo los pies (respaldo)")]
    [Tooltip("Zona que busca objetos recogibles justo donde está parado el jugador. Selecciona al jugador para ver el cubo verde en el Scene.")]
    public Vector3 tamañoBajoPies = new Vector3(1.2f, 1f, 1.2f);
    [Tooltip("Centro de esa zona, relativo al jugador")]
    public Vector3 centroBajoPies = new Vector3(0f, 0.3f, 0f);

    [Header("Sistema de Manos")]
    public Transform itemSocket;
    public bool isHoldingItem = false;
    public GameObject heldItem = null;

    [Header("Físicas y Colisiones (Anti-Traspaso)")]
    public BoxCollider playerCollider;

    [Header("Valores sin objeto")]
    public Vector3 tamañoNormal = new Vector3(1f, 2f, 1f);
    public Vector3 centroNormal = new Vector3(0f, 0f, 0f);

    [Header("Valores cargando objeto")]
    public Vector3 tamañoConObjeto = new Vector3(1f, 2f, 1.5f);
    public Vector3 centroConObjeto = new Vector3(0f, 0f, 0.25f);

    // ── Estado detectado por el radar ─────────────────────────────────────────
    private InteractableTable _currentTable;
    private GameObject _currentItemOnFloor;
    private GameObject _itemBajoPies;

    private bool _usingPlayerInput = false;
    private InputAction interactAction;
    private InputAction _useAction;

    // Para detectar cuándo cambia el objeto en mano y actualizar el Animator
    private GameObject _ultimoHeldItemParaAnim = null;

    [Header("Corrección visual del extintor")]
    [Tooltip("El modelo del extintor apunta la boquilla al revés — se corrige rotándolo al agarrarlo. Ajusta el eje/ángulo hasta que se vea bien.")]
    public Vector3 correccionRotacionExtintor = new Vector3(0f, 180f, 0f);

    [Header("Ajuste del martillo (varía por personaje, lo llena CharacterSwapper)")]
    public Vector3 offsetMartillo = Vector3.zero;
    public Vector3 rotacionMartillo = Vector3.zero;
    public float escalaMartillo = 2f;

    [Header("Ajuste de la lata de aire (varía por personaje, lo llena CharacterSwapper)")]
    public Vector3 offsetLata = Vector3.zero;
    public Vector3 rotacionLata = Vector3.zero;
    public float escalaLata = 2f;

    [Header("Ajuste de la taza de café (varía por personaje, lo llena CharacterSwapper)")]
    public Vector3 offsetTaza = Vector3.zero;
    public Vector3 rotacionTaza = Vector3.zero;
    public float escalaTaza = 2f;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        PlayerInput pi = GetComponent<PlayerInput>();
        if (pi != null)
        {
            _usingPlayerInput = true;
            var map = pi.actions.FindActionMap("Player");
            interactAction = map.FindAction("Interact");
            _useAction = map.FindAction("Secondary");
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
        // En pausa (o en resultados) el jugador no puede interactuar con nada
        if (Time.timeScale == 0f) return;

        DetectAhead();
        ActualizarItemBajoPies();

        if (interactAction != null && interactAction.WasPressedThisFrame())
        {
            if (_currentTable != null)
            {
                bool manosLibresAntes = !isHoldingItem;
                _currentTable.Interact(this);

                // Si la mesa no te dio nada, recoger lo que haya bajo los pies
                if (manosLibresAntes && !isHoldingItem)
                    IntentarRecogerBajoPies();
            }
            else if (_currentItemOnFloor != null && !isHoldingItem)
            {
                Laptop laptopEnSuelo = _currentItemOnFloor.GetComponent<Laptop>();
                if (laptopEnSuelo != null && !laptopEnSuelo.PuedeRecogerse())
                    Debug.Log("PlayerInteraction: ¡Está en llamas! Usa el extintor primero.");
                else
                    RecogerDelSuelo(_currentItemOnFloor);
            }
            else if (!isHoldingItem && _itemBajoPies != null)
            {
                // Sin mesa ni objeto delante, pero hay uno bajo los pies
                IntentarRecogerBajoPies();
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
        ActualizarAnimacionSostenido();
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

    // ── Objeto bajo los pies ──────────────────────────────────────────────────
    // El BoxCast no detecta lo que ya está solapado con él al empezar, así que un
    // objeto bajo el jugador no se ve. Esta zona lo busca aparte; solo se usa como
    // respaldo cuando la mesa de enfrente no hizo nada.

    void ActualizarItemBajoPies()
    {
        _itemBajoPies = null;
        if (isHoldingItem) return;

        Vector3 centro = transform.TransformPoint(centroBajoPies);
        Collider[] cercanos = Physics.OverlapBox(centro, tamañoBajoPies / 2f, transform.rotation, interactableLayer);

        float mejorDist = float.MaxValue;
        foreach (Collider c in cercanos)
        {
            GameObject candidato = null;

            LaptopController lc = c.GetComponent<LaptopController>();
            if (lc != null && lc.isInteractable)
            {
                Laptop l = c.GetComponent<Laptop>();
                if (l != null && l.yaEntregada) continue; // ya entregada, sigue en la banda
                candidato = lc.gameObject;
            }
            else
            {
                PickableItem pk = c.GetComponent<PickableItem>();
                if (pk != null && pk.isInteractable) candidato = pk.gameObject;
            }

            if (candidato == null) continue;

            float d = (candidato.transform.position - centro).sqrMagnitude;
            if (d < mejorDist) { mejorDist = d; _itemBajoPies = candidato; }
        }
    }

    void IntentarRecogerBajoPies()
    {
        if (_itemBajoPies == null) return;

        Laptop laptop = _itemBajoPies.GetComponent<Laptop>();
        if (laptop != null && !laptop.PuedeRecogerse())
        {
            Debug.Log("PlayerInteraction: ¡Está en llamas! Usa el extintor primero.");
            return;
        }

        RecogerDelSuelo(_itemBajoPies);
        _itemBajoPies = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.matrix = Matrix4x4.TRS(transform.TransformPoint(centroBajoPies), transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, tamañoBajoPies);
    }

    // ── Acciones ──────────────────────────────────────────────────────────────

    void RecogerDelSuelo(GameObject item)
    {
        isHoldingItem = true;
        heldItem = item;

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
            extTirado.espumaParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        isHoldingItem = false;
        heldItem = null;
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
            if (usando && !ext.espumaParticles.isPlaying) ext.espumaParticles.Play();
            if (!usando && ext.espumaParticles.isPlaying)
                ext.espumaParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // corte instantáneo, sin dejar que las ya existentes se desvanezcan solas
        }

        if (!usando) return;

        ext.Consumir(Time.deltaTime);

        Collider[] hits = Physics.OverlapSphere(castPoint.position, ext.radioEfecto);
        foreach (Collider col in hits)
        {
            IExtinguishable objetivo = col.GetComponent<IExtinguishable>();
            if (objetivo == null || !objetivo.EstaEnPeligro()) continue;

            Vector3 dir = (col.transform.position - castPoint.position).normalized;
            float angulo = Vector3.Angle(castPoint.forward, dir);
            if (angulo <= ext.anguloEfecto)
                objetivo.RecibirChorroExtintor(Time.deltaTime);
        }
    }

    /// <summary>
    /// Revisa cada frame si cambió el objeto en la mano y actualiza el Animator
    /// con qué tipo es (Laptop, Extintor, RAM, HDD, Procesador o Ninguno).
    /// Funciona sin importar en qué mesa/script se haya recogido el objeto.
    /// </summary>
    void ActualizarAnimacionSostenido()
    {
        if (heldItem == _ultimoHeldItemParaAnim) return;
        _ultimoHeldItemParaAnim = heldItem;

        if (anim == null) return;

        TipoObjetoSostenido tipo = TipoObjetoSostenido.Ninguno;

        if (heldItem != null)
        {
            if (heldItem.GetComponent<Laptop>() != null)
            {
                tipo = TipoObjetoSostenido.Laptop;
            }
            else if (heldItem.GetComponent<Extinguisher>() != null)
            {
                tipo = TipoObjetoSostenido.Extintor;
            }
            else
            {
                HardwarePiece pieza = heldItem.GetComponent<HardwarePiece>();
                if (pieza != null)
                {
                    tipo = pieza.tipoPieza switch
                    {
                        HardwarePiece.TipoPieza.RAM => TipoObjetoSostenido.RAM,
                        HardwarePiece.TipoPieza.HDD => TipoObjetoSostenido.HDD,
                        HardwarePiece.TipoPieza.CPU => TipoObjetoSostenido.Procesador,
                        _ => TipoObjetoSostenido.Ninguno
                    };
                }
            }
        }

        anim.SetInteger("TipoObjeto", (int)tipo);
        anim.SetBool("isHolding", heldItem != null); // se mantiene por compatibilidad con lo ya existente

        // El modelo del extintor tiene la boquilla apuntando al revés —
        // se corrige solo mientras está en la mano (no afecta cómo se ve
        // parado en su estación o tirado en el suelo, eso lo maneja cada
        // script por su cuenta al soltarlo).
        if (tipo == TipoObjetoSostenido.Extintor && heldItem != null)
            heldItem.transform.localRotation = Quaternion.Euler(correccionRotacionExtintor);

        Debug.Log($"PlayerInteraction: Enviando al Animator TipoObjeto={(int)tipo} ({tipo})");
    }

    // ── Gizmos y utilidades ───────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (castPoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.matrix = Matrix4x4.TRS(castPoint.position, castPoint.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.forward * (castDistance / 2),
                            new Vector3(boxSize.x, boxSize.y, castDistance));
        Gizmos.matrix = Matrix4x4.identity;

        // Cono real de efecto del extintor — solo visible mientras lo sostienes,
        // para comparar el alcance real contra cómo se ve el chorro de partículas.
        if (heldItem != null)
        {
            Extinguisher ext = heldItem.GetComponent<Extinguisher>();
            if (ext != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f); // naranja, para distinguirlo del radar cian
                DibujarConoExtintor(ext.radioEfecto, ext.anguloEfecto);
            }
        }
    }

    void DibujarConoExtintor(float radio, float angulo)
    {
        Vector3 origen = castPoint.position;
        Vector3 adelante = castPoint.forward;
        int segmentos = 12;

        Vector3 anterior = Quaternion.AngleAxis(-angulo, Vector3.up) * adelante * radio;
        Gizmos.DrawLine(origen, origen + anterior);

        for (int i = 1; i <= segmentos; i++)
        {
            float t = Mathf.Lerp(-angulo, angulo, (float)i / segmentos);
            Vector3 punto = Quaternion.AngleAxis(t, Vector3.up) * adelante * radio;
            Gizmos.DrawLine(origen + anterior, origen + punto);
            Gizmos.DrawLine(origen, origen + punto);
            anterior = punto;
        }
    }

    public void AjustarCollider(bool tieneObjeto)
    {
        if (playerCollider == null) return;
        playerCollider.size = tieneObjeto ? tamañoConObjeto : tamañoNormal;
        playerCollider.center = tieneObjeto ? centroConObjeto : centroNormal;
    }
}