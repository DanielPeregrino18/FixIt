using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Singleton que controla el tiempo, laptops perdidas, 
/// condiciones de game over y cálculo de estrellas.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ── Configuración del nivel ───────────────────────────────────────────────
    [Header("Configuración del Nivel")]
    [Tooltip("Duración total del nivel en segundos (300 = 5 minutos)")]
    public float duracionNivel = 300f;

    [Tooltip("Porcentaje de la barra donde cae la horda (0-1). Ej: 0.5 = mitad del turno")]
    [Range(0f, 1f)]
    public float marcaHorda = 0.5f;

    [Tooltip("Máximo de laptops perdidas antes de Game Over")]
    public int maxLaptopsPermitidas = 6;

    [Header("Mecánica de Incendios")]
    [Tooltip("Activa o desactiva el sistema de incendios para este nivel")]
    public bool incendiosActivos = false;

    // ── UI ────────────────────────────────────────────────────────────────────
    [Header("UI - Barra de Tiempo")]
    public Image barraProgreso;         // barra de progreso del turno
    public Image marcaHordaUI;          // línea roja de la horda
    public TextMeshProUGUI textoHora;   // reloj digital "8:00 AM"

    [Header("UI - Contadores")]
    public TextMeshProUGUI textoLaptopsEntregadas;
    public TextMeshProUGUI textoLaptopsPerdidas;

    [Header("UI - Pantalla de Resultados")]
    public GameObject pantallaResultados;
    public TextMeshProUGUI textoResultado;    // "¡Turno completado!" o "Game Over"
    public GameObject[] estrellas;           // 3 objetos estrella, se activan según resultado
    public GameObject botonReintentar;
    public GameObject botonContinuar;
    public GameObject botonSalirMenu;

    [Header("UI - Fondo y Tabla (Resultados)")]
    [Tooltip("Imagen de fondo a pantalla completa que se muestra al ganar")]
    public GameObject fondoVictoria;
    [Tooltip("Imagen de fondo a pantalla completa que se muestra al perder")]
    public GameObject fondoDerrota;
    [Tooltip("El Image de la tabla (clipboard) del centro; cambia de sprite según el resultado")]
    public Image imagenTabla;
    public Sprite spriteTablaVictoria;
    public Sprite spriteTablaDerrota;

    [Header("UI - Estrellas (Resultados)")]
    [Tooltip("Sprite de estrella ganada")]
    public Sprite spriteEstrellaLlena;
    [Tooltip("Sprite de estrella no ganada")]
    public Sprite spriteEstrellaVacia;

    [Header("UI - Estilo de botones (Resultados)")]
    [Tooltip("El botón en la posición 0 de la lista de activos usa este sprite (acción recomendada)")]
    public Sprite spriteBotonPrimario;
    [Tooltip("El resto de los botones usa este sprite")]
    public Sprite spriteBotonSecundario;
    [Tooltip("Salir al Menú siempre usa este sprite, sin importar su posición")]
    public Sprite spriteBotonSalir;

    [Header("UI - Mensaje aleatorio en Resultados")]
    public TextMeshProUGUI textoMensaje;
    [Tooltip("Se elige uno al azar cuando el nivel termina en Game Over")]
    public string[] mensajesGameOver = {
        "Bermúdez no puede enterarse de esto.",
        "González va a necesitar una buena excusa.",
        "El área de TI vivió días mejores."
    };
    [Tooltip("Se elige uno al azar con 1 estrella")]
    public string[] mensajes1Estrella = {
        "Sobreviviste... apenas.",
        "Un día más en la trinchera de TI."
    };
    [Tooltip("Se elige uno al azar con 2 estrellas")]
    public string[] mensajes2Estrellas = {
        "Nada mal para ser becario.",
        "El área empieza a respirar."
    };
    [Tooltip("Se elige uno al azar con 3 estrellas")]
    public string[] mensajes3Estrellas = {
        "¡Servicio impecable!",
        "González estaría orgulloso (si se enterara)."
    };

    // ── Estado interno ────────────────────────────────────────────────────────
    private float _timerActual = 0f;

    /// <summary>Segundos transcurridos desde que inició el nivel (para que otros scripts, como Server, programen eventos).</summary>
    public float TiempoTranscurrido => _timerActual;

    // NUEVO: el nivel ya NO empieza activo por defecto — espera a IniciarNivel()
    private bool _nivelActivo = false;
    private bool _hordaActivada = false;
    private int _nivelActualNumero = 0; // para guardar el progreso al terminar

    // Contadores
    private int _laptopsEntregadas = 0;
    private int _laptopsTotales = 0; // total que llegaron en el nivel
    private int _laptopsPerdidas = 0;
    private int _piezasDesperdiciadas = 0;

    // Horario: 8 AM a 4 PM = 8 horas
    private const int HORA_INICIO = 8;
    private const int HORA_FIN = 16;

    // ── Navegación de la pantalla de Resultados (flechas + confirmar) ─────────
    private List<GameObject> _botonesResultadosActivos = new List<GameObject>();
    private int _indiceResultados = 0;
    private float _timerNavResultados = 0f;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // Posicionar la marca de horda en la barra (solo visual, no depende del timer)
        if (marcaHordaUI != null)
        {
            RectTransform rt = marcaHordaUI.GetComponent<RectTransform>();
            RectTransform barraRT = barraProgreso.GetComponent<RectTransform>();
            if (rt != null && barraRT != null)
            {
                float anchoBarra = barraRT.rect.width;
                rt.anchoredPosition = new Vector2(anchoBarra * marcaHorda, rt.anchoredPosition.y);
            }
        }

        if (pantallaResultados != null) pantallaResultados.SetActive(false);

        // NUEVO: el reloj queda en pausa en 8:00 hasta que IniciarNivel() lo active
        ActualizarReloj(0f);
        ActualizarUI();
    }

    void Update()
    {
        // Mientras se muestra la pantalla de Resultados, solo procesamos su
        // navegación por flechas — el reloj del nivel ya está detenido (timeScale 0)
        // pero Update() sigue corriendo cada frame igualmente.
        if (pantallaResultados != null && pantallaResultados.activeSelf)
        {
            ActualizarNavegacionResultados();
            return;
        }

        if (!_nivelActivo) return;

        _timerActual += Time.deltaTime;
        float progreso = Mathf.Clamp01(_timerActual / duracionNivel);

        // Actualizar barra
        if (barraProgreso != null)
            barraProgreso.fillAmount = progreso;

        // Actualizar reloj digital
        ActualizarReloj(progreso);

        // Verificar horda
        if (!_hordaActivada && progreso >= marcaHorda)
        {
            _hordaActivada = true;
            OnHordaActivada();
        }

        // Fin del tiempo
        if (_timerActual >= duracionNivel)
            TerminarNivel();
    }

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>
    /// Arranca el reloj y los contadores del nivel. Llamar justo cuando el
    /// nivel realmente empieza (WaveManager.IniciarNivel), no al cargar la escena.
    /// </summary>
    public void IniciarNivel(int numeroNivel = 0)
    {
        _timerActual = 0f;
        _hordaActivada = false;
        _laptopsEntregadas = 0;
        _laptopsTotales = 0;
        _laptopsPerdidas = 0;
        _piezasDesperdiciadas = 0;
        _nivelActivo = true;
        _nivelActualNumero = numeroNivel;

        Time.timeScale = 1f; // por si venía en pausa de un nivel anterior
        if (pantallaResultados != null) pantallaResultados.SetActive(false);

        ActualizarUI();
        ActualizarReloj(0f);
        if (barraProgreso != null) barraProgreso.fillAmount = 0f;

        // NUEVO: desbloquear movimiento por si venía de un TerminarNivel/GameOver anterior
        PlayerMovement[] movs = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (PlayerMovement m in movs)
            m.movimientoBloqueado = false;

        Debug.Log("GameManager: Nivel iniciado, reloj en 8:00 AM.");
    }

    /// <summary>Llamar cuando se entrega una laptop completa.</summary>
    public void RegistrarLaptopEntregada()
    {
        if (!_nivelActivo) return;
        _laptopsEntregadas++;
        ActualizarUI();
        Debug.Log($"GameManager: Laptop entregada. Total: {_laptopsEntregadas}");
    }

    /// <summary>Llamar cuando llega una laptop nueva al nivel.</summary>
    public void RegistrarLaptopLlegada()
    {
        if (!_nivelActivo) return;
        _laptopsTotales++;
        ActualizarUI();
    }

    /// <summary>Llamar cuando una laptop se pierde (bote, mal entregada, quemada).</summary>
    public void RegistrarLaptopPerdida()
    {
        if (!_nivelActivo) return;
        _laptopsPerdidas++;
        ActualizarUI();
        // GestureController.MostrarGestoATodos(TipoGesto.Estresado); // pendiente: integrar sistema de gestos
        Debug.Log($"GameManager: Laptop perdida. Total perdidas: {_laptopsPerdidas}");

        if (_laptopsPerdidas >= maxLaptopsPermitidas)
            GameOver("Demasiadas laptops perdidas.");
    }

    /// <summary>Llamar cuando se desperdicia una pieza de hardware.</summary>
    public void RegistrarPiezaDesperdiciada()
    {
        if (!_nivelActivo) return;
        _piezasDesperdiciadas++;
        Debug.Log($"GameManager: Pieza desperdiciada. Total: {_piezasDesperdiciadas}");
    }

    /// <summary>Llamar cuando un servidor explota — insta Game Over.</summary>
    public void ServidorExplotado()
    {
        if (!_nivelActivo) return;
        GameOver("¡Un servidor explotó!");
    }

    // ── Lógica interna ────────────────────────────────────────────────────────

    void ActualizarReloj(float progreso)
    {
        if (textoHora == null) return;

        // Interpolar entre 8:00 y 16:00 según el progreso
        float horasTotales = HORA_FIN - HORA_INICIO; // 8 horas
        float horaActual = HORA_INICIO + (horasTotales * progreso);
        int horas = Mathf.FloorToInt(horaActual);
        int minutos = Mathf.FloorToInt((horaActual - horas) * 60f);
        string ampm = horas < 12 ? "AM" : "PM";
        int horas12 = horas > 12 ? horas - 12 : horas;

        textoHora.text = $"{horas12}:{minutos:D2} {ampm}";
    }

    void OnHordaActivada()
    {
        Debug.Log("GameManager: ¡Hora de comida! Viene la horda.");
        // El Spawner escuchará esto para lanzar la oleada
        // LaptopSpawner.Instance.ActivarHorda(); // lo conectamos cuando hagamos el spawner por oleadas
    }

    void TerminarNivel()
    {
        _nivelActivo = false;
        Time.timeScale = 0f;
        BloquearJugadores();

        // NUEVO: cualquier laptop que siga en el nivel (mesas, suelo, manos)
        // nunca se entregó — se cuenta como perdida antes de calcular estrellas.
        LimpiarLaptopsSinEntregar(contarComoPerdidas: true);

        int estrellaGanadas = CalcularEstrellas();

        // NUEVO: guardar el progreso
        if (ProgressManager.Instance != null && _nivelActualNumero > 0)
            ProgressManager.Instance.GuardarResultado(_nivelActualNumero, estrellaGanadas);

        MostrarResultados(estrellaGanadas, false);
    }

    void GameOver(string motivo)
    {
        _nivelActivo = false;
        Time.timeScale = 0f;
        BloquearJugadores();

        // Limpieza de objetos por prolijidad (no afecta el resultado, ya es 0 estrellas)
        LimpiarLaptopsSinEntregar(contarComoPerdidas: false);

        Debug.Log($"GameManager: GAME OVER — {motivo}");
        MostrarResultados(0, true, motivo);
    }

    /// <summary>
    /// Destruye cualquier laptop que siga en el nivel (mesas, suelo, en manos)
    /// al terminar. Si contarComoPerdidas es true, cada una suma a _laptopsPerdidas
    /// para que quede reflejado que nunca se entregó.
    /// </summary>
    void LimpiarLaptopsSinEntregar(bool contarComoPerdidas)
    {
        Laptop[] restantes = FindObjectsByType<Laptop>(FindObjectsSortMode.None);
        int contador = 0;

        foreach (Laptop l in restantes)
        {
            if (l == null) continue;

            if (contarComoPerdidas)
                _laptopsPerdidas++;

            Destroy(l.gameObject);
            contador++;
        }

        if (contador > 0)
        {
            Debug.Log($"GameManager: {contador} laptop(s) sin entregar al terminar el nivel.");
            ActualizarUI();
        }
    }

    /// <summary>Bloquea el movimiento de todos los jugadores activos (fin de nivel / game over).</summary>
    void BloquearJugadores()
    {
        PlayerMovement[] movs = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (PlayerMovement m in movs)
            m.movimientoBloqueado = true;
    }

    int CalcularEstrellas()
    {
        // ⭐ 1 estrella — pasaste el nivel (sobreviviste sin Game Over)
        // ⭐⭐ 2 estrellas — cero piezas desperdiciadas Y entregaste al menos una laptop
        //     (evita el exploit de "no hacer nada" = cero desperdicio trivial)
        // ⭐⭐⭐ 3 estrellas — 100% laptops entregadas correctamente

        if (_laptopsTotales == 0) return 1;

        bool entregoAlgo = _laptopsEntregadas > 0;
        bool cienPorCiento = _laptopsEntregadas == _laptopsTotales;
        bool sinDesperdicio = _piezasDesperdiciadas == 0;

        if (cienPorCiento && sinDesperdicio) return 3;
        if (sinDesperdicio && entregoAlgo) return 2;
        return 1;
    }

    void MostrarResultados(int estrellasGanadas, bool esGameOver, string motivo = "")
    {
        if (pantallaResultados == null) return;
        pantallaResultados.SetActive(true);

        if (textoResultado != null)
        {
            if (esGameOver)
                textoResultado.text = string.IsNullOrEmpty(motivo) ? "GAME OVER" : $"GAME OVER\n{motivo}";
            else
                textoResultado.text = "¡Turno completado!";
        }

        // Fondo y tabla según victoria / derrota
        if (fondoVictoria != null) fondoVictoria.SetActive(!esGameOver);
        if (fondoDerrota != null) fondoDerrota.SetActive(esGameOver);
        if (imagenTabla != null)
        {
            Sprite tabla = esGameOver ? spriteTablaDerrota : spriteTablaVictoria;
            if (tabla != null) imagenTabla.sprite = tabla;
        }

        // Estrellas: siempre visibles las 3, llena o vacía según lo ganado
        if (estrellas != null)
        {
            for (int i = 0; i < estrellas.Length; i++)
            {
                if (estrellas[i] == null) continue;
                estrellas[i].SetActive(true);
                Image imgEstrella = estrellas[i].GetComponent<Image>();
                if (imgEstrella == null) continue;
                Sprite s = (i < estrellasGanadas) ? spriteEstrellaLlena : spriteEstrellaVacia;
                if (s != null) imgEstrella.sprite = s;
            }
        }

        // Mensaje aleatorio según el resultado
        if (textoMensaje != null)
        {
            string[] pool = esGameOver ? mensajesGameOver
                          : estrellasGanadas >= 3 ? mensajes3Estrellas
                          : estrellasGanadas == 2 ? mensajes2Estrellas
                          : mensajes1Estrella;

            textoMensaje.text = (pool != null && pool.Length > 0)
                ? pool[Random.Range(0, pool.Length)]
                : "";
        }

        // Conectar los botones cada vez que se muestra la pantalla
        if (botonReintentar != null)
        {
            botonReintentar.SetActive(true);
            Button btn = botonReintentar.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(Reintentar);
            }
        }

        if (botonContinuar != null)
        {
            botonContinuar.SetActive(!esGameOver);
            Button btn = botonContinuar.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ContinuarAlCalendario);
            }
        }

        if (botonSalirMenu != null)
        {
            botonSalirMenu.SetActive(true);
            Button btn = botonSalirMenu.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(SalirAlMenu);
            }
        }

        // Construir la lista navegable en orden de prioridad: la posición 0 es
        // la acción recomendada (Continuar si ganaste, Reintentar si perdiste)
        // y recibe el sprite "primario" — el resto usa el sprite secundario.
        _botonesResultadosActivos.Clear();
        if (!esGameOver && botonContinuar != null) _botonesResultadosActivos.Add(botonContinuar);
        if (botonReintentar != null) _botonesResultadosActivos.Add(botonReintentar);
        if (botonSalirMenu != null) _botonesResultadosActivos.Add(botonSalirMenu);

        for (int i = 0; i < _botonesResultadosActivos.Count; i++)
        {
            Image img = _botonesResultadosActivos[i].GetComponent<Image>();
            if (img == null) continue;
            Sprite elegido = (i == 0) ? spriteBotonPrimario : spriteBotonSecundario;
            if (elegido != null) img.sprite = elegido; // si el campo está vacío, se respeta el sprite propio del botón
        }

        // Salir al Menú siempre es rojo, sin importar en qué posición quedó
        if (botonSalirMenu != null && spriteBotonSalir != null)
        {
            Image imgSalir = botonSalirMenu.GetComponent<Image>();
            if (imgSalir != null) imgSalir.sprite = spriteBotonSalir;
        }

        _indiceResultados = 0;
        ActualizarSeleccionResultados();
    }

    // ── Navegación con flechas/control en Resultados ──────────────────────────

    void ActualizarNavegacionResultados()
    {
        if (_botonesResultadosActivos.Count == 0) return;

        _timerNavResultados -= Time.unscaledDeltaTime;
        int dir = 0;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.wasPressedThisFrame) dir = -1;
            if (Keyboard.current.downArrowKey.wasPressedThisFrame) dir = 1;
        }

        if (dir == 0)
        {
            foreach (Gamepad gp in Gamepad.all)
            {
                if (gp.dpad.up.wasPressedThisFrame) { dir = -1; break; }
                if (gp.dpad.down.wasPressedThisFrame) { dir = 1; break; }

                if (_timerNavResultados <= 0f)
                {
                    Vector2 stick = gp.leftStick.ReadValue();
                    if (stick.y > 0.5f) { dir = -1; break; }
                    else if (stick.y < -0.5f) { dir = 1; break; }
                }
            }
        }

        if (dir != 0)
        {
            MoverSeleccionResultados(dir);
            _timerNavResultados = 0.2f;
        }

        bool confirmar = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        if (!confirmar)
        {
            foreach (Gamepad gp in Gamepad.all)
                if (gp.buttonSouth.wasPressedThisFrame) { confirmar = true; break; }
        }

        if (confirmar)
        {
            Button btn = _botonesResultadosActivos[_indiceResultados].GetComponent<Button>();
            btn?.onClick.Invoke();
        }
    }

    void MoverSeleccionResultados(int delta)
    {
        _indiceResultados = Mathf.Clamp(_indiceResultados + delta, 0, _botonesResultadosActivos.Count - 1);
        ActualizarSeleccionResultados();
    }

    void ActualizarSeleccionResultados()
    {
        for (int i = 0; i < _botonesResultadosActivos.Count; i++)
        {
            bool esFoco = i == _indiceResultados;
            GameObject boton = _botonesResultadosActivos[i];

            Outline outline = boton.GetComponent<Outline>();
            if (outline != null) outline.enabled = esFoco;

            boton.transform.localScale = esFoco ? Vector3.one * 1.08f : Vector3.one;
        }
    }

    /// <summary>True mientras un nivel está en curso (jugándose o en pausa). False en menú/lobby/resultados.</summary>
    public bool EstaJugando => _nivelActivo;

    /// <summary>Vuelve a cargar el mismo nivel que se acaba de jugar. Pública para el botón de Reintentar y la Pausa.</summary>
    public void Reintentar()
    {
        Time.timeScale = 1f;
        pantallaResultados.SetActive(false);

        int nivel = _nivelActualNumero;
        if (nivel <= 0) return;

        NivelData datos = NivelLoader.Instance.CargarNivel(nivel);
        if (datos != null)
            WaveManager.Instance.IniciarNivel(datos);
    }

    /// <summary>Regresa a la pantalla de selección de nivel (calendario).</summary>
    void ContinuarAlCalendario()
    {
        Time.timeScale = 1f;
        pantallaResultados.SetActive(false);

        // Desactivar movimiento/interacción de todos los jugadores
        PlayerJoinHandler[] jugadores = FindObjectsByType<PlayerJoinHandler>(FindObjectsSortMode.None);
        foreach (PlayerJoinHandler j in jugadores)
            j.ActivarModoJuego(false);

        GameFlowManager.Instance.MostrarSeleccionNivel();
    }

    /// <summary>
    /// Abandona el nivel en curso (llamado desde la Pausa) y regresa al menú principal.
    /// Detiene oleadas, destruye objetos restantes, oculta HUDs y resetea el lobby
    /// para que el próximo "Jugar" arranque desde cero.
    /// </summary>
    public void SalirAlMenu()
    {
        Time.timeScale = 1f;
        _nivelActivo = false;

        if (WaveManager.Instance != null)
            WaveManager.Instance.DetenerNivel();

        // Destruir laptops que queden para que no se vean moviéndose detrás del menú
        LimpiarLaptopsSinEntregar(contarComoPerdidas: false);

        // También piezas de hardware sueltas
        foreach (HardwarePiece p in FindObjectsByType<HardwarePiece>(FindObjectsSortMode.None))
            if (p != null) Destroy(p.gameObject);

        // Ocultar HUDs activos y desvincularlos
        foreach (PlayerHUD hud in FindObjectsByType<PlayerHUD>(FindObjectsSortMode.None))
        {
            hud.jugador = null;
            hud.gameObject.SetActive(false);
        }

        // Resetear el lobby: expulsa a todos los jugadores para que el próximo
        // "Jugar" pida unirse y elegir personaje desde cero
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.ReiniciarLobby();

        GameFlowManager.Instance.MostrarMenu();
    }

    void ActualizarUI()
    {
        if (textoLaptopsEntregadas != null)
            textoLaptopsEntregadas.text = $"Entregadas: {_laptopsEntregadas}";
        if (textoLaptopsPerdidas != null)
            textoLaptopsPerdidas.text = $"Perdidas: {_laptopsPerdidas}/{maxLaptopsPermitidas}";
    }
}