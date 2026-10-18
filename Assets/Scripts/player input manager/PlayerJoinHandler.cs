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
    private bool _confirmEstabaPresionado = true;
    private bool _confirmListoParaUsar = false;
    private float _tiempoSinPresionar = 0f;
    private const float TIEMPO_REQUERIDO_SUELTA = 0.25f; // segundos reales — no depende del framerate del Editor

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
            _timerInicioInput = 0.3f;
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
            _timerInicioInput -= Time.deltaTime;
            if (_timerInicioInput <= 0f)
            {
                _inputHabilitado = true;
            }
            return;
        }

        _timerNavegacion -= Time.deltaTime;

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
        // Detección de flanco manual, pero exigiendo que el botón se vea
        // genuinamente suelto por varios frames seguidos antes de empezar a
        // aceptar pulsaciones — un solo frame "suelto" puede ser solo el
        // hueco entre 2 pulsos de auto-repetición del teclado, no una
        // soltada real.
        bool confirmPresionadoAhora = _confirmAction != null && _confirmAction.IsPressed();

        if (!_confirmListoParaUsar)
        {
            if (confirmPresionadoAhora)
                _tiempoSinPresionar = 0f;
            else
                _tiempoSinPresionar += Time.deltaTime;

            if (_tiempoSinPresionar >= TIEMPO_REQUERIDO_SUELTA)
            {
                _confirmListoParaUsar = true;
                _confirmEstabaPresionado = false;
            }
        }
        else
        {
            if (confirmPresionadoAhora && !_confirmEstabaPresionado)
            {
                // Si ya está listo, confirmar no hace nada (se cancela con Esc / B)
                if (!_miSeccion.EstaListo())
                    _miSeccion.Confirmar();
            }
            _confirmEstabaPresionado = confirmPresionadoAhora;
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