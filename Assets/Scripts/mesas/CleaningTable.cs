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

    [Header("Prop cosmético del minijuego (lata de aire comprimido)")]
    [Tooltip("Prefab visual de la lata. Se instancia en la mano SOLO durante el minijuego — nunca es un objeto recogible del mundo, no pasa por heldItem. Si tiene un ParticleSystem hijo posicionado en la boquilla, se reproduce solo mientras dura la limpieza.")]
    public GameObject lataPrefab;

    public GameObject currentItemOnTable;

    private enum Estado { Vacia, EsperandoIniciar, Limpiando, Lista }
    private Estado _estado = Estado.Vacia;

    private int _movimientosCompletados = 0;
    private bool _joystickFueIzquierda = false;
    private bool _joystickFueDerecha = false;
    private int _ultimoLadoContado = 0; // 0 = ninguno todavía, -1 = izquierda, 1 = derecha
    private GameObject _lataInstanciada = null;

    // Jugador que está usando ESTA mesa — su joystick es el único que cuenta
    private PlayerInteraction _jugadorLimpiando = null;
    private InputAction _moveDelJugador = null;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (barraUI != null) barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);
    }

    void Update()
    {
        // En pausa el joystick no debe avanzar la limpieza
        if (Time.timeScale == 0f) return;

        if (_estado == Estado.Limpiando)
            LeerJoystick();
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
                (_estado == Estado.Lista || _estado == Estado.Vacia || _estado == Estado.EsperandoIniciar))
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
        currentItemOnTable.transform.localScale = Vector3.one;

        if (currentItemOnTable.GetComponent<Collider>() != null)
            currentItemOnTable.GetComponent<Collider>().enabled = true;

        player.isHoldingItem = false;
        player.heldItem = null;
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
        _jugadorLimpiando = player;

        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm != null) pm.movimientoBloqueado = true;

        // Rotar al jugador para que mire directo al centro de la mesa —
        // mismo motivo que en RepairTable: evita ver la animación torcida
        // dependiendo del ángulo de acercamiento.
        Vector3 direccionHaciaMesa = (tableSocket != null ? tableSocket.position : transform.position) - player.transform.position;
        direccionHaciaMesa.y = 0f;
        if (direccionHaciaMesa.sqrMagnitude > 0.001f)
            player.transform.rotation = Quaternion.LookRotation(direccionHaciaMesa);

        // Leer el joystick DEL jugador que limpia (input aislado por dispositivo)
        _moveDelJugador = null;
        PlayerInput pi = player.GetComponent<PlayerInput>();
        if (pi != null)
        {
            var mapa = pi.actions.FindActionMap("Player");
            if (mapa != null) _moveDelJugador = mapa.FindAction("Move");
        }

        _estado = Estado.Limpiando;
        _movimientosCompletados = 0;
        _joystickFueIzquierda = false;
        _joystickFueDerecha = false;
        _ultimoLadoContado = 0;

        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);
        if (barraUI != null) barraUI.SetActive(true);
        ActualizarBarra();

        // Instanciar la lata directo en el hueso de la mano (NO en ItemSocket
        // — ese no se mueve con el swing izquierda/derecha, solo los huesos
        // de la mano se animan). Nunca pasa por heldItem.
        Transform armature = player.transform.Find("Armature");
        Transform manoDer = armature != null ? BuscarHijoPorNombre(armature, "base_mano_der") : null;

        if (lataPrefab != null && manoDer != null)
        {
            _lataInstanciada = Instantiate(lataPrefab, manoDer);
            _lataInstanciada.transform.localPosition = player.offsetLata;
            _lataInstanciada.transform.localRotation = Quaternion.Euler(player.rotacionLata);

            // Compensar la escala real del hueso (varía entre personajes)
            Vector3 escalaHueso = manoDer.lossyScale;
            _lataInstanciada.transform.localScale = new Vector3(
                player.escalaLata / escalaHueso.x,
                player.escalaLata / escalaHueso.y,
                player.escalaLata / escalaHueso.z);

            Rigidbody rb = _lataInstanciada.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            Collider col = _lataInstanciada.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Si la lata trae su propio ParticleSystem en la boquilla, reproducirlo
            ParticleSystem aire = _lataInstanciada.GetComponentInChildren<ParticleSystem>();
            if (aire != null) aire.Play();
        }

        // Prender el interruptor propio de la lata en el Animator — no toca
        // isHolding ni TipoObjeto, así que PlayerInteraction ni se entera.
        if (player.anim != null)
        {
            // Limpiar cualquier Trigger que haya quedado "armado" de una
            // sesión anterior (los Triggers se quedan pendientes hasta que
            // alguna transición los consume, y podrían dispararse solos más
            // tarde sin que el jugador haya presionado nada esta vez).
            player.anim.ResetTrigger("MoverIzquierda");
            player.anim.ResetTrigger("MoverDerecha");

            player.anim.SetBool("LimpiandoLata", true);
        }
    }

    void CancelarMinijuego()
    {
        PlayerInteraction jugador = _jugadorLimpiando; // guardar antes de que se limpie
        DesbloquearJugador();

        _estado = Estado.EsperandoIniciar;
        _movimientosCompletados = 0;

        if (barraUI != null) barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(true);

        QuitarLata(jugador);
    }

    void CompletarLimpieza()
    {
        PlayerInteraction jugador = _jugadorLimpiando; // guardar antes de que se limpie
        DesbloquearJugador();

        _estado = Estado.Lista;

        Laptop laptopScript = currentItemOnTable.GetComponent<Laptop>();
        if (laptopScript != null)
            laptopScript.ResolverProblema(Laptop.Problema.Limpieza);

        if (barraUI != null) barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);

        QuitarLata(jugador);

        // if (jugador != null) jugador.GetComponent<GestureController>()?.MostrarGesto(TipoGesto.Feliz); // pendiente: integrar sistema de gestos
    }

    /// <summary>Detiene el aire, destruye la lata y apaga su interruptor en el Animator.</summary>
    void QuitarLata(PlayerInteraction jugador)
    {
        if (_lataInstanciada != null) Destroy(_lataInstanciada);
        _lataInstanciada = null;

        if (jugador != null && jugador.anim != null)
            jugador.anim.SetBool("LimpiandoLata", false);
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
            _joystickFueDerecha = false;

            if (_jugadorLimpiando != null && _jugadorLimpiando.anim != null)
            {
                // Limpiar el Trigger contrario antes de disparar este — si no,
                // uno puede quedar "armado" esperando (por ejemplo, si presionas
                // el mismo lado 2 veces seguidas) y disparar solo más tarde,
                // en cuanto el Animator llegue por fin al estado que sí lo acepta.
                _jugadorLimpiando.anim.ResetTrigger("MoverDerecha");
                _jugadorLimpiando.anim.SetTrigger("MoverIzquierda");
            }

            // Solo cuenta si el último lado que contó fue el derecho (o si es
            // el primer movimiento) — repetir el mismo lado no avanza la barra
            if (_ultimoLadoContado != -1)
            {
                _ultimoLadoContado = -1;
                ContarMovimiento();
            }
        }
        else if (inputX > umbralMovimiento && !_joystickFueDerecha)
        {
            _joystickFueDerecha = true;
            _joystickFueIzquierda = false;

            if (_jugadorLimpiando != null && _jugadorLimpiando.anim != null)
            {
                _jugadorLimpiando.anim.ResetTrigger("MoverIzquierda");
                _jugadorLimpiando.anim.SetTrigger("MoverDerecha");
            }

            if (_ultimoLadoContado != 1)
            {
                _ultimoLadoContado = 1;
                ContarMovimiento();
            }
        }
        else if (Mathf.Abs(inputX) < umbralMovimiento * 0.3f)
        {
            _joystickFueIzquierda = false;
            _joystickFueDerecha = false;
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

    /// <summary>Resetea la mesa por completo al iniciar un nivel nuevo.</summary>
    public void LimpiarParaNuevoNivel()
    {
        // Si el nivel se reinicia a mitad de una limpieza, hay que apagar
        // el interruptor de la lata y quitar el prop que quedó pegado
        if (_jugadorLimpiando != null && _jugadorLimpiando.anim != null)
            _jugadorLimpiando.anim.SetBool("LimpiandoLata", false);

        if (_lataInstanciada != null) Destroy(_lataInstanciada);
        _lataInstanciada = null;

        DesbloquearJugador();
        _moveDelJugador = null;
        _estado = Estado.Vacia;
        _movimientosCompletados = 0;
        currentItemOnTable = null;

        if (barraUI != null) barraUI.SetActive(false);
        if (iconoBotonSecundario != null) iconoBotonSecundario.SetActive(false);
    }
}