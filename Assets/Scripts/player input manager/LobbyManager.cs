using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("Personajes disponibles")]
    public string[] nombresPersonajes = { "Carlos", "Memo", "Andrés", "Luis" };
    public GameObject[] prefabsPersonajes;

    [Header("Secciones del lobby (una por cuadrante)")]
    public LobbySection[] secciones;

    [Header("Configuración")]
    public float timerDesconexion = 10f;

    [Header("Puntos de spawn en el nivel")]
    public Transform[] _spawnPoints;

    // ── Estado interno ────────────────────────────────────────────────────────
    private Dictionary<int, PlayerData> _jugadores             = new Dictionary<int, PlayerData>();
    private int[]   _personajesSeleccionados                   = new int[4];
    private float[] _timersDesconexion                         = new float[4];
    private bool[]  _desconectando                             = new bool[4];

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        for (int i = 0; i < 4; i++)
        {
            _personajesSeleccionados[i] = -1;
            _timersDesconexion[i]       = 0f;
            _desconectando[i]           = false;
        }
        Debug.Log("LobbyManager: Listo para recibir jugadores.");
    }

    void Update()
    {
        for (int i = 0; i < 4; i++)
        {
            if (!_desconectando[i]) continue;
            _timersDesconexion[i] -= Time.deltaTime;
            if (_timersDesconexion[i] <= 0f)
                ExpulsarJugador(i + 1);
        }
    }

    // ── Registro de jugadores (llamado desde PlayerJoinHandler) ──────────────

    public void RegistrarJugador(PlayerInput playerInput)
    {
        int id = playerInput.playerIndex + 1;
        Debug.Log($"LobbyManager: Registrando jugador {id}");

        if (id > 4) return;

        string dispositivo        = playerInput.currentControlScheme;
        _jugadores[id]            = new PlayerData(id, dispositivo);
        _desconectando[id - 1]    = false;

        // Mover al spawn point
        if (_spawnPoints != null && id <= _spawnPoints.Length && _spawnPoints[id - 1] != null)
        {
            playerInput.transform.position = _spawnPoints[id - 1].position;
            playerInput.transform.rotation = _spawnPoints[id - 1].rotation;
            Debug.Log($"LobbyManager: Jugador {id} en {_spawnPoints[id - 1].position}");
        }
        else
        {
            Debug.LogWarning($"LobbyManager: Sin SpawnPoint para jugador {id}");
        }

        int primerDisponible = PrimerPersonajeDisponible();
        secciones[id - 1].Activar(id, primerDisponible);

        Debug.Log($"LobbyManager: Jugador {id} registrado con {dispositivo}");
    }

    // ── API para LobbySection ─────────────────────────────────────────────────

    public int    TotalPersonajes()      => nombresPersonajes.Length;
    public string NombrePersonaje(int i) => nombresPersonajes[i];

    public bool PersonajeBloqueado(int personajeIndex, int jugadorIdExcluido)
    {
        for (int i = 0; i < 4; i++)
        {
            if (i + 1 == jugadorIdExcluido) continue;
            if (_personajesSeleccionados[i] == personajeIndex) return true;
        }
        return false;
    }

    public void OnJugadorListo(int jugadorId, int personajeIndex)
    {
        _personajesSeleccionados[jugadorId - 1] = personajeIndex;
        Debug.Log($"LobbyManager: Jugador {jugadorId} listo con {nombresPersonajes[personajeIndex]}");
        VerificarTodosListos();
    }

    public void OnJugadorCancelóListo(int jugadorId)
    {
        _personajesSeleccionados[jugadorId - 1] = -1;
    }

    public void OnJugadorQuiereSalir(int jugadorId)
    {
        if (!_jugadores.ContainsKey(jugadorId)) return;
        _desconectando[jugadorId - 1]     = true;
        _timersDesconexion[jugadorId - 1] = timerDesconexion;
    }

    public void CancelarDesconexion(int jugadorId)
    {
        _desconectando[jugadorId - 1]     = false;
        _timersDesconexion[jugadorId - 1] = 0f;
    }

    void ExpulsarJugador(int jugadorId)
    {
        _desconectando[jugadorId - 1]           = false;
        _personajesSeleccionados[jugadorId - 1] = -1;
        _jugadores.Remove(jugadorId);
        secciones[jugadorId - 1].Desactivar();
    }

    void VerificarTodosListos()
    {
        if (_jugadores.Count == 0) return;
        foreach (var kvp in _jugadores)
            if (!secciones[kvp.Key - 1].EstaListo()) return;

        Debug.Log("LobbyManager: ¡Todos listos!");
        GameFlowManager.Instance.MostrarSeleccionNivel();
    }

    int PrimerPersonajeDisponible()
    {
        for (int i = 0; i < nombresPersonajes.Length; i++)
            if (!PersonajeBloqueado(i, -1)) return i;
        return 0;
    }

    /// <summary>
    /// Expulsa a todos los jugadores y resetea las secciones.
    /// Usado al salir al menú a mitad de partida para que el próximo
    /// "Jugar" arranque desde cero (unirse + elegir personaje).
    /// </summary>
    public void ReiniciarLobby()
    {
        // Destruir los personajes instanciados (el PlayerInputManager libera sus índices)
        PlayerJoinHandler[] jugadores = FindObjectsByType<PlayerJoinHandler>(FindObjectsSortMode.None);
        foreach (PlayerJoinHandler j in jugadores)
            Destroy(j.gameObject);

        _jugadores.Clear();

        for (int i = 0; i < 4; i++)
        {
            _personajesSeleccionados[i] = -1;
            _timersDesconexion[i]       = 0f;
            _desconectando[i]           = false;
            if (i < secciones.Length && secciones[i] != null)
                secciones[i].Desactivar();
        }

        Debug.Log("LobbyManager: Lobby reiniciado — todos los jugadores expulsados.");
    }

    public GameObject[] GetPrefabsSeleccionados()
    {
        List<GameObject> resultado = new List<GameObject>();
        foreach (var kvp in _jugadores)
        {
            int pIndex = _personajesSeleccionados[kvp.Key - 1];
            if (pIndex >= 0 && pIndex < prefabsPersonajes.Length)
                resultado.Add(prefabsPersonajes[pIndex]);
        }
        return resultado.ToArray();
    }

    public LobbySection GetSeccion(int jugadorId)
    {
        if (jugadorId < 1 || jugadorId > secciones.Length) return null;
        return secciones[jugadorId - 1];
    }

    /// <summary>Devuelve el prefab del personaje elegido por un jugador.</summary>
    public GameObject GetPrefabPersonaje(int jugadorId)
    {
        int index = _personajesSeleccionados[jugadorId - 1];
        if (index < 0 || index >= prefabsPersonajes.Length) return null;
        return prefabsPersonajes[index];
    }

    public int CantidadJugadores() => _jugadores.Count;
}