using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Va en el Prefab del jugador.
/// Registra al jugador en el LobbyManager y maneja su input en el lobby.
/// Desactiva los scripts de juego mientras estamos en el lobby.
/// </summary>
public class PlayerJoinHandler : MonoBehaviour
{
    private PlayerInput _playerInput;
    private int _jugadorId = -1;

    /// <summary>Número de jugador (1-4), o -1 si aún no se registra.</summary>
    public int JugadorId => _jugadorId;
    private LobbySection _miSeccion = null;

    private InputAction _navigateAction;
    private InputAction _confirmAction;
    private InputAction _cancelAction;

    private float _timerNavegacion = 0f;
    private float _delayNavegacion = 0.2f;
    private float _timerInicioInput = 0f;
    private bool _lobbyEstabaVisible = false;
    private float _timerReentradaLobby = 0f;
    private bool _inputHabilitado = false;

    // Espera tras unirse antes de aceptar input. Es corta a propósito: la pulsación con la que
    // el jugador se unió ya no cuenta porque Confirmar usa WasPressedThisFrame (solo pulsaciones
    // nuevas), así que esto es solo un margen de seguridad.
    private const float ESPERA_TRAS_UNIRSE = 0.12f;

    // Scripts de juego que deben estar desactivados en el lobby
    private PlayerMovement _playerMovement;
    private PlayerInteraction _playerInteraction;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        _playerInput = GetComponent<PlayerInput>();
        if (_playerInput == null) return;

        // Desactivar scripts de juego mientras estamos en el lobby
        _playerMovement = GetComponent<PlayerMovement>();
        _playerInteraction = GetComponent<PlayerInteraction>();
        ActivarModoJuego(false);

        // Obtener acciones del lobby
        var mapa = _playerInput.actions.FindActionMap("Player");
        _navigateAction = mapa.FindAction("Navigate");
        _confirmAction = mapa.FindAction("Confirm");
        _cancelAction = mapa.FindAction("Cancel");

        // Registrar en el LobbyManager
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.RegistrarJugador(_playerInput);
            _jugadorId = _playerInput.playerIndex + 1;
            _miSeccion = LobbyManager.Instance.GetSeccion(_jugadorId);
            _inputHabilitado = false;
            _timerInicioInput = ESPERA_TRAS_UNIRSE;

            // El jugador se une con el lobby ya visible: eso no es "reaparecer el lobby",
            // así que no debe sumarse la espera de reentrada (antes se encadenaban ~0.85 s)
            _lobbyEstabaVisible = GameFlowManager.Instance == null || GameFlowManager.Instance.EnLobby;
        }
        else
        {
            Debug.LogError("PlayerJoinHandler: No se encontró LobbyManager.");
        }
    }

    void Update()
    {
        if (_miSeccion == null || !_miSeccion.EstaActivo()) return;

        // Este script solo debe reaccionar mientras la pantalla de selección de personaje
        // está visible (no en selección de nivel ni durante la partida).
        bool lobbyVisible = GameFlowManager.Instance == null || GameFlowManager.Instance.EnLobby;
        if (!lobbyVisible)
        {
            _lobbyEstabaVisible = false;
            return;
        }

        // Al reaparecer el lobby (ej. al volver desde selección de nivel con Esc / B),
        // se ignora el input un instante para que esa misma pulsación no cuente aquí.
        if (!_lobbyEstabaVisible)
        {
            _lobbyEstabaVisible = true;
            _timerReentradaLobby = 0.3f;
        }
        if (_timerReentradaLobby > 0f)
        {
            _timerReentradaLobby -= Time.unscaledDeltaTime;
            return;
        }

        // Esperar antes de procesar input para evitar que el E del lobby
        // se procese en el mismo frame que el unirse
        if (!_inputHabilitado)
        {
            _timerInicioInput -= Time.unscaledDeltaTime;
            if (_timerInicioInput <= 0f)
            {
                _inputHabilitado = true;
            }
            return;
        }

        _timerNavegacion -= Time.unscaledDeltaTime;

        // Navegación ← →
        if (_timerNavegacion <= 0f)
        {
            Vector2 nav = _navigateAction != null
                ? _navigateAction.ReadValue<Vector2>()
                : Vector2.zero;

            if (nav.x < -0.5f)
            {
                _miSeccion.MoverIzquierda();
                _timerNavegacion = _delayNavegacion;
            }
            else if (nav.x > 0.5f)
            {
                _miSeccion.MoverDerecha();
                _timerNavegacion = _delayNavegacion;
            }
        }

        // Confirmar — A / E
        // WasPressedThisFrame solo es true en el frame en que se presiona el botón: mantenerlo
        // presionado no repite (el Input System no hace auto-repetición de teclas), y la pulsación
        // con la que el jugador se unió ocurrió antes de que existiera, así que no cuenta.
        if (_confirmAction != null && _confirmAction.WasPressedThisFrame())
        {
            // Si ya está listo, confirmar no hace nada (se cancela con Esc / B)
            if (!_miSeccion.EstaListo())
                _miSeccion.Confirmar();
        }

        // Cancelar — B / Escape
        if (_cancelAction != null && _cancelAction.WasPressedThisFrame())
        {
            if (_miSeccion.EstaListo())
                _miSeccion.CancelarListo();
            else
                LobbyManager.Instance.SalirDeLaSeleccion(_jugadorId);
        }
    }

    /// <summary>
    /// Activa o desactiva los scripts de juego.
    /// Llamar con true cuando inicie el nivel, false en el lobby.
    /// </summary>
    public void ActivarModoJuego(bool activo, PlayerHUD[] huds = null)
    {
        if (_playerMovement != null) _playerMovement.enabled = activo;
        if (_playerInteraction != null) _playerInteraction.enabled = activo;

        if (activo && _jugadorId > 0)
        {
            // Cambiar modelo visual por el personaje elegido, y recibir su foto
            Sprite fotoPersonaje = null;
            CharacterSwapper swapper = GetComponent<CharacterSwapper>();
            if (swapper != null && LobbyManager.Instance != null)
            {
                GameObject prefabElegido = LobbyManager.Instance.GetPrefabPersonaje(_jugadorId);
                if (prefabElegido != null)
                    fotoPersonaje = swapper.SwapModel(prefabElegido, _jugadorId);
            }

            // Vincular al HUD usando el array recibido (que incluye objetos desactivados)
            if (huds != null)
            {
                foreach (PlayerHUD hud in huds)
                {
                    if (hud != null && hud.playerId == _jugadorId)
                    {
                        hud.SetJugador(_playerInteraction, fotoPersonaje);
                        Debug.Log($"PlayerJoinHandler: HUD jugador {_jugadorId} vinculado.");
                        break;
                    }
                }
            }
        }
    }
}