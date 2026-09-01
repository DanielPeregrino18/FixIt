using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class NormalTable : InteractableTable
{
    [Header("Estado de la Mesa")]
    public GameObject currentItemOnTable;
    public Transform tableSocket;

    [Header("Barra de Instalación Flotante")]
    public GameObject barraUI;
    public Image barraRelleno;

    [Header("Configuración de Instalación")]
    public float tiempoInstalacion = 3f;

    // ── Estado interno de instalación ─────────────────────────────────────────
    private enum EstadoInstalacion { Inactivo, Instalando }
    private EstadoInstalacion _estadoInstalacion = EstadoInstalacion.Inactivo;

    private float             _timerInstalacion  = 0f;
    private PlayerInteraction _jugadorInstalando = null;
    private GameObject        _piezaInstalando   = null;
    private InputAction       _accionDelJugador  = null; // Secondary del jugador instalando

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (barraUI != null) barraUI.SetActive(false);
    }

    void Update()
    {
        if (_estadoInstalacion != EstadoInstalacion.Instalando) return;

        // Verificar que el jugador que instala SIGA manteniendo SU botón
        bool sigueManteniendo = _accionDelJugador != null && _accionDelJugador.IsPressed();

        if (!sigueManteniendo)
        {
            CancelarInstalacion();
            return;
        }

        _timerInstalacion += Time.deltaTime;
        ActualizarBarra(_timerInstalacion / tiempoInstalacion);

        if (_timerInstalacion >= tiempoInstalacion)
            CompletarInstalacion();
    }

    // ── Interacción principal (E / botón A) ───────────────────────────────────

    public override void Interact(PlayerInteraction player)
    {
        if (_estadoInstalacion == EstadoInstalacion.Instalando)
        {
            Debug.Log("NormalTable: La mesa está ocupada.");
            return;
        }

        bool playerHasItem = player.isHoldingItem;
        bool tableHasItem  = currentItemOnTable != null;

        // Regla 1: Jugador vacío, Mesa tiene algo → recoger
        if (!playerHasItem && tableHasItem)
        {
            Laptop laptopCheck = currentItemOnTable.GetComponent<Laptop>();
            if (laptopCheck != null && !laptopCheck.PuedeRecogerse())
            {
                Debug.Log("NormalTable: ¡Está en llamas! Usa el extintor primero.");
                return;
            }

            player.isHoldingItem = true;
            player.heldItem      = currentItemOnTable;

            Vector3 escalaOriginal = currentItemOnTable.transform.lossyScale;
            currentItemOnTable.transform.SetParent(player.itemSocket);
            currentItemOnTable.transform.localPosition = Vector3.zero;
            currentItemOnTable.transform.localRotation = Quaternion.identity;
            currentItemOnTable.transform.localScale = new Vector3(
                escalaOriginal.x / player.itemSocket.lossyScale.x,
                escalaOriginal.y / player.itemSocket.lossyScale.y,
                escalaOriginal.z / player.itemSocket.lossyScale.z);

            if (currentItemOnTable.GetComponent<Rigidbody>() != null)
                currentItemOnTable.GetComponent<Rigidbody>().isKinematic = true;
            if (currentItemOnTable.GetComponent<Collider>() != null)
                currentItemOnTable.GetComponent<Collider>().enabled = false;

            if (player.anim != null) player.anim.SetBool("isHolding", true);
            player.AjustarCollider(true);

            if (laptopCheck != null) laptopCheck.OnRecogida(player);

            currentItemOnTable = null;
        }

        // Regla 2: Jugador tiene laptop, extintor o pieza, Mesa vacía → dejarlo
        else if (playerHasItem && !tableHasItem)
        {
            Laptop        laptopDelJugador = player.heldItem.GetComponent<Laptop>();
            Extinguisher  extDelJugador    = player.heldItem.GetComponent<Extinguisher>();
            HardwarePiece piezaDelJugador  = player.heldItem.GetComponent<HardwarePiece>();

            if (laptopDelJugador == null && extDelJugador == null && piezaDelJugador == null)
            {
                Debug.Log("NormalTable: No puedes dejar eso en esta mesa.");
                return;
            }

            currentItemOnTable = player.heldItem;

            currentItemOnTable.transform.SetParent(tableSocket);
            currentItemOnTable.transform.localPosition = Vector3.zero;
            currentItemOnTable.transform.localRotation = Quaternion.identity;
            currentItemOnTable.transform.localScale    = Vector3.one;

            if (player.anim != null) player.anim.SetBool("isHolding", false);
            player.AjustarCollider(false);

            if (currentItemOnTable.GetComponent<Collider>() != null)
                currentItemOnTable.GetComponent<Collider>().enabled = true;

            if (laptopDelJugador != null) laptopDelJugador.OnDejada();

            player.isHoldingItem = false;
            player.heldItem      = null;
        }

        // Regla 3: Jugador tiene pieza, Mesa tiene laptop → indicar que use Q
        else if (playerHasItem && tableHasItem)
        {
            HardwarePiece pieza = player.heldItem.GetComponent<HardwarePiece>();
            if (pieza == null)
            {
                Debug.Log("NormalTable: No puedes colocar eso aquí.");
                return;
            }

            if (currentItemOnTable.GetComponent<Laptop>() == null)
            {
                Debug.Log("NormalTable: La mesa está ocupada, no puedes instalar la pieza ahí.");
                return;
            }

            Debug.Log("NormalTable: Mantén presionado Q para instalar la pieza.");
        }

        else
        {
            Debug.Log("Mesa vacía y jugador vacío. No hay acción.");
        }
    }

    // ── Interacción secundaria (Q) — llega SOLO del jugador que apunta ────────

    public override void SecondaryInteract(PlayerInteraction player)
    {
        if (_estadoInstalacion != EstadoInstalacion.Inactivo) return;
        if (currentItemOnTable == null) return;
        if (currentItemOnTable.GetComponent<Laptop>() == null) return;

        HardwarePiece pieza = player.heldItem != null
            ? player.heldItem.GetComponent<HardwarePiece>()
            : null;
        if (pieza == null) return;

        // Obtener la acción Secondary DEL jugador para verificar que la mantenga
        _accionDelJugador = null;
        PlayerInput pi = player.GetComponent<PlayerInput>();
        if (pi != null)
        {
            var mapa = pi.actions.FindActionMap("Player");
            if (mapa != null) _accionDelJugador = mapa.FindAction("Secondary");
        }

        IniciarInstalacion(player, pieza);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void IniciarInstalacion(PlayerInteraction player, HardwarePiece pieza)
    {
        _jugadorInstalando = player;
        _piezaInstalando   = player.heldItem;
        _timerInstalacion  = 0f;
        _estadoInstalacion = EstadoInstalacion.Instalando;

        if (barraUI != null) barraUI.SetActive(true);
        ActualizarBarra(0f);

        Debug.Log($"NormalTable: Instalando {pieza.tipoPieza}...");
    }

    void CancelarInstalacion()
    {
        _estadoInstalacion = EstadoInstalacion.Inactivo;
        _timerInstalacion  = 0f;
        _jugadorInstalando = null;
        _piezaInstalando   = null;
        _accionDelJugador  = null;

        if (barraUI != null) barraUI.SetActive(false);

        Debug.Log("NormalTable: Instalación cancelada.");
    }

    void CompletarInstalacion()
    {
        _estadoInstalacion = EstadoInstalacion.Inactivo;

        HardwarePiece pieza        = _piezaInstalando.GetComponent<HardwarePiece>();
        Laptop        laptopScript = currentItemOnTable.GetComponent<Laptop>();

        if (laptopScript != null && pieza != null)
        {
            Laptop.Problema problema = pieza.ProblemaCorrespondiente();

            if (laptopScript.problemasActivos.Contains(problema))
            {
                laptopScript.ResolverProblema(problema);
                Debug.Log($"NormalTable: {pieza.tipoPieza} instalada correctamente.");
            }
            else
            {
                Debug.Log($"NormalTable: {pieza.tipoPieza} desperdiciada.");
                // GameManager.instance.RegistrarPiezaDesperdiciada(); // futuro
            }
        }

        Destroy(_piezaInstalando);

        if (_jugadorInstalando != null)
        {
            _jugadorInstalando.isHoldingItem = false;
            _jugadorInstalando.heldItem      = null;
            if (_jugadorInstalando.anim != null)
                _jugadorInstalando.anim.SetBool("isHolding", false);
            _jugadorInstalando.AjustarCollider(false);
        }

        _jugadorInstalando = null;
        _piezaInstalando   = null;
        _accionDelJugador  = null;
        _timerInstalacion  = 0f;

        if (barraUI != null) barraUI.SetActive(false);
    }

    void ActualizarBarra(float valor)
    {
        if (barraRelleno != null)
            barraRelleno.fillAmount = Mathf.Clamp01(valor);
    }

    /// <summary>Resetea la mesa por completo al iniciar un nivel nuevo (borra referencias a objetos de la sesión anterior).</summary>
    public void LimpiarParaNuevoNivel()
    {
        _estadoInstalacion = EstadoInstalacion.Inactivo;
        _timerInstalacion  = 0f;
        _jugadorInstalando = null;
        _piezaInstalando   = null;
        _accionDelJugador  = null;
        currentItemOnTable = null;

        if (barraUI != null) barraUI.SetActive(false);
    }
}