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

    [Header("Prop cosmético del minijuego (martillo)")]
    [Tooltip("Prefab visual del martillo. Se instancia en la mano SOLO durante el minijuego — nunca es un objeto recogible del mundo, no pasa por heldItem.")]
    public GameObject martilloPrefab;

    public GameObject currentItemOnTable;

    private enum Estado { Vacia, EsperandoIniciar, Reparando, Lista }
    private Estado _estado = Estado.Vacia;

    private int _pulsacionesCompletadas = 0;
    private GameObject _martilloInstanciado = null;

    // Jugador que está usando ESTA mesa — su input es el único que cuenta
    private PlayerInteraction _jugadorReparando = null;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (barraUI != null) barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);
    }

    // ── Interacción principal (E) ─────────────────────────────────────────────

    public override void Interact(PlayerInteraction player)
    {
        bool playerHasItem = player.isHoldingItem;
        bool tableHasItem = currentItemOnTable != null;

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
        currentItemOnTable.transform.localScale = Vector3.one;

        if (currentItemOnTable.GetComponent<Collider>() != null)
            currentItemOnTable.GetComponent<Collider>().enabled = true;

        player.isHoldingItem = false;
        player.heldItem = null;
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
        player.heldItem = currentItemOnTable;

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

        if (barraUI != null) barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);
    }

    void IniciarMinijuego(PlayerInteraction player)
    {
        _jugadorReparando = player;

        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm != null) pm.movimientoBloqueado = true;

        // Rotar al jugador para que mire directo al centro de la mesa —
        // así el golpe (que apunta "hacia adelante" del personaje) siempre
        // conecta bien, sin importar desde qué ángulo se haya acercado.
        Vector3 direccionHaciaMesa = (tableSocket != null ? tableSocket.position : transform.position) - player.transform.position;
        direccionHaciaMesa.y = 0f; // solo rotar en el plano horizontal, no inclinar al personaje
        if (direccionHaciaMesa.sqrMagnitude > 0.001f)
            player.transform.rotation = Quaternion.LookRotation(direccionHaciaMesa);

        _estado = Estado.Reparando;
        _pulsacionesCompletadas = 0;

        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);
        if (barraUI != null) barraUI.SetActive(true);
        ActualizarBarra();

        // Instanciar el martillo cosmético directo en el hueso de la mano
        // (NO en ItemSocket — ese cuelga del torso/cabeza y no se mueve con
        // el swing, solo los huesos de la mano se animan). Nunca pasa por
        // heldItem — las manos del jugador siguen "vacías" para el resto
        // del juego, esto es puramente visual y vive solo aquí.
        Transform armature = player.transform.Find("Armature");
        Transform manoDer = armature != null ? BuscarHijoPorNombre(armature, "base_mano_der") : null;

        if (martilloPrefab != null && manoDer != null)
        {
            _martilloInstanciado = Instantiate(martilloPrefab, manoDer);
            _martilloInstanciado.transform.localPosition = player.offsetMartillo;
            _martilloInstanciado.transform.localRotation = Quaternion.Euler(player.rotacionMartillo);

            // Compensar la escala real del hueso (varía entre personajes) para
            // que el martillo se vea del mismo tamaño real sin importar a quién
            // esté pegado — mismo truco que ya usas en laptop/extintor/piezas.
            Vector3 escalaHueso = manoDer.lossyScale;
            _martilloInstanciado.transform.localScale = new Vector3(
                player.escalaMartillo / escalaHueso.x,
                player.escalaMartillo / escalaHueso.y,
                player.escalaMartillo / escalaHueso.z);

            // Igual que con laptop/extintor/piezas: si tiene Rigidbody y
            // Collider, hay que apagarlos — si no, la gravedad lo jala y
            // termina cayendo sobre la mesa en vez de quedarse en la mano.
            Rigidbody rb = _martilloInstanciado.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            Collider col = _martilloInstanciado.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        // Prender el interruptor propio del martillo en el Animator — no
        // toca isHolding ni TipoObjeto, así que PlayerInteraction ni se
        // entera y no hay nada que "suprimir".
        if (player.anim != null)
            player.anim.SetBool("SosteniendoMartillo", true);

        Debug.Log("RepairTable: ¡Minijuego iniciado! Pulsa el botón para reparar.");
    }

    void ContarPulsacion()
    {
        _pulsacionesCompletadas++;
        ActualizarBarra();
        Debug.Log($"RepairTable: Pulsación {_pulsacionesCompletadas}/{pulsacionesRequeridas}");

        // Disparar el martillazo — una sola vez por cada pulsación
        if (_jugadorReparando != null && _jugadorReparando.anim != null)
            _jugadorReparando.anim.SetTrigger("Martillazo");

        if (_pulsacionesCompletadas >= pulsacionesRequeridas)
            CompletarReparacion();
    }

    void CompletarReparacion()
    {
        PlayerInteraction jugador = _jugadorReparando; // guardar antes de que se limpie
        DesbloquearJugador();

        _estado = Estado.Lista;

        Laptop laptopScript = currentItemOnTable.GetComponent<Laptop>();
        if (laptopScript != null)
            laptopScript.ResolverProblema(Laptop.Problema.Reparacion);
        if (barraUI != null) barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);

        // Quitar el martillo y apagar su interruptor en el Animator
        if (_martilloInstanciado != null) Destroy(_martilloInstanciado);
        _martilloInstanciado = null;

        if (jugador != null && jugador.anim != null)
            jugador.anim.SetBool("SosteniendoMartillo", false);

        // if (jugador != null) jugador.GetComponent<GestureController>()?.MostrarGesto(TipoGesto.Feliz); // pendiente: integrar sistema de gestos

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

    /// <summary>
    /// Busca un hijo por nombre en toda la jerarquía (sin importar cuántos
    /// niveles de profundidad, ni cómo se llame el padre). Se usa para
    /// encontrar la mano sin depender del nombre exacto de la pelvis,
    /// que puede variar entre personajes ("base" vs "Base").
    /// </summary>
    Transform BuscarHijoPorNombre(Transform padre, string nombre)
    {
        foreach (Transform hijo in padre)
        {
            if (hijo.name == nombre) return hijo;

            Transform encontrado = BuscarHijoPorNombre(hijo, nombre);
            if (encontrado != null) return encontrado;
        }
        return null;
    }

    void ActualizarBarra()
    {
        if (barraRelleno != null)
            barraRelleno.fillAmount = (float)_pulsacionesCompletadas / pulsacionesRequeridas;
    }

    /// <summary>Resetea la mesa por completo al iniciar un nivel nuevo.</summary>
    public void LimpiarParaNuevoNivel()
    {
        // Si el nivel se reinicia a mitad de una reparación, hay que apagar
        // el interruptor del martillo y quitar el prop que quedó pegado
        if (_jugadorReparando != null && _jugadorReparando.anim != null)
            _jugadorReparando.anim.SetBool("SosteniendoMartillo", false);

        if (_martilloInstanciado != null) Destroy(_martilloInstanciado);
        _martilloInstanciado = null;

        DesbloquearJugador();
        _estado = Estado.Vacia;
        _pulsacionesCompletadas = 0;
        currentItemOnTable = null;

        if (barraUI != null) barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);
    }
}