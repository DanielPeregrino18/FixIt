using System.Collections;
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
    [Tooltip("Número de laptops entregadas CORRECTAMENTE (solo el número, ej. 3)")]
    public TextMeshProUGUI textoLaptopsEntregadas;
    [Tooltip("Laptops perdidas / máximo permitido (ej. 2/6)")]
    public TextMeshProUGUI textoLaptopsPerdidas;
    [Tooltip("Color del contador de perdidas cuando se está cerca de perder")]
    public Color colorPeligroPerdidas = new Color(0.85f, 0.1f, 0.1f, 1f);
    [Tooltip("Cuando faltan estas laptops (o menos) para perder, el contador de perdidas se pone en color de peligro")]
    public int avisoPeligroFaltan = 2;
    [Tooltip("Qué tanto crece el número al cambiar (1 = sin efecto)")]
    public float escalaPopContador = 1.35f;
    public float duracionPopContador = 0.25f;

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

    [Header("UI - Animación de Resultados")]
    [Tooltip("Canvas Group del objeto 'Botones' (el contenedor de los 3 botones). Hace el fade y bloquea clics mientras no aparecen")]
    public CanvasGroup grupoBotones;
    [Tooltip("Segundos que tarda en aparecer la tabla (pop de escala)")]
    public float duracionPopTabla = 0.35f;
    [Tooltip("Segundos que espera el mensaje del post-it antes de aparecer")]
    public float retrasoMensaje = 0.25f;
    public float duracionMensaje = 0.3f;
    [Tooltip("Segundos tras aparecer la tabla antes de que salga la primera estrella")]
    public float retrasoPrimeraEstrella = 0.1f;
    [Tooltip("Segundos entre una estrella GANADA y la siguiente")]
    public float intervaloEstrellas = 0.22f;
    [Tooltip("Segundos entre estrellas NO ganadas (más rápido, para no alargar la espera)")]
    public float intervaloEstrellasVacias = 0.08f;
    public float duracionPopEstrella = 0.2f;
    [Tooltip("Pausa tras la última estrella antes de que aparezcan los botones")]
    public float pausaAntesDeBotones = 0.1f;
    public float duracionBotones = 0.3f;
    [Tooltip("Cuánto suben los botones al aparecer (píxeles)")]
    public float distanciaSubidaBotones = 40f;
    [Tooltip("Segundos extra tras aparecer los botones antes de aceptar E / A. Evita que una pulsación del juego active un botón")]
    public float margenInput = 0.2f;

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
    private int _laptopsEsperadas = 0; // total que saldrá en el nivel (JSON × jugadores)
    private Coroutine _corrutinaFinAnticipado;

    [Header("Día del calendario (junto a los contadores)")]
    [Tooltip("Texto con el día del calendario del nivel actual (ej. 8). El ícono de calendario es una Image aparte")]
    public TextMeshProUGUI textoDia;
    [Tooltip("{0} se reemplaza por el día. Ej: \"{0}\" o \"Día {0}\"")]
    public string formatoDia = "{0}";

    [Header("Score")]
    [Tooltip("Texto donde se muestra el score en la pantalla de resultados")]
    public TextMeshProUGUI textoScore;
    [Tooltip("{0} se reemplaza por el número. Ej: \"Score: {0}\"")]
    public string formatoScore = "Score: {0}";
    public int puntosPorEstrella = 100;
    public float puntosPorSegundoSobrante = 1f;
    [Tooltip("Segundos que tarda el número en subir de 0 al score final")]
    public float duracionScore = 0.5f;
    public float pausaAntesDeScore = 0.1f;

    /// <summary>Score de la última partida (0 si fue Game Over). Para enviarlo al backend.</summary>
    public int ScoreFinal { get; private set; }

    private int _segundosSobrantes = 0;
    private int _scoreMostrado = 0;
    private Vector3 _escalaBaseScore = Vector3.one;

    [Header("Fin anticipado del nivel")]
    [Tooltip("Segundos de pausa antes de mostrar resultados cuando ya no queda nada por resolver")]
    public float retrasoFinAnticipado = 1f;
    private int _piezasDesperdiciadas = 0;

    // Horario: 8 AM a 4 PM = 8 horas
    private const int HORA_INICIO = 8;
    private const int HORA_FIN = 16;

    // ── Navegación de la pantalla de Resultados (flechas + confirmar) ─────────
    private List<GameObject> _botonesResultadosActivos = new List<GameObject>();
    private int _indiceResultados = 0;
    private float _timerNavResultados = 0f;

    // Animación / bloqueo de input de la pantalla de resultados
    private Coroutine _rutinaResultados;
    private bool _resultadosListos = false;  // false mientras se anima: se ignora todo el input
    private bool _stickYaNeutral = false;  // el stick debe volver al centro una vez antes de navegar con él
    private bool _baseResultadosCapturada = false;
    private Vector3 _escalaBaseTabla = Vector3.one;
    private Vector3[] _escalaBaseEstrellas;
    private Vector2 _posBaseBotones;

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
        {
            _segundosSobrantes = 0; // se acabó el tiempo: sin bonus
            TerminarNivel();
        }
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
        _laptopsEsperadas = 0;
        _segundosSobrantes = 0;
        ScoreFinal = 0;
        if (_corrutinaFinAnticipado != null) { StopCoroutine(_corrutinaFinAnticipado); _corrutinaFinAnticipado = null; }
        _piezasDesperdiciadas = 0;
        _nivelActivo = true;
        _nivelActualNumero = numeroNivel;
        ActualizarDia();

        // La canción del nivel arranca (o se reinicia) desde el principio
        if (AudioManager.Instance != null) AudioManager.Instance.IniciarMusicaNivel(duracionNivel);

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

    /// <summary>
    /// Día del calendario de un nivel. Se cuentan solo lunes a viernes (5 días por semana),
    /// igual que en el calendario: niveles 1-5 → días 1-5, 6-10 → 8-12, 11-15 → 15-19, 16-20 → 22-26.
    /// </summary>
    public static int DiaDelCalendario(int nivel)
    {
        return nivel + 2 * ((nivel - 1) / 5);
    }

    void ActualizarDia()
    {
        if (textoDia == null) return;
        textoDia.text = _nivelActualNumero > 0
            ? string.Format(formatoDia, DiaDelCalendario(_nivelActualNumero))
            : "";
    }

    /// <summary>Llamar cuando se entrega una laptop completa.</summary>
    public void RegistrarLaptopEntregada()
    {
        if (!_nivelActivo) return;
        _laptopsEntregadas++;
        ActualizarUI();
        Debug.Log($"GameManager: Laptop entregada. Total: {_laptopsEntregadas}");
        RevisarFinAnticipado();
    }

    /// <summary>El WaveManager informa cuántas laptops saldrán en total en este nivel.</summary>
    public void DefinirLaptopsEsperadas(int total)
    {
        _laptopsEsperadas = total;
    }

    /// <summary>Si todas las laptops del nivel ya se entregaron o se perdieron, termina el nivel.</summary>
    void RevisarFinAnticipado()
    {
        if (!_nivelActivo || _corrutinaFinAnticipado != null) return;
        if (_laptopsEsperadas <= 0) return;
        if (_laptopsEntregadas + _laptopsPerdidas < _laptopsEsperadas) return;

        // La música empieza a bajar durante la pausa de antes de los resultados
        if (AudioManager.Instance != null) AudioManager.Instance.DetenerMusicaConFundido();

        // El bonus se mide ahora, no después de la pausa
        _segundosSobrantes = Mathf.Max(0, Mathf.FloorToInt(duracionNivel - _timerActual));
        Debug.Log($"GameManager: no queda nada por resolver — fin anticipado del nivel ({_segundosSobrantes} s sobrantes).");
        _corrutinaFinAnticipado = StartCoroutine(FinAnticipadoTrasPausa());
    }

    IEnumerator FinAnticipadoTrasPausa()
    {
        yield return new WaitForSeconds(retrasoFinAnticipado);
        _corrutinaFinAnticipado = null;
        if (_nivelActivo) TerminarNivel();
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
        else
            RevisarFinAnticipado();
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
        if (AudioManager.Instance != null) AudioManager.Instance.DetenerMusicaConFundido();
        Time.timeScale = 0f;
        BloquearJugadores();

        // NUEVO: cualquier laptop que siga en el nivel (mesas, suelo, manos)
        // nunca se entregó — se cuenta como perdida antes de calcular estrellas.
        LimpiarLaptopsSinEntregar(contarComoPerdidas: true);

        // Si con las laptops que quedaron sin entregar se llega (o pasa) del límite, es derrota
        if (_laptopsPerdidas >= maxLaptopsPermitidas)
        {
            GameOver("Demasiadas laptops perdidas.");
            return;
        }

        int estrellaGanadas = CalcularEstrellas();

        // NUEVO: guardar el progreso
        if (ProgressManager.Instance != null && _nivelActualNumero > 0)
            ProgressManager.Instance.GuardarResultado(_nivelActualNumero, estrellaGanadas);

        int score = estrellaGanadas * puntosPorEstrella
                  + Mathf.FloorToInt(_segundosSobrantes * puntosPorSegundoSobrante);
        Debug.Log($"GameManager: Score = {estrellaGanadas}★ × {puntosPorEstrella} + {_segundosSobrantes} s = {score}");

        MostrarResultados(estrellaGanadas, false, "", score);
    }

    void GameOver(string motivo)
    {
        _nivelActivo = false;
        if (AudioManager.Instance != null) AudioManager.Instance.DetenerMusicaConFundido();
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

            // Las ya entregadas (siguen en la banda de salida) se destruyen sin contarse
            if (l.yaEntregada)
            {
                Destroy(l.gameObject);
                continue;
            }

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

    void MostrarResultados(int estrellasGanadas, bool esGameOver, string motivo = "", int score = 0)
    {
        // "Siguiente nivel" solo si ganaste y no es el último nivel
        bool hayNivelSiguiente = !esGameOver && _nivelActualNumero > 0 &&
                                 _nivelActualNumero < ProgressManager.TOTAL_NIVELES;

        ScoreFinal = esGameOver ? 0 : score;
        _scoreMostrado = ScoreFinal;
        if (textoScore != null) textoScore.text = string.Format(formatoScore, 0);

        if (pantallaResultados == null) return;
        pantallaResultados.SetActive(true);

        // Sonido de victoria o de derrota
        if (AudioManager.Instance != null) AudioManager.Instance.ReproducirSonidoResultado(!esGameOver);

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
            botonContinuar.SetActive(hayNivelSiguiente);
            Button btn = botonContinuar.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ContinuarAlSiguienteNivel);
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
        if (hayNivelSiguiente && botonContinuar != null) _botonesResultadosActivos.Add(botonContinuar);
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

        IniciarSecuenciaResultados(estrellasGanadas);
    }

    // ── Animación de entrada de la pantalla de Resultados ─────────────────────
    // Se hace por código con tiempo REAL (unscaledDeltaTime) porque el juego está en
    // timeScale 0 cuando se muestran los resultados. Mientras dura, se ignora todo el input
    // (teclado, control y mouse) para que una pulsación hecha en el juego no active un botón.

    void IniciarSecuenciaResultados(int estrellasGanadas)
    {
        if (_rutinaResultados != null) StopCoroutine(_rutinaResultados);

        // Guardar los valores "normales" (los del Inspector) la primera vez
        if (!_baseResultadosCapturada)
        {
            _baseResultadosCapturada = true;
            if (imagenTabla != null) _escalaBaseTabla = imagenTabla.transform.localScale;

            int n = estrellas != null ? estrellas.Length : 0;
            _escalaBaseEstrellas = new Vector3[n];
            for (int i = 0; i < n; i++)
                _escalaBaseEstrellas[i] = estrellas[i] != null ? estrellas[i].transform.localScale : Vector3.one;

            if (textoScore != null) _escalaBaseScore = textoScore.transform.localScale;

            if (grupoBotones != null)
                _posBaseBotones = ((RectTransform)grupoBotones.transform).anchoredPosition;
        }

        _rutinaResultados = StartCoroutine(SecuenciaResultados(estrellasGanadas));
    }

    void DetenerSecuenciaResultados()
    {
        if (_rutinaResultados != null)
        {
            StopCoroutine(_rutinaResultados);
            _rutinaResultados = null;
        }
        _resultadosListos = false;
    }

    IEnumerator SecuenciaResultados(int estrellasGanadas)
    {
        _resultadosListos = false;
        _stickYaNeutral = false;

        int nEstrellas = estrellas != null ? estrellas.Length : 0;

        // ── Calcular los tiempos de cada elemento ────────────────────────────
        float tMensajeIni = retrasoMensaje;

        float[] tEstrellaIni = new float[nEstrellas];
        float tSiguiente = duracionPopTabla + retrasoPrimeraEstrella;
        for (int i = 0; i < nEstrellas; i++)
        {
            tEstrellaIni[i] = tSiguiente;
            // Tras una estrella ganada se espera más; tras una vacía, menos
            tSiguiente += (i < estrellasGanadas) ? intervaloEstrellas : intervaloEstrellasVacias;
        }
        float tUltimaEstrellaFin = nEstrellas > 0
            ? tEstrellaIni[nEstrellas - 1] + duracionPopEstrella
            : duracionPopTabla;

        float tScoreIni = tUltimaEstrellaFin + pausaAntesDeScore;
        float tScoreFin = tScoreIni + duracionScore;

        float tBotonesIni = (textoScore != null ? tScoreFin : tUltimaEstrellaFin) + pausaAntesDeBotones;
        float tBotonesFin = tBotonesIni + duracionBotones;
        float tTotal = tBotonesFin + margenInput;

        RectTransform rtBotones = grupoBotones != null ? (RectTransform)grupoBotones.transform : null;

        // ── Bucle de animación ───────────────────────────────────────────────
        float t = 0f;
        while (true)
        {
            // Tabla (incluye el post-it, que ya viene en la imagen)
            if (imagenTabla != null)
                imagenTabla.transform.localScale = _escalaBaseTabla * EaseOutBack(Progreso(t, 0f, duracionPopTabla));

            // Mensaje del post-it
            if (textoMensaje != null)
                textoMensaje.alpha = Progreso(t, tMensajeIni, duracionMensaje);

            // Estrellas, una por una
            for (int i = 0; i < nEstrellas; i++)
            {
                if (estrellas[i] == null) continue;
                estrellas[i].transform.localScale = _escalaBaseEstrellas[i] *
                    EaseOutBack(Progreso(t, tEstrellaIni[i], duracionPopEstrella));
            }

            // Score: aparece con pop y el número sube de 0 al valor final
            if (textoScore != null)
            {
                float ps = Progreso(t, tScoreIni, duracionScore);
                textoScore.alpha = Progreso(t, tScoreIni, 0.15f);
                textoScore.transform.localScale = _escalaBaseScore *
                    EaseOutBack(Progreso(t, tScoreIni, 0.25f));
                textoScore.text = string.Format(formatoScore, Mathf.RoundToInt(_scoreMostrado * ps));
            }

            // Botones: fade + subida
            if (grupoBotones != null)
            {
                float pb = Progreso(t, tBotonesIni, duracionBotones);
                float suave = 1f - Mathf.Pow(1f - pb, 3f);
                grupoBotones.alpha = suave;
                rtBotones.anchoredPosition = _posBaseBotones + Vector2.down * distanciaSubidaBotones * (1f - suave);
                bool visible = pb >= 1f;
                grupoBotones.interactable = visible;
                grupoBotones.blocksRaycasts = visible;
            }

            if (t >= tTotal) break;

            yield return null;
            t += Time.unscaledDeltaTime;
        }

        // Estado final exacto
        if (imagenTabla != null) imagenTabla.transform.localScale = _escalaBaseTabla;
        if (textoMensaje != null) textoMensaje.alpha = 1f;
        if (textoScore != null)
        {
            textoScore.alpha = 1f;
            textoScore.transform.localScale = _escalaBaseScore;
            textoScore.text = string.Format(formatoScore, _scoreMostrado);
        }
        for (int i = 0; i < nEstrellas; i++)
            if (estrellas[i] != null) estrellas[i].transform.localScale = _escalaBaseEstrellas[i];
        if (grupoBotones != null)
        {
            grupoBotones.alpha = 1f;
            grupoBotones.interactable = true;
            grupoBotones.blocksRaycasts = true;
            rtBotones.anchoredPosition = _posBaseBotones;
        }

        _resultadosListos = true;
        _rutinaResultados = null;
    }

    static float Progreso(float t, float inicio, float duracion)
    {
        if (duracion <= 0f) return t >= inicio ? 1f : 0f;
        return Mathf.Clamp01((t - inicio) / duracion);
    }

    /// <summary>Curva con un pequeño "rebote" al final (pop).</summary>
    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // ── Navegación con flechas/control en Resultados ──────────────────────────

    void ActualizarNavegacionResultados()
    {
        if (_botonesResultadosActivos.Count == 0) return;

        // Mientras se anima la pantalla, no se acepta ningún input
        if (!_resultadosListos) return;

        // Si el jugador llegó con el stick inclinado (moviéndose en el juego), no navegar con el
        // stick hasta que lo suelte al centro una vez.
        if (!_stickYaNeutral)
        {
            bool neutral = true;
            foreach (Gamepad gp in Gamepad.all)
                if (gp.leftStick.ReadValue().magnitude > 0.3f) { neutral = false; break; }
            if (neutral) _stickYaNeutral = true;
        }

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

                if (_stickYaNeutral && _timerNavResultados <= 0f)
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
            GameObject botonActivado = _botonesResultadosActivos[_indiceResultados];
            Debug.Log($"GameManager: Resultados — botón '{botonActivado.name}' activado con E / A.");
            Button btn = botonActivado.GetComponent<Button>();
            if (btn != null)
            {
                AudioManager.Confirmar();
                btn.onClick.Invoke();
            }
        }
    }

    void MoverSeleccionResultados(int delta)
    {
        int anterior = _indiceResultados;
        _indiceResultados = Mathf.Clamp(_indiceResultados + delta, 0, _botonesResultadosActivos.Count - 1);
        if (_indiceResultados != anterior) AudioManager.Navegar();
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
        DetenerSecuenciaResultados();
        pantallaResultados.SetActive(false);

        int nivel = _nivelActualNumero;
        if (nivel <= 0) return;

        NivelData datos = NivelLoader.Instance.CargarNivel(nivel);
        if (datos != null)
            WaveManager.Instance.IniciarNivel(datos);
    }

    /// <summary>Botón "Siguiente nivel": inicia directamente el nivel que sigue, sin pasar por el calendario.</summary>
    void ContinuarAlSiguienteNivel()
    {
        int siguiente = _nivelActualNumero + 1;

        // Seguridad: si no hay siguiente nivel o no está desbloqueado, volver al calendario
        bool puedeSeguir = siguiente <= ProgressManager.TOTAL_NIVELES &&
                           (ProgressManager.Instance == null || ProgressManager.Instance.EstaDesbloqueado(siguiente));
        if (!puedeSeguir)
        {
            VolverAlCalendario();
            return;
        }

        Time.timeScale = 1f;
        DetenerSecuenciaResultados();
        pantallaResultados.SetActive(false);

        NivelData datos = NivelLoader.Instance.CargarNivel(siguiente);
        if (datos != null)
            WaveManager.Instance.IniciarNivel(datos);
        else
        {
            Debug.LogError($"GameManager: No se pudo cargar el nivel {siguiente}.");
            VolverAlCalendario();
        }
    }

    /// <summary>Regresa a la pantalla de selección de nivel (calendario).</summary>
    void VolverAlCalendario()
    {
        Time.timeScale = 1f;
        DetenerSecuenciaResultados();
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
        if (AudioManager.Instance != null) AudioManager.Instance.DetenerMusicaInmediato();
        DetenerSecuenciaResultados();
        if (pantallaResultados != null) pantallaResultados.SetActive(false);

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

    // Estado del HUD de contadores
    private Color _colorBasePerdidas;
    private bool _colorBaseCapturado = false;
    private int _ultimoEntregadasUI = 0;
    private int _ultimoPerdidasUI = 0;
    private Coroutine _popEntregadas;
    private Coroutine _popPerdidas;

    void ActualizarUI()
    {
        // Correctas: solo el número
        if (textoLaptopsEntregadas != null)
        {
            textoLaptopsEntregadas.text = _laptopsEntregadas.ToString();

            if (_laptopsEntregadas > _ultimoEntregadasUI)
                HacerPopContador(textoLaptopsEntregadas.transform, ref _popEntregadas);
        }

        // Incorrectas (perdidas): N/máximo, y rojo cuando se está cerca de perder
        if (textoLaptopsPerdidas != null)
        {
            if (!_colorBaseCapturado)
            {
                _colorBasePerdidas = textoLaptopsPerdidas.color;
                _colorBaseCapturado = true;
            }

            textoLaptopsPerdidas.text = $"{_laptopsPerdidas}/{maxLaptopsPermitidas}";

            bool peligro = _laptopsPerdidas > 0 &&
                           (maxLaptopsPermitidas - _laptopsPerdidas) <= avisoPeligroFaltan;
            textoLaptopsPerdidas.color = peligro ? colorPeligroPerdidas : _colorBasePerdidas;

            if (_laptopsPerdidas > _ultimoPerdidasUI)
                HacerPopContador(textoLaptopsPerdidas.transform, ref _popPerdidas);
        }

        _ultimoEntregadasUI = _laptopsEntregadas;
        _ultimoPerdidasUI = _laptopsPerdidas;
    }

    void HacerPopContador(Transform objetivo, ref Coroutine rutina)
    {
        if (escalaPopContador <= 1f || objetivo == null || !objetivo.gameObject.activeInHierarchy) return;

        if (rutina != null) StopCoroutine(rutina);
        rutina = StartCoroutine(PopContador(objetivo));
    }

    IEnumerator PopContador(Transform objetivo)
    {
        float dur = Mathf.Max(0.01f, duracionPopContador);
        float transcurrido = 0f;

        while (transcurrido < dur)
        {
            if (objetivo == null) yield break;
            transcurrido += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(transcurrido / dur);
            float k = Mathf.Sin(p * Mathf.PI); // sube y vuelve a 1
            objetivo.localScale = Vector3.one * (1f + (escalaPopContador - 1f) * k);
            yield return null;
        }

        if (objetivo != null) objetivo.localScale = Vector3.one;
    }
}