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

    [Header("Materiales de color (uno por laptop)")]
    public Material[] materialesColor;

    // ── Estado interno ────────────────────────────────────────────────────────
    private NivelData _nivelActual;
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
        PuertaCorrediza.ConfigurarNivel(hayServidores, hayPiezasOIncendios);

        StartCoroutine(EjecutarOleadas());

        // De negro a la escena (tiempo real, funciona aunque el tutorial congele el juego).
        // Si hay historieta de inicio, el aclarado lo hace ella al cerrarse.
        if (!ComicManager.Activo && FundidoPantalla.Instance != null) FundidoPantalla.Instance.Aclarar();
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