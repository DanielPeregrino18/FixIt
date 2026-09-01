using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ejecuta las oleadas del nivel actual según el JSON cargado.
/// Escala la cantidad de laptops según el número de jugadores:
/// total = base × jugadores, repartido en ráfagas de máximo N laptops
/// con un intervalo entre ráfagas.
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Spawn")]
    public Transform spawnPoint;
    public GameObject laptopPrefab;

    [Header("Escalado por jugadores")]
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
    private bool      _corriendo = false;

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
    public void IniciarNivel(NivelData datos)
    {
        // Detener cualquier corrutina de oleadas viva (del nivel anterior o del
        // mismo nivel si esto es un reinicio a mitad de partida).
        StopAllCoroutines();

        _nivelActual = datos;
        _corriendo   = true;

        // Limpiar cualquier laptop/pieza y estado de mesas que haya
        // quedado del nivel anterior (el juego no recarga la escena entre niveles)
        LimpiarNivelAnterior();

        // NUEVO: arrancar el reloj y contadores del GameManager justo ahora
        if (GameManager.Instance != null)
        {
            GameManager.Instance.IniciarNivel(datos.nivel);
            GameManager.Instance.incendiosActivos = datos.incendios;
        }

        // Configurar servidores: primero desactivar todos, luego programar
        // solo los que indica el JSON de este nivel.
        Server[] servidoresEscena = FindObjectsByType<Server>(FindObjectsSortMode.None);
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

        StartCoroutine(EjecutarOleadas());
    }

    IEnumerator EjecutarOleadas()
    {
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

        _corriendo = false;
        Debug.Log("WaveManager: Todas las oleadas fueron lanzadas.");
    }

    /// <summary>
    /// Multiplica la lista base por la cantidad de jugadores y la
    /// reparte en ráfagas de maxPorRafaga con intervaloRafagas entre ellas.
    /// </summary>
    IEnumerator EjecutarOleadaEscalada(OleadaData oleada, int jugadores)
    {
        // 1. Construir lista total: la base repetida una vez por jugador,
        //    conservando la proporción de tipos.
        List<string> total = new List<string>();
        for (int j = 0; j < jugadores; j++)
            total.AddRange(oleada.laptops);

        // Oleadas del tramo final despliegan sus ráfagas más rápido
        float intervalo = oleada.segundo >= segundoUmbralFinal
            ? intervaloRafagasFinal
            : intervaloRafagas;

        Debug.Log($"WaveManager: Oleada seg {oleada.segundo} — {oleada.laptops.Count} base × {jugadores} jugadores = {total.Count} laptops (intervalo {intervalo}s).");

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
        foreach (Laptop l in FindObjectsByType<Laptop>(FindObjectsSortMode.None))
        {
            if (l != null) { Destroy(l.gameObject); laptopsDestruidas++; }
        }

        int piezasDestruidas = 0;
        foreach (HardwarePiece p in FindObjectsByType<HardwarePiece>(FindObjectsSortMode.None))
        {
            if (p != null) { Destroy(p.gameObject); piezasDestruidas++; }
        }

        foreach (var t in FindObjectsByType<NormalTable>(FindObjectsSortMode.None))   t.LimpiarParaNuevoNivel();
        foreach (var t in FindObjectsByType<CleaningTable>(FindObjectsSortMode.None)) t.LimpiarParaNuevoNivel();
        foreach (var t in FindObjectsByType<RepairTable>(FindObjectsSortMode.None))   t.LimpiarParaNuevoNivel();
        foreach (var t in FindObjectsByType<UpdateTable>(FindObjectsSortMode.None))   t.LimpiarParaNuevoNivel();
        foreach (var t in FindObjectsByType<SlotTable>(FindObjectsSortMode.None))     t.LimpiarParaNuevoNivel();

        // Devolver cada extintor a su estación con carga completa
        foreach (var e in FindObjectsByType<ExtinguisherStation>(FindObjectsSortMode.None))
            e.ResetearExtintor();

        // Vaciar las manos de los jugadores por si sostenían algo justo cuando terminó el nivel anterior
        foreach (PlayerInteraction p in FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None))
        {
            p.isHoldingItem = false;
            p.heldItem      = null;
            if (p.anim != null) p.anim.SetBool("isHolding", false);
            p.AjustarCollider(false);
        }

        Debug.Log($"WaveManager: Nivel anterior limpiado — {laptopsDestruidas} laptops y {piezasDestruidas} piezas destruidas.");
    }

    /// <summary>Detiene por completo las oleadas en curso (usado al salir al menú desde la Pausa).</summary>
    public void DetenerNivel()
    {
        StopAllCoroutines();
        _corriendo = false;
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

    void AsignarProblemas(Laptop laptop, string tipo)
    {
        int cantidad;
        Laptop.Problema[] pool;

        switch (tipo.ToLower())
        {
            case "facil":       cantidad = 1; pool = _problemasFacil; break;
            case "media":       cantidad = 2; pool = _problemasMedia; break;
            case "dificil":     cantidad = 3; pool = _problemasMedia; break;
            case "muy_dificil": cantidad = 4; pool = _problemasMedia; break;
            default:            cantidad = 1; pool = _problemasFacil; break;
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