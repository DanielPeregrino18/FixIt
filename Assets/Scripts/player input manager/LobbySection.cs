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
    public TextMeshProUGUI textoApodoPersonaje;
    public TextMeshProUGUI textoInstruccion;  // "← → para elegir / A para confirmar"

    [Header("Indicador de bloqueado")]
    public GameObject overlayBloqueado; // Panel semitransparente oscuro sobre el personaje

    [Header("Personaje ya elegido por otro jugador (blanco y negro)")]
    [Tooltip("Material con el shader UI/Grayscale")]
    public Material materialGrises;
    [Tooltip("Todos los Image (gafete, foto, etc.) que deben verse en blanco y negro cuando el personaje está bloqueado")]
    public Image[] imagenesAEscalaDeGrises;

    [Header("Vista previa del personaje (foto estática)")]
    public Image imagenPersonaje;

    [Header("Puntitos indicadores (4, en orden)")]
    public Image[] puntosIndicador;
    public Sprite spriteRelleno;
    public Sprite spriteVacio;

    // ── Estado interno ────────────────────────────────────────────────────────
    private int _jugadorId = -1;
    private bool _activo = false;
    private bool _listo = false;
    private int _personajeActual = 0;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        MostrarEsperando();
    }

    /// <summary>Inicializa la sección cuando un jugador se une.</summary>
    public void Activar(int jugadorId, int personajeInicial)
    {
        _jugadorId = jugadorId;
        _activo = true;
        _listo = false;
        _personajeActual = personajeInicial;

        MostrarEligiendo();
        ActualizarNombrePersonaje();
        ActualizarFotoPersonaje();
    }

    /// <summary>Desactiva la sección si el jugador se desconecta.</summary>
    public void Desactivar()
    {
        _jugadorId = -1;
        _activo = false;
        _listo = false;

        SetBloqueado(false); // restaurar colores normales
        MostrarEsperando();
    }

    /// <summary>Mueve la selección a la izquierda.</summary>
    public void MoverIzquierda()
    {
        if (!_activo || _listo) return;
        _personajeActual--;
        if (_personajeActual < 0) _personajeActual = LobbyManager.Instance.TotalPersonajes() - 1;
        AudioManager.CambioPersonaje();
        ActualizarNombrePersonaje();
        ActualizarFotoPersonaje();
    }

    /// <summary>Mueve la selección a la derecha.</summary>
    public void MoverDerecha()
    {
        if (!_activo || _listo) return;
        _personajeActual++;
        if (_personajeActual >= LobbyManager.Instance.TotalPersonajes())
            _personajeActual = 0;
        AudioManager.CambioPersonaje();
        ActualizarNombrePersonaje();
        ActualizarFotoPersonaje();
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
        AudioManager.Confirmar();
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

        // Blanco y negro: se cambia el material de cada Image (null = material normal de UI)
        if (imagenesAEscalaDeGrises != null)
        {
            Material mat = (bloqueado && materialGrises != null) ? materialGrises : null;
            for (int i = 0; i < imagenesAEscalaDeGrises.Length; i++)
                if (imagenesAEscalaDeGrises[i] != null)
                    imagenesAEscalaDeGrises[i].material = mat;
        }
    }

    /// <summary>
    /// Recalcula si el personaje que esta sección está viendo quedó bloqueado
    /// (o liberado) por lo que hizo otro jugador. Lo llama el LobbyManager.
    /// </summary>
    public void RefrescarBloqueo()
    {
        if (!_activo) return;
        SetBloqueado(LobbyManager.Instance.PersonajeBloqueado(_personajeActual, _jugadorId));
    }

    public int GetPersonajeActual() => _personajeActual;
    public bool EstaListo() => _listo;
    public bool EstaActivo() => _activo;

    // ── Helpers visuales ─────────────────────────────────────────────────────

    void MostrarEsperando()
    {
        if (panelEsperando != null) panelEsperando.SetActive(true);
        if (panelEligiendo != null) panelEligiendo.SetActive(false);
        if (panelListo != null) panelListo.SetActive(false);
    }

    void MostrarEligiendo()
    {
        if (panelEsperando != null) panelEsperando.SetActive(false);
        if (panelEligiendo != null) panelEligiendo.SetActive(true);
        if (panelListo != null) panelListo.SetActive(false);

        if (textoInstruccion != null)
            textoInstruccion.text = "← → Elegir  |  A Confirmar  |  B Salir";
    }

    void MostrarListo()
    {
        if (panelEsperando != null) panelEsperando.SetActive(false);
        // panelEligiendo se queda visible: el gafete se ve debajo del PNG de "Listo"
        if (panelEligiendo != null) panelEligiendo.SetActive(true);
        if (panelListo != null) panelListo.SetActive(true);

        if (textoInstruccion != null)
            textoInstruccion.text = "B Cancelar";
    }

    void ActualizarNombrePersonaje()
    {
        if (textoNombrePersonaje != null)
            textoNombrePersonaje.text = LobbyManager.Instance.NombrePersonaje(_personajeActual);

        if (textoApodoPersonaje != null)
            textoApodoPersonaje.text = LobbyManager.Instance.ApodoPersonaje(_personajeActual);

        // Actualizar overlay de bloqueado
        bool bloqueado = LobbyManager.Instance.PersonajeBloqueado(_personajeActual, _jugadorId);
        SetBloqueado(bloqueado);
    }

    /// <summary>Muestra la foto estática del personaje actual (la misma que usa el HUD).</summary>
    void ActualizarFotoPersonaje()
    {
        if (imagenPersonaje != null)
            imagenPersonaje.sprite = LobbyManager.Instance.GetFotoPersonaje(_personajeActual);

        ActualizarPuntos();
    }

    /// <summary>Rellena el puntito que corresponde al personaje actual, vacía los demás.</summary>
    void ActualizarPuntos()
    {
        if (puntosIndicador == null) return;

        for (int i = 0; i < puntosIndicador.Length; i++)
        {
            if (puntosIndicador[i] == null) continue;
            puntosIndicador[i].sprite = (i == _personajeActual) ? spriteRelleno : spriteVacio;
        }
    }
}