using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("Referencia al Jugador")]
    public PlayerInteraction jugador;

    [Header("UI - Nombre")]
    public TextMeshProUGUI textoNombre;
    public string nombreJugador = "Jugador 1";

    [Header("UI - Íconos de problemas")]
    public Transform iconosContainer;
    public float iconSize = 50f;
    [Tooltip("Cuántas columnas tiene dibujado tu panel de fondo (Fix IT usa 4, el máximo de problemas por laptop)")]
    public int columnasVisibles = 4;

    [Header("Identidad")]
    public int playerId = 1;

    [Header("UI - Retrato del personaje")]
    public Image fotoPersonajeImage;

    [Header("Sprites de problemas")]
    public Sprite icono_limpieza;
    public Sprite icono_reparacion;
    public Sprite icono_actualizacion;
    public Sprite icono_ram;
    public Sprite icono_hdd;
    public Sprite icono_procesador;

    [Header("UI - Stamina")]
    public Image barraStaminaRelleno;

    [Header("UI - Carga del Extintor")]
    public GameObject barraExtintorUI;
    public Image barraExtintorRelleno;

    // ── Estado interno ─────────────────────────────────────────────────────
    private GameObject     _ultimoItemEnMano = null;
    private PlayerMovement _playerMovement   = null;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (textoNombre != null)
            textoNombre.text = nombreJugador;

        if (jugador != null)
            _playerMovement = jugador.GetComponent<PlayerMovement>();

        LimpiarIconos();
        if (barraExtintorUI != null) barraExtintorUI.SetActive(false);

        // Ocultarse al inicio — se activa cuando un jugador se vincula
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Vincula este HUD a un jugador en tiempo de ejecución.
    /// Llamar desde PlayerJoinHandler al iniciar el juego.
    /// </summary>
    public void SetJugador(PlayerInteraction nuevoJugador, Sprite fotoPersonaje = null)
    {
        // Activarse al vincularse con un jugador
        gameObject.SetActive(true);

        jugador         = nuevoJugador;
        _playerMovement = nuevoJugador != null
            ? nuevoJugador.GetComponent<PlayerMovement>()
            : null;

        if (fotoPersonajeImage != null && fotoPersonaje != null)
            fotoPersonajeImage.sprite = fotoPersonaje;

        _ultimoItemEnMano = null;
        LimpiarIconos();
        Debug.Log($"PlayerHUD {playerId}: jugador vinculado.");
    }

    void Update()
    {
        if (jugador == null) return;

        if (jugador.heldItem != _ultimoItemEnMano)
        {
            _ultimoItemEnMano = jugador.heldItem;
            RefrescarVista();
        }

        ActualizarBarraStamina();
        ActualizarBarraExtintor();
    }

    // ── Lógica ────────────────────────────────────────────────────────────────

    void RefrescarVista()
    {
        Extinguisher ext = _ultimoItemEnMano != null
            ? _ultimoItemEnMano.GetComponent<Extinguisher>()
            : null;

        if (ext != null)
        {
            LimpiarIconos();
            if (barraExtintorUI != null) barraExtintorUI.SetActive(true);
            return;
        }

        if (barraExtintorUI != null) barraExtintorUI.SetActive(false);
        RefrescarIconos();
    }

    void ActualizarBarraStamina()
    {
        if (_playerMovement == null || barraStaminaRelleno == null) return;
        barraStaminaRelleno.fillAmount = _playerMovement.PorcentajeStamina();
    }

    void ActualizarBarraExtintor()
    {
        if (_ultimoItemEnMano == null) return;
        Extinguisher ext = _ultimoItemEnMano.GetComponent<Extinguisher>();
        if (ext == null) return;
        if (barraExtintorRelleno != null)
            barraExtintorRelleno.fillAmount = ext.PorcentajeCarga();
    }

    void RefrescarIconos()
    {
        LimpiarIconos();
        if (_ultimoItemEnMano == null) return;

        Laptop laptopScript = _ultimoItemEnMano.GetComponent<Laptop>();
        if (laptopScript == null) return;

        // Ancho de una columna del panel (para que cada ícono ocupe exactamente
        // su casillero, sin importar cuántos problemas haya activos ahora).
        float anchoColumna = iconSize;
        RectTransform containerRT = iconosContainer as RectTransform;
        if (containerRT != null && columnasVisibles > 0)
            anchoColumna = containerRT.rect.width / columnasVisibles;

        foreach (Laptop.Problema problema in laptopScript.problemasActivos)
        {
            Sprite sprite = ObtenerSprite(problema);
            if (sprite == null) continue;

            GameObject iconObj = new GameObject(problema.ToString(), typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(iconosContainer, false);

            RectTransform rt = iconObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(anchoColumna, iconSize);

            Image img = iconObj.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true; // el ícono no se estira: queda centrado y proporcionado dentro de su columna

            // Si el Horizontal Layout Group del contenedor controla el tamaño de
            // los hijos, esto asegura que respete el ancho de columna calculado.
            LayoutElement le = iconObj.AddComponent<LayoutElement>();
            le.preferredWidth  = anchoColumna;
            le.preferredHeight = iconSize;
        }
    }

    void LimpiarIconos()
    {
        if (iconosContainer == null) return;
        foreach (Transform hijo in iconosContainer)
            Destroy(hijo.gameObject);
    }

    Sprite ObtenerSprite(Laptop.Problema problema)
    {
        switch (problema)
        {
            case Laptop.Problema.Limpieza:           return icono_limpieza;
            case Laptop.Problema.Reparacion:         return icono_reparacion;
            case Laptop.Problema.Actualizacion:      return icono_actualizacion;
            case Laptop.Problema.NecesitaRAM:        return icono_ram;
            case Laptop.Problema.NecesitaHDD:        return icono_hdd;
            case Laptop.Problema.NecesitaProcesador: return icono_procesador;
            default:                                 return null;
        }
    }
}