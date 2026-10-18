using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("Personajes disponibles")]
    public string[] nombresPersonajes = { "Steve", "Mark", "Vill", "Alan" };
    public GameObject[] prefabsPersonajes;

    [Tooltip("MISMO ORDEN que Nombres Personajes / Prefabs Personajes — revisa y corrige aquí si no coincide")]
    public string[] apodosPersonajes = { "Panic Expert", "Network Guy", "The Analyst", "The Intern" };

    [Header("Secciones del lobby (una por cuadrante)")]
    public LobbySection[] secciones;

    [Header("Salir del lobby")]
    [Tooltip("Tiempo (seg) tras entrar al lobby durante el cual se ignora Esc / B para volver al menú")]
    public float retrasoSalirLobbyVacio = 0.3f;

    [Header("Puntos de spawn en el nivel")]
    public Transform[] _spawnPoints;

    // ── Estado interno ────────────────────────────────────────────────────────
    private Dictionary<int, PlayerData> _jugadores = new Dictionary<int, PlayerData>();
    private int[] _personajesSeleccionados = new int[4];
    private bool _lobbyEstabaVisible = false;
    private float _timerLobbyVacio = 0f;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        for (int i = 0; i < 4; i++)
            _personajesSeleccionados[i] = -1;

        Debug.Log("LobbyManager: Listo para recibir jugadores.");
    }

    void Update()
    {
        // Lobby vacío (nadie se ha unido): Esc / B de cualquier dispositivo regresa al menú
        bool lobbyVisible = GameFlowManager.Instance != null && GameFlowManager.Instance.EnLobby;

        if (lobbyVisible && !_lobbyEstabaVisible)
            _timerLobbyVacio = retrasoSalirLobbyVacio; // acaba de aparecer el lobby

        _lobbyEstabaVisible = lobbyVisible;
        if (!lobbyVisible) return;

        _timerLobbyVacio -= Time.unscaledDeltaTime;
        if (_timerLobbyVacio > 0f || _jugadores.Count > 0) return;

        bool salir = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (!salir)
        {
            foreach (Gamepad gp in Gamepad.all)
                if (gp.buttonEast.wasPressedThisFrame) { salir = true; break; }
        }

        if (salir)
            GameFlowManager.Instance.MostrarMenu();
    }

    // ── Registro de jugadores (llamado desde PlayerJoinHandler) ──────────────

    public void RegistrarJugador(PlayerInput playerInput)
    {
        int id = playerInput.playerIndex + 1;
        Debug.Log($"LobbyManager: Registrando jugador {id}");

        if (id > 4) return;

        string dispositivo = playerInput.currentControlScheme;
        _jugadores[id] = new PlayerData(id, dispositivo);

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

    public int TotalPersonajes() => nombresPersonajes.Length;
    public string NombrePersonaje(int i) => nombresPersonajes[i];
    public string ApodoPersonaje(int i) => (i >= 0 && i < apodosPersonajes.Length) ? apodosPersonajes[i] : "";

    /// <summary>Prefab directo por índice — usado para la vista previa en vivo mientras se elige.</summary>
    public GameObject GetPrefabPorIndice(int index)
    {
        if (index < 0 || index >= prefabsPersonajes.Length) return null;
        return prefabsPersonajes[index];
    }

    /// <summary>Foto estática del personaje (la misma que ya usa el HUD), para la vista previa del lobby.</summary>
    public Sprite GetFotoPersonaje(int index)
    {
        GameObject prefab = GetPrefabPorIndice(index);
        if (prefab == null) return null;

        CharacterReference charRef = prefab.GetComponent<CharacterReference>();
        return charRef != null ? charRef.fotoPersonaje : null;
    }

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
        RefrescarBloqueoTodas();
        VerificarTodosListos();
    }

    public void OnJugadorCancelóListo(int jugadorId)
    {
        _personajesSeleccionados[jugadorId - 1] = -1;
        RefrescarBloqueoTodas();
    }

    /// <summary>Actualiza el blanco y negro / overlay de todas las secciones activas.</summary>
    void RefrescarBloqueoTodas()
    {
        for (int i = 0; i < secciones.Length; i++)
            if (secciones[i] != null)
                secciones[i].RefrescarBloqueo();
    }

    /// <summary>
    /// El jugador pulsó Esc / B mientras elegía personaje: sale de la selección al instante.
    /// Su personaje se destruye (libera su lugar y su dispositivo) y su sección vuelve a
    /// "Presiona A para unirse". Los demás jugadores no se ven afectados.
    /// Si era el último jugador, se regresa al menú principal.
    /// </summary>
    public void SalirDeLaSeleccion(int jugadorId)
    {
        if (!_jugadores.ContainsKey(jugadorId)) return;

        _personajesSeleccionados[jugadorId - 1] = -1;
        _jugadores.Remove(jugadorId);
        secciones[jugadorId - 1].Desactivar();

        // Destruir el personaje de ese jugador (el PlayerInputManager libera su índice)
        PlayerJoinHandler[] jugadores = FindObjectsByType<PlayerJoinHandler>(FindObjectsSortMode.None);
        foreach (PlayerJoinHandler j in jugadores)
        {
            if (j.JugadorId == jugadorId)
            {
                Destroy(j.gameObject);
                break;
            }
        }

        RefrescarBloqueoTodas();

        // Último jugador en salir → menú principal
        if (_jugadores.Count == 0)
        {
            GameFlowManager.Instance.MostrarMenu();
            return;
        }

        // Si los que quedan ya estaban todos listos, la partida puede continuar
        VerificarTodosListos();
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
            if (i < secciones.Length && secciones[i] != null)
                secciones[i].Desactivar();
        }

        Debug.Log("LobbyManager: Lobby reiniciado — todos los jugadores expulsados.");
    }

    /// <summary>
    /// Vuelve de la selección de nivel a la selección de personaje (Esc / B).
    /// Todos los jugadores dejan de estar "listos" y conservan el personaje que tenían
    /// elegido en pantalla, para poder cambiarlo o volver a confirmar.
    /// </summary>
    public void VolverASeleccionDePersonaje()
    {
        for (int i = 0; i < secciones.Length; i++)
            if (secciones[i] != null && secciones[i].EstaListo())
                secciones[i].CancelarListo();

        GameFlowManager.Instance.MostrarLobby();
        RefrescarBloqueoTodas();
    }

    /// <summary>Llamado desde el nuevo botón "Volver al Menú" de la pantalla de selección.</summary>
    public void VolverAlMenu()
    {
        ReiniciarLobby();
        GameFlowManager.Instance.MostrarMenu();
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