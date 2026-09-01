using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Va en el Prefab del jugador.
/// Registra al jugador en el LobbyManager y maneja su input en el lobby.
/// Desactiva los scripts de juego mientras estamos en el lobby.
/// </summary>
public class PlayerJoinHandler : MonoBehaviour
{
    private PlayerInput     _playerInput;
    private int             _jugadorId   = -1;
    private LobbySection    _miSeccion   = null;

    private InputAction _navigateAction;
    private InputAction _confirmAction;
    private InputAction _cancelAction;

    private float _timerNavegacion  = 0f;
    private float _delayNavegacion  = 0.2f;
    private float _timerInicioInput = 0f;
    private bool  _inputHabilitado  = false;

    // Scripts de juego que deben estar desactivados en el lobby
    private PlayerMovement    _playerMovement;
    private PlayerInteraction _playerInteraction;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        _playerInput = GetComponent<PlayerInput>();
        if (_playerInput == null) return;

        // Desactivar scripts de juego mientras estamos en el lobby
        _playerMovement    = GetComponent<PlayerMovement>();
        _playerInteraction = GetComponent<PlayerInteraction>();
        ActivarModoJuego(false);

        // Obtener acciones del lobby
        var mapa        = _playerInput.actions.FindActionMap("Player");
        _navigateAction = mapa.FindAction("Navigate");
        _confirmAction  = mapa.FindAction("Confirm");
        _cancelAction   = mapa.FindAction("Cancel");

        // Registrar en el LobbyManager
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.RegistrarJugador(_playerInput);
            _jugadorId        = _playerInput.playerIndex + 1;
            _miSeccion        = LobbyManager.Instance.GetSeccion(_jugadorId);
            _inputHabilitado  = false;
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

        // Esperar antes de procesar input para evitar que el E del lobby
        // se procese en el mismo frame que el unirse
        if (!_inputHabilitado)
        {
            _timerInicioInput -= Time.deltaTime;
            if (_timerInicioInput <= 0f) _inputHabilitado = true;
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
        if (_confirmAction != null && _confirmAction.WasPressedThisFrame())
        {
            if (_miSeccion.EstaListo())
                LobbyManager.Instance.CancelarDesconexion(_jugadorId);
            else
                _miSeccion.Confirmar();
        }

        // Cancelar — B / Escape
        if (_cancelAction != null && _cancelAction.WasPressedThisFrame())
        {
            if (_miSeccion.EstaListo())
                _miSeccion.CancelarListo();
            else
                LobbyManager.Instance.OnJugadorQuiereSalir(_jugadorId);
        }
    }

    /// <summary>
    /// Activa o desactiva los scripts de juego.
    /// Llamar con true cuando inicie el nivel, false en el lobby.
    /// </summary>
    public void ActivarModoJuego(bool activo, PlayerHUD[] huds = null)
    {
        if (_playerMovement    != null) _playerMovement.enabled    = activo;
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