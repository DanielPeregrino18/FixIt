using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Menú principal: botones (Jugar / Configuración / Salir) con navegación
/// por teclado y control (↑↓ + E / A), resaltado con Outline + escala,
/// y sincronizado con el mouse (al pasar el mouse sobre un botón, ese queda seleccionado).
/// </summary>
public class MenuPrincipal : MonoBehaviour
{
    [Header("Navegación (de arriba hacia abajo)")]
    [Tooltip("Arrastra aquí los botones del menú en el orden en que aparecen: Jugar, Configuración, Salir")]
    public Button[] botonesMenu;

    [Header("Resaltado")]
    [Tooltip("Cuánto crece el botón seleccionado")]
    public float escalaSeleccionado = 1.08f;

    [Tooltip("Tiempo (seg) tras mostrarse el menú durante el cual se ignora el botón de confirmar. " +
             "Evita que el mismo toque que abrió el menú active un botón.")]
    public float retrasoEntrada = 0.25f;

    [Header("Créditos")]
    [Tooltip("Imagen de créditos. Si se deja vacía se usa la última página de la historieta final (nivel 20)")]
    public Sprite imagenCreditos;
    [Tooltip("Ícono del botón Créditos (reemplaza el ícono izquierdo copiado de Salir)")]
    public Sprite iconoCreditos;
    [Tooltip("Tamaño del ícono de Créditos (ancho, alto)")]
    public Vector2 tamanoIconoCreditos = new Vector2(58f, 72f);
    public float duracionFundidoCreditos = 0.3f;

    // ── Estado interno ────────────────────────────────────────────────────────
    private GameObject _panelCreditos;
    private CanvasGroup _grupoCreditos;
    private bool _creditosAbiertos = false;
    private float _ignorarCreditosHasta = 0f;
    private Coroutine _rutinaCreditos;

    private int _indice = 0;
    private float _timerNavStick = 0f;
    private float _timerEntrada = 0f;
    private bool _hoverConfigurado = false;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        CrearBotonSupervivencia();
        CrearBotonCreditos();
    }

    void OnEnable()
    {
        ConfigurarHoverDeMouse();

        _indice = 0;
        _timerNavStick = 0f;
        _timerEntrada = retrasoEntrada;
        ActualizarSeleccion();
    }

    void Update()
    {
        if (botonesMenu == null || botonesMenu.Length == 0) return;

        if (_creditosAbiertos)
        {
            ActualizarCreditos();
            return;
        }

        // Seguridad: si el menú no está visible (lobby, juego...), no responder
        if (botonesMenu[0] == null || !botonesMenu[0].gameObject.activeInHierarchy) return;

        float dt = Time.unscaledDeltaTime;
        _timerNavStick -= dt;
        _timerEntrada -= dt;

        // ── Moverse arriba / abajo ────────────────────────────────────────────
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

                if (_timerNavStick <= 0f)
                {
                    Vector2 stick = gp.leftStick.ReadValue();
                    if (stick.y > 0.5f) { dir = -1; break; }
                    else if (stick.y < -0.5f) { dir = 1; break; }
                }
            }
        }

        if (dir != 0)
        {
            MoverSeleccion(dir);
            _timerNavStick = 0.2f;
        }

        // ── Confirmar ────────────────────────────────────────────────────────
        if (_timerEntrada > 0f) return;

        bool confirmar = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        if (!confirmar)
        {
            foreach (Gamepad gp in Gamepad.all)
                if (gp.buttonSouth.wasPressedThisFrame) { confirmar = true; break; }
        }

        if (confirmar)
        {
            Button btn = botonesMenu[_indice];
            if (btn != null && btn.gameObject.activeInHierarchy && btn.interactable)
            {
                AudioManager.Confirmar();
                btn.onClick.Invoke();
            }
        }
    }

    // ── Selección ────────────────────────────────────────────────────────────

    void MoverSeleccion(int delta)
    {
        int anterior = _indice;
        _indice = Mathf.Clamp(_indice + delta, 0, botonesMenu.Length - 1);
        if (_indice != anterior) AudioManager.Navegar();
        ActualizarSeleccion();
    }

    void ActualizarSeleccion()
    {
        if (botonesMenu == null) return;

        for (int i = 0; i < botonesMenu.Length; i++)
        {
            if (botonesMenu[i] == null) continue;

            bool esFoco = i == _indice;

            Outline outline = botonesMenu[i].GetComponent<Outline>();
            if (outline != null) outline.enabled = esFoco;

            botonesMenu[i].transform.localScale = esFoco ? Vector3.one * escalaSeleccionado : Vector3.one;
        }
    }

    /// <summary>Al pasar el mouse sobre un botón, ese pasa a ser el seleccionado.</summary>
    void ConfigurarHoverDeMouse()
    {
        if (_hoverConfigurado || botonesMenu == null) return;
        _hoverConfigurado = true;

        for (int i = 0; i < botonesMenu.Length; i++)
        {
            if (botonesMenu[i] == null) continue;

            int indiceLocal = i; // copia para el closure

            EventTrigger trigger = botonesMenu[i].GetComponent<EventTrigger>();
            if (trigger == null) trigger = botonesMenu[i].gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entrada = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entrada.callback.AddListener(_ =>
            {
                if (_indice != indiceLocal) AudioManager.Navegar();
                _indice = indiceLocal;
                ActualizarSeleccion();
            });
            trigger.triggers.Add(entrada);
        }
    }

    // ── Botón Supervivencia (se crea por código copiando el de Jugar) ────────

    /// <summary>Copia el botón Jugar, lo pone justo debajo con el texto "Supervivencia" y lo agrega a la navegación.</summary>
    void CrearBotonSupervivencia()
    {
        if (botonesMenu == null || botonesMenu.Length == 0 || botonesMenu[0] == null) return;
        foreach (Button b in botonesMenu)
            if (b != null && b.name == "BotonSupervivencia") return; // ya existe

        Button jugar = botonesMenu[0];
        Transform padre = jugar.transform.parent;
        GameObject copia = Instantiate(jugar.gameObject, padre);
        copia.name = "BotonSupervivencia";
        copia.transform.SetSiblingIndex(jugar.transform.GetSiblingIndex() + 1);
        copia.transform.localScale = Vector3.one;

        // La copia trae el OnClick de Jugar configurado en el Inspector: se reemplaza entero
        Button boton = copia.GetComponent<Button>();
        boton.onClick = new Button.ButtonClickedEvent();
        boton.onClick.AddListener(JugarSupervivencia);

        // Si el original ya tenía el hover del mouse, la copia lo trae duplicado: se quita
        EventTrigger trigger = copia.GetComponent<EventTrigger>();
        if (trigger != null) trigger.triggers.Clear();

        TMP_Text texto = copia.GetComponentInChildren<TMP_Text>(true);
        TMP_Text original = jugar.GetComponentInChildren<TMP_Text>(true);
        if (texto != null)
        {
            string t = original != null ? original.text : "";
            int n = 0;
            while (n < t.Length && t[n] == ' ') n++; // conserva los espacios con que se alinean los textos
            string resto = t.Substring(n);
            bool mayusculas = resto.Length > 0 && resto == resto.ToUpperInvariant();
            texto.text = t.Substring(0, n) + (mayusculas ? "SUPERVIVENCIA" : "Supervivencia");
        }

        // Hacer espacio en el panel (tiene Layout Group que acomoda los botones)
        RectTransform panel = padre as RectTransform;
        RectTransform rj = jugar.transform as RectTransform;
        if (panel != null && rj != null && panel.GetComponent<LayoutGroup>() != null)
        {
            float espacio = rj.rect.height;
            VerticalLayoutGroup v = panel.GetComponent<VerticalLayoutGroup>();
            if (v != null) espacio += v.spacing;
            panel.sizeDelta += new Vector2(0f, espacio);
        }

        // Insertar en la navegación justo después de Jugar
        List<Button> lista = new List<Button>(botonesMenu);
        lista.Insert(1, boton);
        botonesMenu = lista.ToArray();
    }

    // ── Créditos ─────────────────────────────────────────────────────────────

    /// <summary>Copia el botón Salir (mismo diseño) como "Créditos" y lo pone justo antes de él.</summary>
    void CrearBotonCreditos()
    {
        if (botonesMenu == null || botonesMenu.Length < 2 || botonesMenu[0] == null) return;
        foreach (Button b in botonesMenu)
            if (b != null && b.name == "BotonCreditos") return;

        Button salir = botonesMenu[botonesMenu.Length - 1];
        if (salir == null) return;
        GameObject copia = Instantiate(salir.gameObject, salir.transform.parent);
        copia.name = "BotonCreditos";
        copia.transform.SetSiblingIndex(salir.transform.GetSiblingIndex()); // queda justo antes de Salir
        copia.transform.localScale = Vector3.one;

        Button boton = copia.GetComponent<Button>();
        boton.onClick = new Button.ButtonClickedEvent();
        boton.onClick.AddListener(AbrirCreditos);

        EventTrigger trigger = copia.GetComponent<EventTrigger>();
        if (trigger != null) trigger.triggers.Clear();

        // Ícono propio (el copiado de Salir es el de la puerta)
        Transform icono = copia.transform.Find("IconoIzquierdo");
        Image imgIcono = icono != null ? icono.GetComponent<Image>() : null;
        if (imgIcono != null && iconoCreditos != null)
        {
            imgIcono.sprite = iconoCreditos;
            imgIcono.preserveAspect = false; // se estira al tamaño elegido (más alto que ancho)
            ((RectTransform)icono).sizeDelta = tamanoIconoCreditos;
        }

        TMP_Text texto = copia.GetComponentInChildren<TMP_Text>(true);
        TMP_Text original = salir.GetComponentInChildren<TMP_Text>(true);
        if (texto != null)
        {
            string t = original != null ? original.text : "";
            int n = 0;
            while (n < t.Length && t[n] == ' ') n++;
            string resto = t.Substring(n);
            bool mayusculas = resto.Length > 0 && resto == resto.ToUpperInvariant();
            texto.text = t.Substring(0, n) + (mayusculas ? "CRÉDITOS" : "Créditos");
        }

        RectTransform panel = salir.transform.parent as RectTransform;
        RectTransform rj = salir.transform as RectTransform;
        if (panel != null && rj != null && panel.GetComponent<LayoutGroup>() != null)
        {
            float espacio = rj.rect.height;
            VerticalLayoutGroup v = panel.GetComponent<VerticalLayoutGroup>();
            if (v != null) espacio += v.spacing;
            panel.sizeDelta += new Vector2(0f, espacio);
        }

        List<Button> lista = new List<Button>(botonesMenu);
        lista.Insert(lista.Count - 1, boton);
        botonesMenu = lista.ToArray();
    }

    /// <summary>Botón CRÉDITOS — muestra la imagen de créditos con "A Continuar", como en las historietas.</summary>
    public void AbrirCreditos()
    {
        if (_creditosAbiertos) return;
        if (!CrearPanelCreditos()) return;

        _creditosAbiertos = true;
        _ignorarCreditosHasta = Time.unscaledTime + 0.4f; // que la misma pulsación no lo cierre
        _panelCreditos.SetActive(true);
        _panelCreditos.transform.SetAsLastSibling();
        if (_rutinaCreditos != null) StopCoroutine(_rutinaCreditos);
        _rutinaCreditos = StartCoroutine(FundirCreditos(0f, 1f, false));
    }

    void ActualizarCreditos()
    {
        if (Time.unscaledTime < _ignorarCreditosHasta || _rutinaCreditos != null) return;

        bool cerrar = Keyboard.current != null &&
                      (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame);
        if (!cerrar)
        {
            foreach (Gamepad gp in Gamepad.all)
                if (gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame) { cerrar = true; break; }
        }
        if (!cerrar) return;

        AudioManager.Confirmar();
        _rutinaCreditos = StartCoroutine(FundirCreditos(1f, 0f, true));
    }

    System.Collections.IEnumerator FundirCreditos(float desde, float hasta, bool cerrarAlFinal)
    {
        float t = 0f, dur = Mathf.Max(0.01f, duracionFundidoCreditos);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _grupoCreditos.alpha = Mathf.Lerp(desde, hasta, Mathf.Clamp01(t / dur));
            yield return null;
        }
        _grupoCreditos.alpha = hasta;
        _rutinaCreditos = null;

        if (cerrarAlFinal)
        {
            _panelCreditos.SetActive(false);
            _creditosAbiertos = false;
            _timerEntrada = retrasoEntrada; // la tecla que cerró no debe activar un botón del menú
        }
    }

    /// <summary>
    /// Copia el panel de las historietas (fondo negro, texto "Continuar" y botón A) dentro del canvas
    /// del menú, y en lugar de las viñetas pone la imagen de créditos completa.
    /// </summary>
    bool CrearPanelCreditos()
    {
        if (_panelCreditos != null) return true;

        Sprite imagen = imagenCreditos != null ? imagenCreditos : BuscarImagenCreditos();
        if (imagen == null)
        {
            Debug.LogWarning("MenuPrincipal: no hay imagen de créditos (asigna 'Imagen Creditos' en el Inspector).");
            return false;
        }

        Canvas canvasBoton = botonesMenu[0].GetComponentInParent<Canvas>();
        Canvas canvas = canvasBoton != null ? canvasBoton.rootCanvas : null;
        if (canvas == null) return false;

        ComicManager comics = ComicManager.Instance != null
            ? ComicManager.Instance : FindAnyObjectByType<ComicManager>(FindObjectsInactive.Include);
        GameObject plantilla = comics != null ? comics.panelComic : null;

        RectTransform contenedorOriginal = null;
        if (plantilla != null)
        {
            _panelCreditos = Instantiate(plantilla, canvas.transform);
            Transform c = _panelCreditos.transform.Find("ContenedorPagina");
            if (c != null) contenedorOriginal = (RectTransform)c;
        }
        else
        {
            // Sin panel de historietas: fondo negro simple
            _panelCreditos = new GameObject("PanelCreditos", typeof(RectTransform), typeof(Image));
            _panelCreditos.transform.SetParent(canvas.transform, false);
            _panelCreditos.GetComponent<Image>().color = Color.black;
        }
        _panelCreditos.name = "PanelCreditos";

        RectTransform rp = (RectTransform)_panelCreditos.transform;
        rp.anchorMin = Vector2.zero;
        rp.anchorMax = Vector2.one;
        rp.offsetMin = rp.offsetMax = Vector2.zero;
        rp.localScale = Vector3.one;

        // La imagen ocupa toda la pantalla (sin deformarse), detrás del texto "Continuar" y el botón A
        GameObject goImg = new GameObject("ImagenCreditos", typeof(RectTransform), typeof(Image));
        goImg.transform.SetParent(_panelCreditos.transform, false);
        RectTransform ri = (RectTransform)goImg.transform;
        ri.anchorMin = Vector2.zero;
        ri.anchorMax = Vector2.one;
        ri.offsetMin = ri.offsetMax = Vector2.zero;
        ri.pivot = new Vector2(0.5f, 0.5f);
        ri.localScale = Vector3.one;
        if (contenedorOriginal != null)
        {
            ri.SetSiblingIndex(contenedorOriginal.GetSiblingIndex());
            Destroy(contenedorOriginal.gameObject);
        }
        Image img = goImg.GetComponent<Image>();
        img.sprite = imagen;
        img.preserveAspect = true;
        img.raycastTarget = false;

        _grupoCreditos = _panelCreditos.GetComponent<CanvasGroup>();
        if (_grupoCreditos == null) _grupoCreditos = _panelCreditos.AddComponent<CanvasGroup>();
        _grupoCreditos.alpha = 0f;
        _panelCreditos.SetActive(false);
        return true;
    }

    /// <summary>La última página de la historieta final del nivel 20 (la de "Gracias por jugar").</summary>
    static Sprite BuscarImagenCreditos()
    {
        ComicManager comics = ComicManager.Instance != null
            ? ComicManager.Instance : FindAnyObjectByType<ComicManager>(FindObjectsInactive.Include);
        if (comics == null || comics.historietas == null) return null;

        foreach (ComicManager.Comic c in comics.historietas)
        {
            if (c == null || c.nivel != 20 || c.momento != ComicManager.Momento.Final || c.paginas == null) continue;
            for (int i = c.paginas.Length - 1; i >= 0; i--)
                if (c.paginas[i] != null && c.paginas[i].imagen != null) return c.paginas[i].imagen;
        }
        return null;
    }

    // ── Acciones de los botones ──────────────────────────────────────────────

    /// <summary>Botón JUGAR — lleva al lobby y habilita que los jugadores se unan.</summary>
    public void Jugar()
    {
        ModoSupervivencia.Activo = false;
        GameFlowManager.Instance.MostrarLobby();
    }

    /// <summary>Botón SUPERVIVENCIA — mismo lobby, pero al estar todos listos arranca la supervivencia (sin calendario).</summary>
    public void JugarSupervivencia()
    {
        ModoSupervivencia.Activo = true;
        GameFlowManager.Instance.MostrarLobby();
    }

    /// <summary>Botón SALIR — cierra el juego (en el Editor solo muestra un log).</summary>
    public void Salir()
    {
        Debug.Log("MenuPrincipal: Saliendo del juego...");
        Application.Quit();

#if UNITY_EDITOR
        // En el Editor, Application.Quit no hace nada — esto detiene el Play Mode
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    /// <summary>Botón CONFIGURACIÓN — abre el menú de cuenta y ajustes encima de este.</summary>
    public void Configuracion()
    {
        ConfiguracionMenu.Abrir(this);
    }
}