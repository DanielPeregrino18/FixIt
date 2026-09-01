using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class CleaningTable : InteractableTable
{
    [Header("Slot de la Mesa")]
    public Transform tableSocket;

    [Header("Barra de Progreso Flotante")]
    public GameObject barraUI;
    public Image barraRelleno;

    [Header("Ícono de botón secundario")]
    public GameObject iconoBotonSecundario;

    [Header("Configuración del Minijuego")]
    public int movimientosRequeridos = 5;
    public float umbralMovimiento = 0.5f;

    public GameObject currentItemOnTable;

    private enum Estado { Vacia, EsperandoIniciar, Limpiando, Lista }
    private Estado _estado = Estado.Vacia;

    private int  _movimientosCompletados = 0;
    private bool _joystickFueIzquierda   = false;
    private bool _joystickFueDerecha     = false;

    // Jugador que está usando ESTA mesa — su joystick es el único que cuenta
    private PlayerInteraction _jugadorLimpiando = null;
    private InputAction       _moveDelJugador   = null;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (barraUI != null)              barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);
    }

    void Update()
    {
        if (_estado == Estado.Limpiando)
            LeerJoystick();
    }

    // ── Interacción principal (E) ─────────────────────────────────────────────

    public override void Interact(PlayerInteraction player)
    {
        bool playerHasItem = player.isHoldingItem;
        bool tableHasItem  = currentItemOnTable != null;

        if (playerHasItem && !tableHasItem)
        {
            DejarLaptop(player);
        }
        else if (!playerHasItem && tableHasItem &&
                (_estado == Estado.Lista || _estado == Estado.Vacia))
        {
            Laptop laptopCheck = currentItemOnTable.GetComponent<Laptop>();
            if (laptopCheck != null && !laptopCheck.PuedeRecogerse())
            {
                Debug.Log("CleaningTable: ¡Está en llamas! Usa el extintor primero.");
                return;
            }
            RecogerLaptop(player);
        }
    }

    // ── Interacción secundaria (Q) — llega SOLO del jugador que apunta ────────

    public override void SecondaryInteract(PlayerInteraction player)
    {
        if (_estado == Estado.EsperandoIniciar)
        {
            if (player.isHoldingItem) return; // manos ocupadas
            IniciarMinijuego(player);
        }
        else if (_estado == Estado.Limpiando && player == _jugadorLimpiando)
        {
            CancelarMinijuego();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void DejarLaptop(PlayerInteraction player)
    {
        currentItemOnTable = player.heldItem;

        currentItemOnTable.transform.SetParent(tableSocket);
        currentItemOnTable.transform.localPosition = Vector3.zero;
        currentItemOnTable.transform.localRotation = Quaternion.identity;
        currentItemOnTable.transform.localScale    = Vector3.one;

        if (currentItemOnTable.GetComponent<Collider>() != null)
            currentItemOnTable.GetComponent<Collider>().enabled = true;

        player.isHoldingItem = false;
        player.heldItem      = null;
        if (player.anim != null) player.anim.SetBool("isHolding", false);
        player.AjustarCollider(false);

        Laptop laptopScript = currentItemOnTable.GetComponent<Laptop>();
        if (laptopScript != null) laptopScript.OnDejada();

        bool necesitaLimpieza = laptopScript != null &&
                                laptopScript.problemasActivos.Contains(Laptop.Problema.Limpieza);

        if (necesitaLimpieza)
        {
            _estado = Estado.EsperandoIniciar;
            if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(true);
        }
        else
        {
            _estado = Estado.Lista;
        }
    }

    void RecogerLaptop(PlayerInteraction player)
    {
        DesbloquearJugador();

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

        Laptop laptopScript = currentItemOnTable.GetComponent<Laptop>();
        if (laptopScript != null) laptopScript.OnRecogida(player);

        currentItemOnTable = null;
        _estado = Estado.Vacia;

        if (barraUI != null)              barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);
    }

    void IniciarMinijuego(PlayerInteraction player)
    {
        _jugadorLimpiando = player;

        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm != null) pm.movimientoBloqueado = true;

        // Leer el joystick DEL jugador que limpia (input aislado por dispositivo)
        _moveDelJugador = null;
        PlayerInput pi = player.GetComponent<PlayerInput>();
        if (pi != null)
        {
            var mapa = pi.actions.FindActionMap("Player");
            if (mapa != null) _moveDelJugador = mapa.FindAction("Move");
        }

        _estado                 = Estado.Limpiando;
        _movimientosCompletados = 0;
        _joystickFueIzquierda   = false;
        _joystickFueDerecha     = false;

        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);
        if (barraUI != null)              barraUI.SetActive(true);
        ActualizarBarra();
    }

    void CancelarMinijuego()
    {
        DesbloquearJugador();

        _estado                 = Estado.EsperandoIniciar;
        _movimientosCompletados = 0;

        if (barraUI != null)              barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(true);
    }

    void CompletarLimpieza()
    {
        DesbloquearJugador();

        _estado = Estado.Lista;

        Laptop laptopScript = currentItemOnTable.GetComponent<Laptop>();
        if (laptopScript != null)
            laptopScript.ResolverProblema(Laptop.Problema.Limpieza);

        if (barraUI != null)              barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);
    }

    void DesbloquearJugador()
    {
        if (_jugadorLimpiando != null)
        {
            PlayerMovement pm = _jugadorLimpiando.GetComponent<PlayerMovement>();
            if (pm != null) pm.movimientoBloqueado = false;
            _jugadorLimpiando = null;
        }
        _moveDelJugador = null;
    }

    void LeerJoystick()
    {
        if (_moveDelJugador == null) return;

        float inputX = 0f;
        try { inputX = _moveDelJugador.ReadValue<Vector2>().x; }
        catch { inputX = _moveDelJugador.ReadValue<float>(); }

        if (inputX < -umbralMovimiento && !_joystickFueIzquierda)
        {
            _joystickFueIzquierda = true;
            _joystickFueDerecha   = false;
            ContarMovimiento();
        }
        else if (inputX > umbralMovimiento && !_joystickFueDerecha)
        {
            _joystickFueDerecha   = true;
            _joystickFueIzquierda = false;
            ContarMovimiento();
        }
        else if (Mathf.Abs(inputX) < umbralMovimiento * 0.3f)
        {
            _joystickFueIzquierda = false;
            _joystickFueDerecha   = false;
        }
    }

    void ContarMovimiento()
    {
        _movimientosCompletados++;
        ActualizarBarra();

        if (_movimientosCompletados >= movimientosRequeridos)
            CompletarLimpieza();
    }

    void ActualizarBarra()
    {
        if (barraRelleno != null)
            barraRelleno.fillAmount = (float)_movimientosCompletados / movimientosRequeridos;
    }

    /// <summary>Resetea la mesa por completo al iniciar un nivel nuevo.</summary>
    public void LimpiarParaNuevoNivel()
    {
        DesbloquearJugador();
        _moveDelJugador          = null;
        _estado                  = Estado.Vacia;
        _movimientosCompletados  = 0;
        currentItemOnTable       = null;

        if (barraUI != null)              barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);
    }
}