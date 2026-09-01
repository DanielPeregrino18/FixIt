using UnityEngine;
using UnityEngine.UI;

public class RepairTable : InteractableTable
{
    [Header("Slot de la Mesa")]
    public Transform tableSocket;

    [Header("Barra de Progreso Flotante")]
    public GameObject barraUI;
    public Image barraRelleno;

    [Header("Ícono de botón secundario")]
    public GameObject iconoBotonSecundario;

    [Header("Configuración del Minijuego")]
    public int pulsacionesRequeridas = 6;

    public GameObject currentItemOnTable;

    private enum Estado { Vacia, EsperandoIniciar, Reparando, Lista }
    private Estado _estado = Estado.Vacia;

    private int _pulsacionesCompletadas = 0;

    // Jugador que está usando ESTA mesa — su input es el único que cuenta
    private PlayerInteraction _jugadorReparando = null;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (barraUI != null)              barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);
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
                Debug.Log("RepairTable: ¡Está en llamas! Usa el extintor primero.");
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
            // No se puede reparar con las manos ocupadas
            if (player.isHoldingItem) return;
            IniciarMinijuego(player);
        }
        else if (_estado == Estado.Reparando && player == _jugadorReparando)
        {
            ContarPulsacion();
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

        bool necesitaReparacion = laptopScript != null &&
                                  laptopScript.problemasActivos.Contains(Laptop.Problema.Reparacion);

        if (necesitaReparacion)
        {
            _estado = Estado.EsperandoIniciar;
            if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(true);
        }
        else
        {
            _estado = Estado.Lista;
            Debug.Log("RepairTable: La laptop no necesita reparación.");
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
        _jugadorReparando = player;

        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm != null) pm.movimientoBloqueado = true;

        _estado                 = Estado.Reparando;
        _pulsacionesCompletadas = 0;

        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);
        if (barraUI != null)              barraUI.SetActive(true);
        ActualizarBarra();

        Debug.Log("RepairTable: ¡Minijuego iniciado! Pulsa el botón para reparar.");
    }

    void ContarPulsacion()
    {
        _pulsacionesCompletadas++;
        ActualizarBarra();
        Debug.Log($"RepairTable: Pulsación {_pulsacionesCompletadas}/{pulsacionesRequeridas}");

        if (_pulsacionesCompletadas >= pulsacionesRequeridas)
            CompletarReparacion();
    }

    void CompletarReparacion()
    {
        DesbloquearJugador();

        _estado = Estado.Lista;

        Laptop laptopScript = currentItemOnTable.GetComponent<Laptop>();
        if (laptopScript != null)
            laptopScript.ResolverProblema(Laptop.Problema.Reparacion);

        if (barraUI != null)              barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);

        Debug.Log("RepairTable: ¡Reparación completa!");
    }

    void DesbloquearJugador()
    {
        if (_jugadorReparando != null)
        {
            PlayerMovement pm = _jugadorReparando.GetComponent<PlayerMovement>();
            if (pm != null) pm.movimientoBloqueado = false;
            _jugadorReparando = null;
        }
    }

    void ActualizarBarra()
    {
        if (barraRelleno != null)
            barraRelleno.fillAmount = (float)_pulsacionesCompletadas / pulsacionesRequeridas;
    }

    /// <summary>Resetea la mesa por completo al iniciar un nivel nuevo.</summary>
    public void LimpiarParaNuevoNivel()
    {
        DesbloquearJugador();
        _estado                  = Estado.Vacia;
        _pulsacionesCompletadas  = 0;
        currentItemOnTable       = null;

        if (barraUI != null)              barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);
    }
}