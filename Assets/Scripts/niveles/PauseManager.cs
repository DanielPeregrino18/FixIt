using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Pantalla de pausa. Cualquier jugador puede pausar con P (teclado) o
/// Start (control). Solo funciona mientras hay un nivel en curso
/// (GameManager.EstaJugando), no en menú/lobby/selección/resultados.
/// Navegación: ↑ ↓ mueven el foco entre los 3 botones, E / botón Sur confirma.
/// </summary>
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    [Header("Canvas de Pausa")]
    public GameObject canvasPausa;

    [Header("Botones (en el orden en que quieres navegarlos)")]
    public GameObject botonReanudar;
    public GameObject botonReiniciar;
    [Tooltip("Opcional. Si se deja vacío, se crea solo al iniciar copiando el botón Reiniciar. " +
             "No hace falta conectarle el OnClick: el script lo hace.")]
    public GameObject botonConfiguracion;
    public GameObject botonSalir;

    [Header("Consejos (se muestra uno distinto cada vez que pausas)")]
    [Tooltip("Texto (TextMeshPro) dentro del Canvas de Pausa donde aparece el consejo")]
    public TMP_Text textoConsejo;
    [Tooltip("Cómo se muestra. {0} es el consejo. Ejemplo: Pro tip: {0}")]
    public string formatoConsejo = "{0}";
    [Tooltip("Escribe aquí los consejos. Salen en orden aleatorio y no se repiten hasta haber salido todos.")]
    [TextArea(1, 3)]
    public string[] consejos;

    [Header("Panel de poderes (solo en Supervivencia)")]
    [Tooltip("Fondo del panel lateral. Si se deja vacío se copia el fondo del consejo (FondoConsejo)")]
    public Image fondoPanelPoderes;
    public Vector2 tamanoPanelPoderes = new Vector2(600f, 660f);
    [Tooltip("Separación desde el borde derecho de la pantalla")]
    public float margenPanelPoderes = 40f;
    public float tamanoTextoPoderes = 24f;
    public Color colorNombrePoder = new Color(1f, 0.85f, 0.05f, 1f);

    private GameObject _panelPoderes;
    private TMP_Text _textoPoderes;

    private readonly List<int> _mazoConsejos = new List<int>();
    private int _ultimoConsejo = -1;

    private bool _pausado = false;
    private List<GameObject> _botones = new List<GameObject>();
    private int _indice = 0;
    private float _timerNav = 0f;

    private bool _enConfiguracion = false;
    private float _timerConfirmar = 0f; // evita que la tecla que cerró la configuración confirme otro botón aquí

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (botonConfiguracion == null) botonConfiguracion = CrearBotonConfiguracion();
        if (botonConfiguracion != null)
        {
            Button b = botonConfiguracion.GetComponent<Button>();
            if (b != null) b.onClick.AddListener(AbrirConfiguracion);
        }

        if (botonReanudar != null) _botones.Add(botonReanudar);
        if (botonReiniciar != null) _botones.Add(botonReiniciar);
        if (botonConfiguracion != null) _botones.Add(botonConfiguracion);
        if (botonSalir != null) _botones.Add(botonSalir);
    }

    /// <summary>
    /// Crea el botón "Configuración" copiando el de Reiniciar y lo pone justo debajo.
    /// El panel crece lo que mide el botón para que quepan todos.
    /// </summary>
    GameObject CrearBotonConfiguracion()
    {
        if (botonReiniciar == null) return null;

        Transform padre = botonReiniciar.transform.parent;
        GameObject copia = Instantiate(botonReiniciar, padre);
        copia.name = "BotonConfiguracion";
        copia.transform.SetSiblingIndex(botonReiniciar.transform.GetSiblingIndex() + 1);
        copia.transform.localScale = Vector3.one;

        // La copia trae el OnClick de Reiniciar configurado en el Inspector: se reemplaza entero
        Button b = copia.GetComponent<Button>();
        if (b != null) b.onClick = new Button.ButtonClickedEvent();

        TMP_Text texto = copia.GetComponentInChildren<TMP_Text>(true);
        TMP_Text original = botonReiniciar.GetComponentInChildren<TMP_Text>(true);
        if (texto != null)
        {
            // Conserva los espacios iniciales que usan los otros botones para alinear el texto
            string prefijo = "";
            if (original != null)
            {
                string t = original.text;
                int n = 0;
                while (n < t.Length && t[n] == ' ') n++;
                prefijo = t.Substring(0, n);
            }
            texto.text = prefijo + "Configuración";
        }

        RectTransform panel = padre as RectTransform;
        RectTransform rb = botonReiniciar.transform as RectTransform;
        if (panel != null && rb != null && panel.GetComponent<LayoutGroup>() != null)
            panel.sizeDelta += new Vector2(0f, rb.rect.height);

        return copia;
    }

    /// <summary>Botón "Configuración" de la pausa: sonido, pantalla y controles (sin cuenta).</summary>
    public void AbrirConfiguracion()
    {
        if (_enConfiguracion) return;
        _enConfiguracion = true;

        Button plantilla = botonReiniciar != null ? botonReiniciar.GetComponent<Button>() : null;
        ConfiguracionMenu.AbrirDesdePausa(plantilla, () =>
        {
            _enConfiguracion = false;
            _timerConfirmar = 0.25f;
        });
    }

    void CerrarConfiguracion()
    {
        if (_enConfiguracion) ConfiguracionMenu.CerrarPausa();
        _enConfiguracion = false;
    }

    void Start()
    {
        if (canvasPausa != null) canvasPausa.SetActive(false);
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.EstaJugando) return;

        // Con un tutorial en pantalla no se puede pausar (reanudar rompería el timeScale)
        if (TutorialManager.Activo || ComicManager.Activo) return;

        // Con la configuración abierta, ella maneja los controles (P / Start siguen reanudando)
        if (_pausado && !_enConfiguracion) ActualizarNavegacion();

        bool presionado = false;

        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            presionado = true;

        foreach (Gamepad gp in Gamepad.all)
        {
            if (gp.startButton.wasPressedThisFrame)
            {
                presionado = true;
                break;
            }
        }

        if (presionado)
            TogglePausa();
    }

    public void TogglePausa()
    {
        if (_pausado) Reanudar();
        else Pausar();
    }

    public void Pausar()
    {
        _pausado = true;
        Time.timeScale = 0f;
        if (AudioManager.Instance != null) AudioManager.Instance.PausarMusica();
        if (canvasPausa != null) canvasPausa.SetActive(true);

        MostrarConsejo();
        MostrarPanelPoderes();

        _indice = 0;
        ActualizarSeleccion();
    }

    /// <summary>
    /// Muestra un consejo al azar. Funciona como una baraja: salen todos, en orden aleatorio,
    /// antes de que se repita alguno, y nunca el mismo dos veces seguidas.
    /// </summary>
    void MostrarConsejo()
    {
        if (textoConsejo == null) return;

        if (consejos == null || consejos.Length == 0)
        {
            textoConsejo.gameObject.SetActive(false);
            return;
        }

        if (_mazoConsejos.Count == 0)
        {
            for (int i = 0; i < consejos.Length; i++) _mazoConsejos.Add(i);

            // Barajar (Fisher-Yates)
            for (int i = _mazoConsejos.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int aux = _mazoConsejos[i]; _mazoConsejos[i] = _mazoConsejos[j]; _mazoConsejos[j] = aux;
            }

            // Que la primera de la nueva baraja no sea la última que se vio
            if (_mazoConsejos.Count > 1 && _mazoConsejos[_mazoConsejos.Count - 1] == _ultimoConsejo)
            {
                int aux = _mazoConsejos[0];
                _mazoConsejos[0] = _mazoConsejos[_mazoConsejos.Count - 1];
                _mazoConsejos[_mazoConsejos.Count - 1] = aux;
            }
        }

        int indice = _mazoConsejos[_mazoConsejos.Count - 1];
        _mazoConsejos.RemoveAt(_mazoConsejos.Count - 1);
        _ultimoConsejo = indice;

        textoConsejo.gameObject.SetActive(true);
        textoConsejo.text = string.Format(formatoConsejo, consejos[indice]);
    }

    // ── Panel de poderes (Supervivencia) ──────────────────────────────────────

    void MostrarPanelPoderes()
    {
        if (!ModoSupervivencia.Activo)
        {
            if (_panelPoderes != null) _panelPoderes.SetActive(false);
            return;
        }

        CrearPanelPoderes();
        if (_panelPoderes == null) return;
        _textoPoderes.text = TextoPoderes();
        _panelPoderes.SetActive(true);
        AcomodarPanelPoderes();
    }

    /// <summary>
    /// Coloca el panel en el hueco libre: a la derecha de lo más ancho del menú de pausa (botones)
    /// y arriba del fondo del consejo, para que nunca se encime con ellos.
    /// </summary>
    void AcomodarPanelPoderes()
    {
        RectTransform canvasRT = canvasPausa.transform as RectTransform;
        RectTransform rp = (RectTransform)_panelPoderes.transform;
        if (canvasRT == null) return;
        Canvas.ForceUpdateCanvases();

        Rect r = canvasRT.rect;
        float izquierda = r.xMin + r.width * 0.5f + 200f; // por si no se encuentra el menú
        float abajo = r.yMin + margenPanelPoderes;
        Vector3[] esquinas = new Vector3[4];

        foreach (Transform hijo in canvasPausa.transform)
        {
            if (hijo == _panelPoderes.transform || !hijo.gameObject.activeInHierarchy) continue;
            bool esConsejo = hijo.name == "FondoConsejo" || (textoConsejo != null && hijo == textoConsejo.transform);

            foreach (RectTransform rt in hijo.GetComponentsInChildren<RectTransform>(false))
            {
                if (rt.GetComponent<Graphic>() == null) continue; // solo lo que se ve
                rt.GetWorldCorners(esquinas);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 p = canvasRT.InverseTransformPoint(esquinas[i]);
                    if (esConsejo) abajo = Mathf.Max(abajo, p.y + margenPanelPoderes * 0.5f);
                    else izquierda = Mathf.Max(izquierda, p.x + margenPanelPoderes);
                }
            }
        }

        float derecha = r.xMax - margenPanelPoderes;
        float arriba = r.yMax - margenPanelPoderes;
        float ancho = Mathf.Min(tamanoPanelPoderes.x, derecha - izquierda);
        float alto = Mathf.Min(tamanoPanelPoderes.y, arriba - abajo);
        if (ancho < 260f) { ancho = 260f; } // pantalla muy angosta: mejor encimado que ilegible

        // Pegado al borde derecho, centrado en el alto disponible
        Vector2 centro = new Vector2(derecha - ancho * 0.5f, (arriba + abajo) * 0.5f);
        rp.anchorMin = rp.anchorMax = new Vector2(0.5f, 0.5f);
        rp.pivot = new Vector2(0.5f, 0.5f);
        rp.sizeDelta = new Vector2(ancho, alto);
        rp.anchoredPosition = centro - r.center;
        rp.SetAsLastSibling();
    }

    /// <summary>Crea el panel a la derecha copiando el fondo y el texto del consejo (mismo estilo).</summary>
    void CrearPanelPoderes()
    {
        if (_panelPoderes != null || canvasPausa == null || textoConsejo == null) return;

        // Fondo: el asignado, o una copia del fondo del consejo
        Image plantilla = fondoPanelPoderes;
        if (plantilla == null)
        {
            Transform f = canvasPausa.transform.Find("FondoConsejo");
            if (f != null) plantilla = f.GetComponent<Image>();
        }

        if (plantilla != null)
        {
            _panelPoderes = Instantiate(plantilla.gameObject, canvasPausa.transform);
            for (int i = _panelPoderes.transform.childCount - 1; i >= 0; i--)
                Destroy(_panelPoderes.transform.GetChild(i).gameObject);
        }
        else
        {
            _panelPoderes = new GameObject("PanelPoderes", typeof(RectTransform), typeof(Image));
            _panelPoderes.transform.SetParent(canvasPausa.transform, false);
            _panelPoderes.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        }
        _panelPoderes.name = "PanelPoderes";

        RectTransform rp = (RectTransform)_panelPoderes.transform;
        rp.anchorMin = rp.anchorMax = new Vector2(1f, 0.5f);
        rp.pivot = new Vector2(1f, 0.5f);
        rp.localScale = Vector3.one;
        rp.anchoredPosition = new Vector2(-margenPanelPoderes, 60f);
        rp.sizeDelta = tamanoPanelPoderes;

        // Texto: copia del consejo (misma fuente), estirado dentro del panel con margen
        _textoPoderes = Instantiate(textoConsejo, _panelPoderes.transform);
        _textoPoderes.name = "TextoPoderes";
        _textoPoderes.gameObject.SetActive(true);
        RectTransform rt = (RectTransform)_textoPoderes.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.offsetMin = new Vector2(32f, 28f);
        rt.offsetMax = new Vector2(-32f, -28f);
        // El texto del consejo usa márgenes negativos enormes para ensancharse; la copia no debe heredarlos
        _textoPoderes.margin = Vector4.zero;
        _textoPoderes.enableAutoSizing = true; // por si el texto no cabe, se encoge solo
        _textoPoderes.fontSizeMin = 14f;
        _textoPoderes.fontSizeMax = tamanoTextoPoderes;
        _textoPoderes.alignment = TextAlignmentOptions.TopLeft;
        _textoPoderes.enableWordWrapping = true;
        _textoPoderes.overflowMode = TextOverflowModes.Truncate; // nunca salirse del cuadro
    }

    /// <summary>Lista de poderes con sus duraciones reales (del GameManager) y cuáles están activos.</summary>
    string TextoPoderes()
    {
        GameManager gm = GameManager.Instance;
        string c = "#" + ColorUtility.ToHtmlStringRGB(colorNombrePoder);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.Append("<size=125%><b>Laptop dorada</b></size>\n");
        sb.Append("Entrégala bien y gana un poder al azar:\n");

        void Linea(Poder p, string nombre, string descripcion)
        {
            sb.Append("\n<color=").Append(c).Append("><b>").Append(nombre).Append("</b></color>: ").Append(descripcion);
            float r = PoderesSupervivencia.Restante(p);
            if (r > 0f) sb.Append(" <color=#5CFF4D><b>(activo ").Append(Mathf.CeilToInt(r)).Append("s)</b></color>");
        }

        string S(float s) => $"{s:0} s";
        if (gm == null)
            return sb.ToString();

        Linea(Poder.QuitarPerdidas, "Perdonar perdidas", $"quita {gm.perdidasQueQuita} laptops perdidas.");
        Linea(Poder.StaminaInfinita, "Café para todos", $"stamina llena e infinita ({S(gm.duracionStaminaInfinita)}).");
        Linea(Poder.ServidoresRapidos, "Servidores fáciles", $"se apagan con un rocío corto ({S(gm.duracionServidoresRapidos)}).");
        Linea(Poder.LaptopsFaciles, "Solo laptops fáciles", $"las nuevas llegan fáciles ({S(gm.duracionLaptopsFaciles)}).");
        Linea(Poder.FuegoCongelado, "Fuego congelado", $"apaga humo y llamas, y nada se calienta ({S(gm.duracionFuegoCongelado)}).");
        Linea(Poder.Respiro, "Respiro", $"no llegan laptops ({S(gm.duracionRespiroPoder)}, desde el minuto {gm.segundoMinimoRespiroPoder / 60f:0}).");
        Linea(Poder.ExtintorInfinito, "Extintor infinito", $"no gasta carga ({S(gm.duracionExtintorInfinito)}).");
        Linea(Poder.ReparacionRapida, "Reparación rápida", $"todas las mesas x{gm.factorReparacionRapida:0} más rápido ({S(gm.duracionReparacionRapida)}).");
        return sb.ToString();
    }

    public void Reanudar()
    {
        CerrarConfiguracion();
        _pausado = false;
        Time.timeScale = 1f;
        if (AudioManager.Instance != null) AudioManager.Instance.ReanudarMusica();
        if (canvasPausa != null) canvasPausa.SetActive(false);
    }

    /// <summary>Botón "Reiniciar Nivel" de la pausa.</summary>
    public void ReiniciarNivel()
    {
        CerrarConfiguracion();
        _pausado = false;
        if (canvasPausa != null) canvasPausa.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.Reintentar();
    }

    /// <summary>Botón "Salir al Menú" de la pausa.</summary>
    public void SalirAlMenu()
    {
        CerrarConfiguracion();
        _pausado = false;
        if (canvasPausa != null) canvasPausa.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.SalirAlMenu();
    }

    // ── Navegación con flechas/control ────────────────────────────────────────

    void ActualizarNavegacion()
    {
        if (_botones.Count == 0) return;

        _timerNav -= Time.unscaledDeltaTime;
        int dir = 0;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.wasPressedThisFrame) dir = -1;
            if (Keyboard.current.downArrowKey.wasPressedThisFrame) dir = 1;
        }

        if (dir == 0)
        {
            foreach (Gamepad gp in Gamepad.all)
            {
                if (gp.dpad.up.wasPressedThisFrame) { dir = -1; break; }
                if (gp.dpad.down.wasPressedThisFrame) { dir = 1; break; }

                if (_timerNav <= 0f)
                {
                    Vector2 stick = gp.leftStick.ReadValue();
                    if (stick.y > 0.5f) { dir = -1; break; }
                    else if (stick.y < -0.5f) { dir = 1; break; }
                }
            }
        }

        if (dir != 0)
        {
            int anterior = _indice;
            _indice = Mathf.Clamp(_indice + dir, 0, _botones.Count - 1);
            if (_indice != anterior) AudioManager.Navegar();
            ActualizarSeleccion();
            _timerNav = 0.2f;
        }

        if (_timerConfirmar > 0f)
        {
            _timerConfirmar -= Time.unscaledDeltaTime;
            return;
        }

        bool confirmar = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        if (!confirmar)
        {
            foreach (Gamepad gp in Gamepad.all)
                if (gp.buttonSouth.wasPressedThisFrame) { confirmar = true; break; }
        }

        if (confirmar)
        {
            Button btn = _botones[_indice].GetComponent<Button>();
            if (btn != null)
            {
                AudioManager.Confirmar();
                btn.onClick.Invoke();
            }
        }
    }

    void ActualizarSeleccion()
    {
        for (int i = 0; i < _botones.Count; i++)
        {
            bool esFoco = i == _indice;
            Outline outline = _botones[i].GetComponent<Outline>();
            if (outline != null) outline.enabled = esFoco;
            _botones[i].transform.localScale = esFoco ? Vector3.one * 1.08f : Vector3.one;
        }
    }
}