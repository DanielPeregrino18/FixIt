using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ejecuta las oleadas del nivel actual según el JSON cargado.
/// Escala la cantidad de laptops según el número de jugadores:
/// total = base × multiplicador (1 jugador ×1, 2 ×1.8, 3 ×2.3, 4 ×2.6 por defecto),
/// repartido en ráfagas de máximo N laptops con un intervalo entre ráfagas.
/// El escalado no es lineal porque las mesas son las mismas sin importar cuántos jueguen.
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Spawn")]
    public Transform spawnPoint;
    public GameObject laptopPrefab;

    [Header("Escalado por jugadores")]
    [Tooltip("Cuántas veces la lista del JSON sale con 1, 2, 3 y 4 jugadores. No es lineal: las mesas son " +
             "las mismas sin importar cuántos jueguen, así que 4 jugadores no rinden 4 veces más")]
    public float[] multiplicadorPorJugadores = { 1f, 1.8f, 2.3f, 2.6f };
    [Tooltip("Pausa entre laptops de una misma ráfaga")]
    public float delayEntreHorda = 0.5f;
    [Tooltip("Laptops máximas por ráfaga")]
    public int maxPorRafaga = 3;
    [Tooltip("Segundos entre ráfagas de una misma oleada")]
    public float intervaloRafagas = 10f;

    [Header("Despliegue acelerado al final del nivel")]
    [Tooltip("A partir de este segundo, las oleadas usan el intervalo reducido")]
    public float segundoUmbralFinal = 200f;
    [Tooltip("Intervalo entre ráfagas para oleadas del tramo final")]
    public float intervaloRafagasFinal = 5f;

    [Header("Supervivencia — oleadas")]
    [Tooltip("Multiplica qué tan rápido sube la dificultad (1 = normal, 1.5 = 50% más rápido)")]
    public float velocidadDificultad = 1f;
    [Tooltip("Segundo en que sale la primera laptop")]
    public float primeraOleadaSupervivencia = 3f;
    [Tooltip("Segundos entre oleadas al empezar")]
    public float intervaloOleadaInicial = 24f;
    [Tooltip("Segundos entre oleadas cuando ya va muy avanzado (nunca baja de aquí)")]
    public float intervaloOleadaMinimo = 13f;
    [Tooltip("Qué tan rápido se acerca el intervalo al mínimo (segundos; más grande = más lento)")]
    public float suavidadIntervalo = 420f;
    [Tooltip("Laptops por oleada (con 1 jugador) al empezar")]
    public int laptopsPorOleadaInicial = 1;
    [Tooltip("Segundos que tarda en sumarse una laptop más por oleada. Crece poco a poco (las fracciones se " +
             "acumulan entre oleadas), no de golpe")]
    public float segundosPorLaptopExtra = 240f;
    [Tooltip("Máximo de laptops por oleada (con 1 jugador; luego se multiplica por jugadores)")]
    public int laptopsPorOleadaMaximo = 3;
    [Tooltip("A partir de este segundo las laptops pueden incendiarse")]
    public float segundoIncendiosLaptops = 90f;

    [Header("Supervivencia — respiros")]
    [Tooltip("A partir de este segundo, de vez en cuando hay un rato sin laptops para recuperarse")]
    public float segundoInicioRespiros = 300f;
    [Tooltip("Cada cuántas oleadas hay un respiro (mínimo y máximo, se elige al azar)")]
    public int oleadasEntreRespirosMin = 3;
    public int oleadasEntreRespirosMax = 5;
    [Tooltip("Segundos extra sin laptops que dura cada respiro")]
    public float duracionRespiro = 25f;

    [Header("Supervivencia — servidores")]
    [Tooltip("Segundo del primer incendio de servidor")]
    public float primerServidorSupervivencia = 120f;
    [Tooltip("Segundos entre incendios de servidor al principio")]
    public float intervaloServidorInicial = 120f;
    [Tooltip("Cuánto se acorta el intervalo con cada incendio")]
    public float reduccionIntervaloServidor = 12f;
    [Tooltip("Intervalo mínimo entre incendios de servidor")]
    public float intervaloServidorMinimo = 50f;
    [Tooltip("A partir de este segundo se incendian dos servidores a la vez")]
    public float segundoServidoresDobles = 720f;

    [Header("Supervivencia — laptop dorada")]
    [Tooltip("Segundo a partir del cual puede salir la primera laptop dorada")]
    public float segundoPrimeraDorada = 120f;
    [Tooltip("Segundos mínimos entre una dorada y la siguiente")]
    public float intervaloMinimoDorada = 90f;
    [Tooltip("Probabilidad de que una laptop sea dorada (cuando ya se puede)")]
    [Range(0f, 1f)] public float probabilidadDorada = 0.25f;
    public Color colorDorado = new Color(1f, 0.78f, 0.15f, 1f);

    [Header("Materiales de color (uno por laptop)")]
    public Material[] materialesColor;

    // ── Estado interno ────────────────────────────────────────────────────────
    private NivelData _nivelActual;
    private float _proximaDoradaPosible = 0f;
    // Lista final de laptops de cada oleada (ya escalada), calculada una vez al cargar el nivel
    private readonly Dictionary<OleadaData, List<string>> _laptopsPorOleada = new Dictionary<OleadaData, List<string>>();

    private static readonly Laptop.Problema[] _problemasFacil = {
        Laptop.Problema.Limpieza,
        Laptop.Problema.Actualizacion,
        Laptop.Problema.Reparacion
    };

    private static readonly Laptop.Problema[] _problemasMedia = {
        Laptop.Problema.Limpieza,
        Laptop.Problema.Actualizacion,
        Laptop.Problema.Reparacion,
        Laptop.Problema.NecesitaRAM,
        Laptop.Problema.NecesitaHDD,
        Laptop.Problema.NecesitaProcesador
    };

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Inicia el nivel con los datos del JSON. Seguro de llamar en cualquier
    /// momento, incluso a mitad de otro nivel (Reiniciar desde la Pausa).</summary>
    public void IniciarNivel(NivelData datos, bool esReintento = false)
    {
        // Detener cualquier corrutina de oleadas viva (del nivel anterior o del
        // mismo nivel si esto es un reinicio a mitad de partida).
        StopAllCoroutines();

        // Pantalla negra de golpe: la limpieza del nivel anterior (laptops, mesas,
        // jugadores reposicionados) ocurre mientras no se ve nada.
        if (FundidoPantalla.Instance != null) FundidoPantalla.Instance.NegroInstantaneo();

        _nivelActual = datos;

        // Limpiar cualquier laptop/pieza y estado de mesas que haya
        // quedado del nivel anterior (el juego no recarga la escena entre niveles)
        LimpiarNivelAnterior();

        // NUEVO: arrancar el reloj y contadores del GameManager justo ahora
        if (GameManager.Instance != null)
        {
            GameManager.Instance.IniciarNivel(datos.nivel, esReintento);
            GameManager.Instance.incendiosActivos = datos.incendios;
        }

        // Lista escalada de cada oleada (se calcula una sola vez: el total esperado y lo que
        // sale de verdad tienen que coincidir para que funcione el fin anticipado)
        int jugadoresNivel = 1;
        if (LobbyManager.Instance != null)
            jugadoresNivel = Mathf.Max(1, LobbyManager.Instance.CantidadJugadores());
        int totalEsperado = ConstruirOleadasEscaladas(datos, jugadoresNivel);
        if (GameManager.Instance != null)
            GameManager.Instance.DefinirLaptopsEsperadas(totalEsperado);

        // Configurar servidores: primero desactivar todos, luego programar
        // solo los que indica el JSON de este nivel.
        Server[] servidoresEscena = FindObjectsByType<Server>();
        foreach (Server s in servidoresEscena)
            s.Desactivar();

        if (datos.servidores != null)
        {
            foreach (ServidorData sd in datos.servidores)
            {
                bool encontrado = false;
                foreach (Server s in servidoresEscena)
                {
                    if (s.servidorId == sd.id)
                    {
                        s.ProgramarIncendio(sd.segundo);
                        encontrado = true;
                        break;
                    }
                }
                if (!encontrado)
                    Debug.LogWarning($"WaveManager: No se encontró servidor con id {sd.id} en la escena.");
            }
        }

        // Puertas corredizas: la de servidores solo funciona si el nivel trae servidores; la de
        // almacenes, si pide piezas (laptops que no son "facil") o tiene incendios (extintor)
        bool hayServidores = datos.servidores != null && datos.servidores.Count > 0;
        bool hayPiezasOIncendios = datos.incendios;
        if (datos.oleadas != null)
            foreach (OleadaData o in datos.oleadas)
                if (o.laptops != null)
                    foreach (string tipo in o.laptops)
                        if (tipo != "facil") hayPiezasOIncendios = true;
        if (ModoSupervivencia.Activo) { hayServidores = true; hayPiezasOIncendios = true; }
        PuertaCorrediza.ConfigurarNivel(hayServidores, hayPiezasOIncendios);

        StartCoroutine(ModoSupervivencia.Activo ? EjecutarSupervivencia() : EjecutarOleadas());

        // De negro a la escena (tiempo real, funciona aunque el tutorial congele el juego).
        // Si hay historieta de inicio, el aclarado lo hace ella al cerrarse.
        if (!ComicManager.Activo && FundidoPantalla.Instance != null) FundidoPantalla.Instance.Aclarar();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Supervivencia
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Inicia (o reinicia) una partida de supervivencia: sin JSON, las oleadas se generan solas.</summary>
    public void IniciarSupervivencia(bool esReintento = false)
    {
        ModoSupervivencia.Activo = true;
        NivelData datos = new NivelData
        {
            nivel = 0, // sin número: sin historietas, tutorial, progreso ni envío al backend
            incendios = false,
            servidores = new List<ServidorData>(),
            oleadas = new List<OleadaData>()
        };
        IniciarNivel(datos, esReintento);
    }

    IEnumerator EjecutarSupervivencia()
    {
        int jugadores = 1;
        if (LobbyManager.Instance != null)
            jugadores = Mathf.Max(1, LobbyManager.Instance.CantidadJugadores());
        float mult = Multiplicador(jugadores);

        Debug.Log($"WaveManager: Supervivencia iniciada con {jugadores} jugador(es), multiplicador ×{mult}.");

        _proximaDoradaPosible = segundoPrimeraDorada;
        float proximaOleada = primeraOleadaSupervivencia;
        float proximoServidor = primerServidorSupervivencia;
        int incendiosServidor = 0;
        float arrastre = 0f;
        int oleadasHastaRespiro = Random.Range(oleadasEntreRespirosMin, oleadasEntreRespirosMax + 1);

        while (true)
        {
            // El reloj del GameManager (se detiene en pausa y al terminar)
            float t = GameManager.Instance != null ? GameManager.Instance.TiempoTranscurrido : 0f;
            float tDif = t * Mathf.Max(0.1f, velocidadDificultad);

            if (GameManager.Instance != null)
                GameManager.Instance.incendiosActivos = tDif >= segundoIncendiosLaptops;

            // Poder "Respiro": la siguiente oleada espera a que termine
            if (t >= proximaOleada && PoderesSupervivencia.Activo(Poder.Respiro))
                proximaOleada = t + PoderesSupervivencia.Restante(Poder.Respiro) + 2f;

            // ── Oleada ────────────────────────────────────────────────────────
            if (t >= proximaOleada)
            {
                // Crece de forma continua (1 → 1.6 → 2.2 …): la fracción se arrastra a la siguiente oleada
                float baseOleada = Mathf.Min(laptopsPorOleadaMaximo,
                    laptopsPorOleadaInicial + tDif / Mathf.Max(1f, segundosPorLaptopExtra));
                float exacto = baseOleada * mult + arrastre;
                int cantidad = Mathf.Max(1, Mathf.FloorToInt(exacto + 0.0001f));
                arrastre = Mathf.Max(0f, exacto - cantidad);

                List<string> lista = new List<string>();
                for (int i = 0; i < cantidad; i++) lista.Add(ElegirTipoSupervivencia(tDif));

                OleadaData oleada = new OleadaData { segundo = t, laptops = lista };
                StartCoroutine(EjecutarOleadaEscalada(oleada, jugadores));

                float intervalo = intervaloOleadaMinimo +
                    (intervaloOleadaInicial - intervaloOleadaMinimo) * Mathf.Exp(-tDif / Mathf.Max(1f, suavidadIntervalo));
                proximaOleada = t + intervalo;

                // Respiro: cada pocas oleadas, un rato sin laptops para poder recuperarse
                if (tDif >= segundoInicioRespiros && --oleadasHastaRespiro <= 0)
                {
                    proximaOleada += duracionRespiro;
                    oleadasHastaRespiro = Random.Range(oleadasEntreRespirosMin, oleadasEntreRespirosMax + 1);
                    Debug.Log($"WaveManager: respiro de {duracionRespiro}s (sig. oleada en el seg {proximaOleada:0}).");
                }
            }

            // ── Incendio de servidor ──────────────────────────────────────────
            if (t >= proximoServidor)
            {
                int cuantos = tDif >= segundoServidoresDobles ? 2 : 1;
                for (int i = 0; i < cuantos; i++)
                    if (IncendiarServidorLibre()) incendiosServidor++;

                proximoServidor = t + Mathf.Max(intervaloServidorMinimo,
                    intervaloServidorInicial - reduccionIntervaloServidor * incendiosServidor);
            }

            yield return null;
        }
    }

    // Probabilidad de cada tipo según el minuto (con la velocidad de dificultad aplicada).
    // Columnas: minuto, fácil, media, difícil, muy difícil. Entre filas se interpola.
    private static readonly float[,] _tablaTipos = {
        // 0-1.5 min: calentamiento | 1.5-5 min: ritmo bueno y parejo | 5+ min: se complica
        { 0.0f, 1.00f, 0.00f, 0.00f, 0.00f },
        { 1.0f, 1.00f, 0.00f, 0.00f, 0.00f },
        { 1.5f, 0.65f, 0.35f, 0.00f, 0.00f },
        { 3.0f, 0.40f, 0.45f, 0.15f, 0.00f },
        { 5.0f, 0.20f, 0.50f, 0.30f, 0.00f },
        { 7.0f, 0.05f, 0.50f, 0.35f, 0.10f },
        {10.0f, 0.00f, 0.35f, 0.45f, 0.20f },
        {15.0f, 0.00f, 0.25f, 0.45f, 0.30f },
    };
    private static readonly string[] _nombresTipos = { "facil", "media", "dificil", "muy_dificil" };

    string ElegirTipoSupervivencia(float segundos)
    {
        if (PoderesSupervivencia.Activo(Poder.LaptopsFaciles)) return "facil";

        float min = segundos / 60f;
        int filas = _tablaTipos.GetLength(0);
        int a = filas - 1, b = filas - 1;
        for (int i = 0; i < filas - 1; i++)
            if (min < _tablaTipos[i + 1, 0]) { a = i; b = i + 1; break; }

        float k = a == b ? 0f : Mathf.InverseLerp(_tablaTipos[a, 0], _tablaTipos[b, 0], min);
        float[] pesos = new float[4];
        float suma = 0f;
        for (int j = 0; j < 4; j++)
        {
            pesos[j] = Mathf.Lerp(_tablaTipos[a, j + 1], _tablaTipos[b, j + 1], k);
            suma += pesos[j];
        }

        float r = Random.value * suma;
        for (int j = 0; j < 4; j++)
        {
            if (r < pesos[j]) return _nombresTipos[j];
            r -= pesos[j];
        }
        return "facil";
    }

    /// <summary>¿Esta laptop sale dorada? Nunca dos a la vez y con un mínimo de tiempo entre ellas.</summary>
    bool TocaDorada()
    {
        float t = GameManager.Instance != null ? GameManager.Instance.TiempoTranscurrido : 0f;
        if (t < _proximaDoradaPosible || LaptopDorada.Activas.Count > 0) return false;
        if (Random.value >= probabilidadDorada) return false;
        _proximaDoradaPosible = t + intervaloMinimoDorada;
        return true;
    }

    /// <summary>Incendia un servidor al azar de los que están tranquilos. False si no hay ninguno libre.</summary>
    bool IncendiarServidorLibre()
    {
        List<Server> libres = new List<Server>();
        foreach (Server s in FindObjectsByType<Server>())
            if (s != null && s.EstaLibre) libres.Add(s);
        if (libres.Count == 0) return false;

        libres[Random.Range(0, libres.Count)].ProgramarIncendio(0.1f);
        return true;
    }

    IEnumerator EjecutarOleadas()
    {
        // Si hay un tutorial en pantalla, las oleadas esperan a que se cierre
        while (TutorialManager.Activo || ComicManager.Activo) yield return null;

        float tiempoInicio = Time.time;

        _nivelActual.oleadas.Sort((a, b) => a.segundo.CompareTo(b.segundo));

        // Cantidad de jugadores activos (mínimo 1 para no multiplicar por 0)
        int jugadores = 1;
        if (LobbyManager.Instance != null)
            jugadores = Mathf.Max(1, LobbyManager.Instance.CantidadJugadores());

        Debug.Log($"WaveManager: Nivel {_nivelActual.nivel} iniciado con {jugadores} jugador(es).");

        foreach (OleadaData oleada in _nivelActual.oleadas)
        {
            float tiempoEspera = oleada.segundo - (Time.time - tiempoInicio);
            if (tiempoEspera > 0f)
                yield return new WaitForSeconds(tiempoEspera);

            // Cada oleada corre sus ráfagas de forma independiente
            StartCoroutine(EjecutarOleadaEscalada(oleada, jugadores));
        }

        Debug.Log("WaveManager: Todas las oleadas fueron lanzadas.");
    }

    /// <summary>
    /// Multiplica la lista base por la cantidad de jugadores y la
    /// reparte en ráfagas de maxPorRafaga con intervaloRafagas entre ellas.
    /// </summary>
    IEnumerator EjecutarOleadaEscalada(OleadaData oleada, int jugadores)
    {
        // 1. Lista total ya escalada (la calculó ConstruirOleadasEscaladas al cargar el nivel)
        if (!_laptopsPorOleada.TryGetValue(oleada, out List<string> total))
            total = new List<string>(oleada.laptops);

        // Oleadas del tramo final despliegan sus ráfagas más rápido
        float intervalo = oleada.segundo >= segundoUmbralFinal
            ? intervaloRafagasFinal
            : intervaloRafagas;

        Debug.Log($"WaveManager: Oleada seg {oleada.segundo} — {oleada.laptops.Count} base, {jugadores} jugador(es) → {total.Count} laptops (intervalo {intervalo}s).");

        // 2. Repartir en ráfagas
        int indice = 0;
        while (indice < total.Count)
        {
            int enEstaRafaga = Mathf.Min(maxPorRafaga, total.Count - indice);

            for (int i = 0; i < enEstaRafaga; i++)
            {
                // Poder "Respiro": las ráfagas pendientes también esperan
                while (PoderesSupervivencia.Activo(Poder.Respiro)) yield return null;

                SpawnLaptop(total[indice]);
                indice++;

                if (i < enEstaRafaga - 1)
                    yield return new WaitForSeconds(delayEntreHorda);
            }

            if (indice < total.Count)
                yield return new WaitForSeconds(intervalo);
        }
    }

    /// <summary>
    /// Destruye laptops y piezas sueltas que hayan quedado del nivel anterior
    /// y resetea el estado de todas las mesas (el juego no recarga la escena
    /// entre niveles, así que hay que limpiar manualmente).
    /// </summary>
    void LimpiarNivelAnterior()
    {
        int laptopsDestruidas = 0;
        foreach (Laptop l in FindObjectsByType<Laptop>())
        {
            if (l != null) { Destroy(l.gameObject); laptopsDestruidas++; }
        }

        int piezasDestruidas = 0;
        foreach (HardwarePiece p in FindObjectsByType<HardwarePiece>())
        {
            if (p != null) { Destroy(p.gameObject); piezasDestruidas++; }
        }

        foreach (var t in FindObjectsByType<NormalTable>()) t.LimpiarParaNuevoNivel();
        foreach (var t in FindObjectsByType<CleaningTable>()) t.LimpiarParaNuevoNivel();
        foreach (var t in FindObjectsByType<RepairTable>()) t.LimpiarParaNuevoNivel();
        foreach (var t in FindObjectsByType<UpdateTable>()) t.LimpiarParaNuevoNivel();
        foreach (var t in FindObjectsByType<SlotTable>()) t.LimpiarParaNuevoNivel();

        // Devolver cada extintor a su estación con carga completa
        foreach (var e in FindObjectsByType<ExtinguisherStation>())
            e.ResetearExtintor();

        // Vaciar las manos de los jugadores por si sostenían algo justo cuando terminó el nivel anterior
        foreach (PlayerInteraction p in FindObjectsByType<PlayerInteraction>())
            p.VaciarManos();

        // Reiniciar a los personajes: stamina llena y de vuelta en su punto de inicio
        foreach (PlayerMovement m in FindObjectsByType<PlayerMovement>())
            m.ReiniciarEstado();

        PlayerSpawner spawner = FindAnyObjectByType<PlayerSpawner>();
        if (spawner != null) spawner.ReposicionarJugadores();

        Debug.Log($"WaveManager: Nivel anterior limpiado — {laptopsDestruidas} laptops y {piezasDestruidas} piezas destruidas.");
    }

    /// <summary>Detiene por completo las oleadas en curso (usado al salir al menú desde la Pausa).</summary>
    public void DetenerNivel()
    {
        StopAllCoroutines();
    }

    void SpawnLaptop(string tipo)
    {
        if (laptopPrefab == null || spawnPoint == null) return;

        // Supervivencia: de vez en cuando sale una laptop dorada (problemas de una media)
        bool dorada = ModoSupervivencia.Activo && TocaDorada();
        if (dorada) tipo = "media";

        GameObject obj = Instantiate(laptopPrefab, spawnPoint.position, laptopPrefab.transform.rotation);

        Laptop laptopScript = obj.GetComponent<Laptop>();
        if (laptopScript != null)
            AsignarProblemas(laptopScript, tipo);

        if (materialesColor != null && materialesColor.Length > 0)
        {
            MeshRenderer renderer = obj.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
                renderer.material = materialesColor[Random.Range(0, materialesColor.Length)];
        }

        if (dorada)
        {
            obj.AddComponent<LaptopDorada>().Configurar(colorDorado);
            Debug.Log("WaveManager: ✨ ¡Laptop dorada!");
        }

        // NOTA: NO se llama GameManager.RegistrarLaptopLlegada() aquí — eso
        // pasaba antes también en GestorMesas.AcomodarLaptop() cuando la laptop
        // llega físicamente al slot, causando un doble conteo. Se deja solo ahí,
        // que es el momento real de "llegada" (no cuando se instancia en la banda).

        Debug.Log($"WaveManager: Laptop '{tipo}' spawneada.");
    }

    /// <summary>Multiplicador de laptops para esta cantidad de jugadores.</summary>
    float Multiplicador(int jugadores)
    {
        if (multiplicadorPorJugadores == null || multiplicadorPorJugadores.Length == 0) return jugadores;
        int i = Mathf.Clamp(jugadores - 1, 0, multiplicadorPorJugadores.Length - 1);
        return Mathf.Max(1f, multiplicadorPorJugadores[i]);
    }

    /// <summary>
    /// Calcula la lista final de laptops de cada oleada: la base del JSON × el multiplicador.
    /// Las fracciones (p. ej. 3 laptops × 1.6 = 4.8) se arrastran a la siguiente oleada para que el
    /// total del nivel salga parejo; las laptops extra se eligen al azar de la misma oleada, así se
    /// conserva la proporción de tipos. Devuelve el total del nivel.
    /// </summary>
    int ConstruirOleadasEscaladas(NivelData datos, int jugadores)
    {
        _laptopsPorOleada.Clear();
        if (datos.oleadas == null) return 0;

        datos.oleadas.Sort((a, b) => a.segundo.CompareTo(b.segundo));
        float mult = Multiplicador(jugadores);
        float arrastre = 0f;
        int total = 0;

        foreach (OleadaData o in datos.oleadas)
        {
            List<string> lista = new List<string>();
            if (o.laptops != null && o.laptops.Count > 0)
            {
                float exacto = o.laptops.Count * mult + arrastre;
                int cantidad = Mathf.Max(o.laptops.Count, Mathf.FloorToInt(exacto + 0.0001f));
                arrastre = Mathf.Max(0f, exacto - cantidad);

                // Copias completas de la oleada…
                while (lista.Count + o.laptops.Count <= cantidad)
                    lista.AddRange(o.laptops);

                // …y las que falten, al azar de la misma oleada
                List<string> mezcla = new List<string>(o.laptops);
                for (int i = 0; i < mezcla.Count; i++)
                {
                    int j = Random.Range(i, mezcla.Count);
                    (mezcla[i], mezcla[j]) = (mezcla[j], mezcla[i]);
                }
                for (int i = 0; lista.Count < cantidad; i++)
                    lista.Add(mezcla[i % mezcla.Count]);
            }

            _laptopsPorOleada[o] = lista;
            total += lista.Count;
        }

        Debug.Log($"WaveManager: {jugadores} jugador(es), multiplicador ×{mult} → {total} laptops en el nivel.");
        return total;
    }

    void AsignarProblemas(Laptop laptop, string tipo)
    {
        int cantidad;
        Laptop.Problema[] pool;

        switch (tipo.ToLower())
        {
            case "facil": cantidad = 1; pool = _problemasFacil; break;
            case "media": cantidad = 2; pool = _problemasMedia; break;
            case "dificil": cantidad = 3; pool = _problemasMedia; break;
            case "muy_dificil": cantidad = 4; pool = _problemasMedia; break;
            default: cantidad = 1; pool = _problemasFacil; break;
        }

        List<Laptop.Problema> mezclado = new List<Laptop.Problema>(pool);
        for (int i = 0; i < mezclado.Count; i++)
        {
            int j = Random.Range(i, mezclado.Count);
            (mezclado[i], mezclado[j]) = (mezclado[j], mezclado[i]);
        }

        laptop.problemasActivos.Clear();
        for (int i = 0; i < Mathf.Min(cantidad, mezclado.Count); i++)
            laptop.problemasActivos.Add(mezclado[i]);
    }
}