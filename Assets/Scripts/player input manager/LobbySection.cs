using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controla una sección del lobby (un cuadrante de la pantalla).
/// Muestra el estado de un jugador: esperando, eligiendo personaje o listo.
/// </summary>
public class LobbySection : MonoBehaviour
{
    [Header("Paneles de estado")]
    public GameObject panelEsperando;   // "Presiona A para unirse"
    public GameObject panelEligiendo;   // Selección de personaje
    public GameObject panelListo;       // "¡Listo!"

    [Header("UI de selección")]
    public TextMeshProUGUI textoNombrePersonaje;
    public TextMeshProUGUI textoInstruccion;  // "← → para elegir / A para confirmar"

    [Header("Indicador de bloqueado")]
    public GameObject overlayBloqueado; // Panel semitransparente oscuro sobre el personaje

    // ── Estado interno ────────────────────────────────────────────────────────
    private int  _jugadorId       = -1;
    private bool _activo          = false;
    private bool _listo           = false;
    private int  _personajeActual = 0;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        MostrarEsperando();
    }

    /// <summary>Inicializa la sección cuando un jugador se une.</summary>
    public void Activar(int jugadorId, int personajeInicial)
    {
        _jugadorId        = jugadorId;
        _activo           = true;
        _listo            = false;
        _personajeActual  = personajeInicial;

        MostrarEligiendo();
        ActualizarNombrePersonaje();
    }

    /// <summary>Desactiva la sección si el jugador se desconecta.</summary>
    public void Desactivar()
    {
        _jugadorId = -1;
        _activo    = false;
        _listo     = false;

        MostrarEsperando();
    }

    /// <summary>Mueve la selección a la izquierda.</summary>
    public void MoverIzquierda()
    {
        if (!_activo || _listo) return;
        _personajeActual--;
        if (_personajeActual < 0) _personajeActual = LobbyManager.Instance.TotalPersonajes() - 1;
        ActualizarNombrePersonaje();
    }

    /// <summary>Mueve la selección a la derecha.</summary>
    public void MoverDerecha()
    {
        if (!_activo || _listo) return;
        _personajeActual++;
        if (_personajeActual >= LobbyManager.Instance.TotalPersonajes())
            _personajeActual = 0;
        ActualizarNombrePersonaje();
    }

    /// <summary>Confirma la selección o entra en estado Listo.</summary>
    public void Confirmar()
    {
        if (!_activo || _listo) return;

        // Verificar que el personaje no esté bloqueado por otro jugador
        if (LobbyManager.Instance.PersonajeBloqueado(_personajeActual, _jugadorId))
        {
            Debug.Log($"LobbySection {_jugadorId}: personaje bloqueado, elige otro.");
            return;
        }

        _listo = true;
        LobbyManager.Instance.OnJugadorListo(_jugadorId, _personajeActual);
        MostrarListo();
    }

    /// <summary>Cancela el estado Listo y regresa a selección.</summary>
    public void CancelarListo()
    {
        if (!_activo || !_listo) return;
        _listo = false;
        LobbyManager.Instance.OnJugadorCancelóListo(_jugadorId);
        MostrarEligiendo();
    }

    /// <summary>Muestra u oculta el overlay de bloqueado sobre un personaje.</summary>
    public void SetBloqueado(bool bloqueado)
    {
        if (overlayBloqueado != null)
            overlayBloqueado.SetActive(bloqueado);
    }

    public int  GetPersonajeActual() => _personajeActual;
    public bool EstaListo()          => _listo;
    public bool EstaActivo()         => _activo;

    // ── Helpers visuales ─────────────────────────────────────────────────────

    void MostrarEsperando()
    {
        if (panelEsperando  != null) panelEsperando.SetActive(true);
        if (panelEligiendo  != null) panelEligiendo.SetActive(false);
        if (panelListo      != null) panelListo.SetActive(false);
    }

    void MostrarEligiendo()
    {
        if (panelEsperando  != null) panelEsperando.SetActive(false);
        if (panelEligiendo  != null) panelEligiendo.SetActive(true);
        if (panelListo      != null) panelListo.SetActive(false);

        if (textoInstruccion != null)
            textoInstruccion.text = "← → Elegir  |  A Confirmar  |  B Salir";
    }

    void MostrarListo()
    {
        if (panelEsperando  != null) panelEsperando.SetActive(false);
        if (panelEligiendo  != null) panelEligiendo.SetActive(false);
        if (panelListo      != null) panelListo.SetActive(true);
    }

    void ActualizarNombrePersonaje()
    {
        if (textoNombrePersonaje != null)
            textoNombrePersonaje.text = LobbyManager.Instance.NombrePersonaje(_personajeActual);

        // Actualizar overlay de bloqueado
        bool bloqueado = LobbyManager.Instance.PersonajeBloqueado(_personajeActual, _jugadorId);
        SetBloqueado(bloqueado);
    }
}
