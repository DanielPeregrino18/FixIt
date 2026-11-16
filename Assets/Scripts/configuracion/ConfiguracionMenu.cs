using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Menú de Configuración: cuenta (iniciar / cerrar sesión, solo desde el menú principal),
/// ajustes básicos (música, efectos, brillo, contraste) y la lista de controles,
/// todo en una sola pantalla con scroll.
///
/// Se construye por código la primera vez que se abre, copiando la fuente y el estilo
/// de los botones del menú que lo abre; no hace falta montarlo en la escena.
/// Se abre desde MenuPrincipal.Configuracion() y desde el menú de pausa (PauseManager).
///
/// Controles: mouse, flechas / d-pad / stick (←→ mueven las barras),
/// E / Enter / A para confirmar, Esc / B para volver.
///
/// En el juego no se pueden crear cuentas: solo se muestra una nota para crearla fuera.
/// </summary>
public class ConfiguracionMenu : MonoBehaviour
{
    // ── Textos fáciles de cambiar ────────────────────────────────────────────
    private const string TEXTO_CREAR_CUENTA =
        "¿No tienes cuenta? Créala en el sitio web de FixIt o descargando la app FixIt.";

    // ── Colores ──────────────────────────────────────────────────────────────
    private static readonly Color COLOR_FONDO_OSCURO = new Color(0f, 0f, 0f, 0.65f);
    private static readonly Color COLOR_PANEL = new Color(0.17f, 0.23f, 0.29f, 0.98f);
    private static readonly Color COLOR_CAJA = new Color(0.12f, 0.17f, 0.22f, 1f);
    private static readonly Color COLOR_TEXTO = Color.white;
    private static readonly Color COLOR_TEXTO_SUAVE = new Color(0.8f, 0.85f, 0.9f, 1f);
    private static readonly Color COLOR_ETIQUETA = new Color(0.62f, 0.7f, 0.78f, 1f);
    private static readonly Color COLOR_FOCO = new Color(1f, 0.84f, 0.29f, 1f);
    private static readonly Color COLOR_BARRA = new Color(0.33f, 0.38f, 0.42f, 1f);
    private static readonly Color COLOR_ERROR = new Color(1f, 0.5f, 0.45f, 1f);

    // Se recuerda el último email para no volver a escribirlo (nunca la contraseña)
    private const string KEY_ULTIMO_EMAIL = "FixIT_UltimoEmail";

    private const float ESCALA_FOCO = 1.06f;
    private const float RETRASO_ENTRADA = 0.25f;

    // ── Controles del juego (de PlayerControls.inputactions; actualizar aquí si cambian) ──
    private static readonly string[,] CONTROLES =
    {
        // Acción                                   Teclado            Control
        { "Moverse",                                "WASD / Flechas",  "Stick izquierdo" },
        { "Correr",                                 "Shift izq.",      "Gatillo der. (RT)" },
        { "Agarrar / soltar / interactuar",         "E",               "A" },
        { "Usar (reparar, limpiar, extintor)",      "Q",               "X" },
        { "Lanzar pieza / extintor",                "R",               "RB" },
        { "Pausa",                                  "P",               "Start" },
    };

    // Una instancia por lugar desde donde se abre (cada una tiene su propio contenido y estilo)
    private static ConfiguracionMenu _instanciaMenu;
    private static ConfiguracionMenu _instanciaPausa;

    // ── Referencias ──────────────────────────────────────────────────────────
    private bool _incluirCuenta;
    private System.Action _alCerrar;
    private TMP_FontAsset _fuente;
    private float _tamTexto = 32f;
    private Color _colorTextoBoton = Color.white;
    private Button _plantillaBoton;

    private RectTransform _viewport;
    private RectTransform _contenido;
    private ScrollRect _scroll;

    private TextMeshProUGUI _textoEstadoCuenta;
    private Button _botonCuenta;
    private TextMeshProUGUI _textoBotonCuenta;
    private Slider _sliderMusica, _sliderEfectos, _sliderBrillo, _sliderContraste;
    private Button _botonVolver;

    // Recuadro de inicio de sesión
    private GameObject _modal;
    private TMP_InputField _inputEmail;
    private TMP_InputField _inputPassword;
    private TextMeshProUGUI _textoEstadoLogin;
    private Button _botonEntrar;
    private Button _botonCancelar;
    private bool _esperandoServidor;

    private readonly List<Selectable> _navegacionPrincipal = new List<Selectable>();
    private readonly List<Selectable> _navegacionModal = new List<Selectable>();
    private readonly Dictionary<Slider, TextMeshProUGUI> _etiquetaDeSlider = new Dictionary<Slider, TextMeshProUGUI>();
    private readonly List<Button> _botones = new List<Button>();

    private float _timerEntrada;
    private GameObject _seleccionAnterior;
    private bool _seleccionPorMouse;

    // ═════════════════════════════════════════════════════════════════════════
    // Abrir / cerrar
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Abre la configuración completa (con cuenta) encima del menú principal.</summary>
    public static void Abrir(MenuPrincipal menu)
    {
        Button plantilla = null;
        if (menu != null && menu.botonesMenu != null)
            foreach (Button b in menu.botonesMenu) if (b != null) { plantilla = b; break; }

        if (menu != null) menu.enabled = false; // el menú principal deja de leer controles mientras tanto
        AbrirInstancia(ref _instanciaMenu, "ConfiguracionMenu", true, plantilla,
            () => { if (menu != null) menu.enabled = true; }); // su OnEnable reinicia la selección
    }

    /// <summary>
    /// Abre la configuración sin la sección de cuenta, encima del menú de pausa.
    /// alCerrar se llama al volver (con Volver, Esc o B) o al cerrarla con CerrarPausa().
    /// </summary>
    public static void AbrirDesdePausa(Button plantillaBoton, System.Action alCerrar)
    {
        AbrirInstancia(ref _instanciaPausa, "ConfiguracionPausa", false, plantillaBoton, alCerrar);
    }

    /// <summary>Cierra la configuración de la pausa si está abierta (p. ej. al reanudar con P / Start).</summary>
    public static void CerrarPausa()
    {
        if (_instanciaPausa != null && _instanciaPausa.gameObject.activeSelf) _instanciaPausa.Cerrar();
    }

    private static void AbrirInstancia(ref ConfiguracionMenu instancia, string nombre, bool incluirCuenta,
                                       Button plantilla, System.Action alCerrar)
    {
        if (instancia == null)
        {
            GameObject go = new GameObject(nombre);
            instancia = go.AddComponent<ConfiguracionMenu>();
            instancia._incluirCuenta = incluirCuenta;
            instancia.Construir(plantilla);
        }
        instancia._alCerrar = alCerrar;
        instancia.Mostrar();
    }

    private void Mostrar()
    {
        gameObject.SetActive(true);
        if (_modal != null) _modal.SetActive(false);

        _sliderMusica.SetValueWithoutNotify(AjustesJuego.VolumenMusica);
        _sliderEfectos.SetValueWithoutNotify(AjustesJuego.VolumenEfectos);
        _sliderBrillo.SetValueWithoutNotify(AjustesJuego.Brillo);
        _sliderContraste.SetValueWithoutNotify(AjustesJuego.Contraste);
        ActualizarTextosSliders();
        RefrescarCuenta();

        _scroll.verticalNormalizedPosition = 1f;
        Seleccionar(null);
        _seleccionAnterior = null;
        // La selección inicial se pone al terminar el retraso: así el mismo botón
        // que abrió el menú no "confirma" también el primer botón de aquí
        _timerEntrada = RETRASO_ENTRADA;
    }

    private void Cerrar()
    {
        AjustesJuego.Guardar();
        Seleccionar(null);
        gameObject.SetActive(false);

        System.Action alCerrar = _alCerrar;
        _alCerrar = null;
        alCerrar?.Invoke();
    }

    void OnEnable()
    {
        if (PlayerDataManager.Instance != null) PlayerDataManager.Instance.UsuarioCambiado += RefrescarCuenta;
    }

    void OnDisable()
    {
        if (PlayerDataManager.Instance != null) PlayerDataManager.Instance.UsuarioCambiado -= RefrescarCuenta;
    }

    void OnDestroy()
    {
        if (_instanciaMenu == this) _instanciaMenu = null;
        if (_instanciaPausa == this) _instanciaPausa = null;
        // Si se destruye estando abierto (cambio de escena), devolver el control a quien lo abrió
        if (gameObject.activeSelf) _alCerrar?.Invoke();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Controles
    // ═════════════════════════════════════════════════════════════════════════

    void Update()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return;

        if (_timerEntrada > 0f)
        {
            _timerEntrada -= Time.unscaledDeltaTime;
            if (_timerEntrada <= 0f) SeleccionarPrimero();
            return;
        }

        bool modalAbierto = _modal != null && _modal.activeSelf;

        // Volver (Esc / B)
        if (PresionoVolver())
        {
            if (modalAbierto) { if (!_esperandoServidor) CerrarModal(); }
            else { AudioManager.Confirmar(); Cerrar(); }
            return;
        }

        GameObject sel = es.currentSelectedGameObject;
        ActualizarResaltado(sel);

        // Si nada está seleccionado (p. ej. tras un clic en el fondo), cualquier dirección recupera el foco
        if (sel == null || !sel.activeInHierarchy)
        {
            if (PresionoDireccion()) SeleccionarPrimero();
            return;
        }

        TMP_InputField campo = sel.GetComponent<TMP_InputField>();

        // Tab: pasa de email a contraseña y viceversa
        if (modalAbierto && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            Seleccionar(sel == _inputEmail.gameObject ? _inputPassword.gameObject : _inputEmail.gameObject);
            return;
        }

        // E confirma botones, igual que en el menú principal (Enter y A ya los maneja el EventSystem).
        // Mientras se escribe en un campo de texto, la E es solo una letra.
        if (campo == null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            Button b = sel.GetComponent<Button>();
            if (b != null && b.interactable) b.onClick.Invoke();
        }

        if (sel != _seleccionAnterior)
        {
            if (_seleccionAnterior != null && sel != null) AudioManager.Navegar();
            if (!_seleccionPorMouse) MantenerVisible(sel.transform as RectTransform);
            _seleccionPorMouse = false;
            _seleccionAnterior = sel;
        }
    }

    private static bool PresionoVolver()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
        foreach (Gamepad gp in Gamepad.all)
            if (gp.buttonEast.wasPressedThisFrame) return true;
        return false;
    }

    private static bool PresionoDireccion()
    {
        Keyboard k = Keyboard.current;
        if (k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame ||
                          k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame))
            return true;
        foreach (Gamepad gp in Gamepad.all)
        {
            if (gp.dpad.up.wasPressedThisFrame || gp.dpad.down.wasPressedThisFrame) return true;
            if (gp.leftStick.ReadValue().sqrMagnitude > 0.25f) return true;
        }
        return false;
    }

    private void SeleccionarPrimero()
    {
        bool modalAbierto = _modal != null && _modal.activeSelf;
        if (modalAbierto) Seleccionar(_inputEmail.gameObject);
        else if (_navegacionPrincipal.Count > 0) Seleccionar(_navegacionPrincipal[0].gameObject);
    }

    private static void Seleccionar(GameObject go)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go);
    }

    /// <summary>El elemento con foco se agranda y se marca con contorno (botones) o en amarillo (barras).</summary>
    private void ActualizarResaltado(GameObject sel)
    {
        foreach (Button b in _botones)
        {
            bool foco = b.gameObject == sel;
            b.transform.localScale = foco ? Vector3.one * ESCALA_FOCO : Vector3.one;
            Outline o = b.GetComponent<Outline>();
            if (o != null) o.enabled = foco;
        }

        foreach (KeyValuePair<Slider, TextMeshProUGUI> par in _etiquetaDeSlider)
        {
            bool foco = par.Key.gameObject == sel;
            par.Value.color = foco ? COLOR_FOCO : COLOR_TEXTO;
            if (par.Key.handleRect != null)
            {
                Image h = par.Key.handleRect.GetComponent<Image>();
                if (h != null) h.color = foco ? COLOR_FOCO : Color.white;
            }
        }
    }

    /// <summary>Con teclado o control, mueve el scroll para que el elemento con foco se vea.</summary>
    private void MantenerVisible(RectTransform objetivo)
    {
        if (objetivo == null || !objetivo.IsChildOf(_contenido)) return;

        Vector3[] esquinas = new Vector3[4];
        objetivo.GetWorldCorners(esquinas);
        float abajo = _viewport.InverseTransformPoint(esquinas[0]).y;
        float arriba = _viewport.InverseTransformPoint(esquinas[1]).y;
        Rect r = _viewport.rect;
        const float margen = 16f;

        Vector2 pos = _contenido.anchoredPosition;
        if (arriba + margen > r.yMax) pos.y -= (arriba + margen - r.yMax);
        else if (abajo - margen < r.yMin) pos.y += (r.yMin - (abajo - margen));
        else return;

        float maxY = Mathf.Max(0f, _contenido.rect.height - r.height);
        pos.y = Mathf.Clamp(pos.y, 0f, maxY);
        _contenido.anchoredPosition = pos;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Cuenta
    // ═════════════════════════════════════════════════════════════════════════

    private void RefrescarCuenta()
    {
        if (_textoEstadoCuenta == null) return;

        PlayerDataManager datos = PlayerDataManager.Instance;
        if (datos != null && datos.HaySesion)
        {
            string nombre = string.IsNullOrEmpty(datos.CurrentUsername) ? datos.CurrentUser?.email : datos.CurrentUsername;
            _textoEstadoCuenta.text = $"Sesión iniciada: <b>{nombre}</b>\n" +
                                      "<size=80%>Tu progreso se guarda en tu cuenta.</size>";
            _textoBotonCuenta.text = "Cerrar sesión";
        }
        else
        {
            _textoEstadoCuenta.text = "Sin sesión\n<size=80%>Tu progreso se guarda solo en esta PC.</size>";
            _textoBotonCuenta.text = "Iniciar sesión";
        }
    }

    private void AlPresionarCuenta()
    {
        AudioManager.Confirmar();
        PlayerDataManager datos = PlayerDataManager.Instance;
        if (datos != null && datos.HaySesion) datos.CerrarSesion(); // RefrescarCuenta llega por el evento
        else AbrirModal();
    }

    private void AbrirModal()
    {
        _modal.SetActive(true);
        _inputEmail.text = PlayerPrefs.GetString(KEY_ULTIMO_EMAIL, "");
        _inputPassword.text = "";
        MostrarEstadoLogin("", false);
        PonerEsperando(false);
        Seleccionar(string.IsNullOrEmpty(_inputEmail.text) ? _inputEmail.gameObject : _inputPassword.gameObject);
    }

    private void CerrarModal()
    {
        _modal.SetActive(false);
        _inputPassword.text = "";
        Seleccionar(_botonCuenta.gameObject);
    }

    private void Entrar()
    {
        if (_esperandoServidor) return;

        string email = _inputEmail.text.Trim();
        string password = _inputPassword.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            MostrarEstadoLogin("Escribe tu email y tu contraseña.", true);
            return;
        }
        if (FixItNetworkManager.Instance == null || PlayerDataManager.Instance == null)
        {
            MostrarEstadoLogin("No se puede conectar con el servidor ahora mismo.", true);
            return;
        }

        AudioManager.Confirmar();
        PonerEsperando(true);
        MostrarEstadoLogin("Conectando… la primera vez puede tardar hasta un minuto.", false);

        FixItNetworkManager.Instance.Login(email, password,
            onSuccess: user =>
            {
                if (this == null) return; // el menú se destruyó mientras esperaba
                PonerEsperando(false);

                if (user == null || string.IsNullOrEmpty(user._id))
                {
                    MostrarEstadoLogin("El servidor respondió sin datos de la cuenta. Intenta de nuevo.", true);
                    return;
                }

                PlayerPrefs.SetString(KEY_ULTIMO_EMAIL, email);
                PlayerDataManager.Instance.IniciarSesion(user); // el progreso local se suma a la cuenta
                CerrarModal();
            },
            onError: error =>
            {
                if (this == null) return;
                PonerEsperando(false);
                MostrarEstadoLogin(TraducirError(error), true);
            });
    }

    private static string TraducirError(string error)
    {
        if (string.IsNullOrEmpty(error)) return "No se pudo iniciar sesión.";
        if (error.StartsWith("HTTP 401") || error.StartsWith("HTTP 400") || error.StartsWith("HTTP 404"))
            return "Email o contraseña incorrectos.";
        if (error.StartsWith("HTTP 0 "))
            return "Sin conexión a internet. Puedes seguir jugando en local.";
        if (error.StartsWith("HTTP 5"))
            return "El servidor no está disponible. Intenta más tarde.";
        return "No se pudo iniciar sesión. Intenta de nuevo.";
    }

    private void MostrarEstadoLogin(string texto, bool esError)
    {
        _textoEstadoLogin.text = texto;
        _textoEstadoLogin.color = esError ? COLOR_ERROR : COLOR_TEXTO_SUAVE;
        _textoEstadoLogin.gameObject.SetActive(!string.IsNullOrEmpty(texto));
    }

    private void PonerEsperando(bool esperando)
    {
        _esperandoServidor = esperando;
        _inputEmail.interactable = !esperando;
        _inputPassword.interactable = !esperando;
        _botonEntrar.interactable = !esperando;
        _botonCancelar.interactable = !esperando;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Ajustes
    // ═════════════════════════════════════════════════════════════════════════

    private void AlCambiarVolumen(float _)
    {
        AjustesJuego.GuardarVolumen(_sliderMusica.value, _sliderEfectos.value);
        ActualizarTextosSliders();
    }

    private void AlCambiarImagen(float _)
    {
        AjustesJuego.GuardarImagen(_sliderBrillo.value, _sliderContraste.value);
        ActualizarTextosSliders();
    }

    private readonly Dictionary<Slider, TextMeshProUGUI> _valorDeSlider = new Dictionary<Slider, TextMeshProUGUI>();

    private void ActualizarTextosSliders()
    {
        foreach (KeyValuePair<Slider, TextMeshProUGUI> par in _valorDeSlider)
            par.Value.text = Mathf.RoundToInt(par.Key.value * 100f) + "%";
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Construcción de la interfaz
    // ═════════════════════════════════════════════════════════════════════════

    private void Construir(Button plantilla)
    {
        LeerEstilo(plantilla);

        // Canvas propio, encima de todo, con la misma escala que el del menú que lo abre
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        Vector2 referencia = CopiarEscala(plantilla, scaler);
        gameObject.AddComponent<GraphicRaycaster>();

        // Fondo oscuro que tapa el menú (y bloquea sus clics)
        RectTransform raiz = (RectTransform)transform;
        Image fondo = CrearImagen("Fondo", raiz, COLOR_FONDO_OSCURO);
        Estirar(fondo.rectTransform);

        // Panel principal
        float alto = Mathf.Min(referencia.y * 0.88f, 860f);
        float ancho = Mathf.Min(referencia.x * 0.9f, 820f);
        Image panel = CrearImagen("Panel", raiz, COLOR_PANEL);
        Centrar(panel.rectTransform, new Vector2(ancho, alto));

        TextMeshProUGUI titulo = CrearTexto("Titulo", panel.rectTransform, "Configuración", _tamTexto * 1.3f, COLOR_TEXTO);
        titulo.alignment = TextAlignmentOptions.Center;
        titulo.fontStyle = FontStyles.Bold;
        AnclarArriba(titulo.rectTransform, 24f, 70f);

        float altoVolver = AltoBoton() + 40f;

        // Zona con scroll entre el título y el botón Volver
        CrearScroll(panel.rectTransform, top: 104f, bottom: altoVolver);

        if (_incluirCuenta)
        {
            ConstruirCuenta();
            CrearSeparador();
        }
        CrearEtiquetaSeccion("SONIDO");
        _sliderMusica = CrearFilaSlider("Música", AlCambiarVolumen);
        _sliderEfectos = CrearFilaSlider("Efectos", AlCambiarVolumen);
        CrearSeparador();
        CrearEtiquetaSeccion("PANTALLA");
        _sliderBrillo = CrearFilaSlider("Brillo", AlCambiarImagen);
        _sliderContraste = CrearFilaSlider("Contraste", AlCambiarImagen);
        CrearSeparador();
        CrearEtiquetaSeccion("CONTROLES");
        Selectable cajaControles = ConstruirControles();

        // Volver, fijo abajo
        _botonVolver = CrearBoton("Volver", panel.rectTransform, () => { AudioManager.Confirmar(); Cerrar(); });
        RectTransform rv = (RectTransform)_botonVolver.transform;
        rv.anchorMin = rv.anchorMax = new Vector2(0.5f, 0f);
        rv.pivot = new Vector2(0.5f, 0f);
        rv.anchoredPosition = new Vector2(0f, 20f);

        // La caja de controles también recibe el foco: así con teclado o control
        // se puede bajar hasta ella y el scroll la muestra
        if (_botonCuenta != null) _navegacionPrincipal.Add(_botonCuenta);
        _navegacionPrincipal.AddRange(new Selectable[]
            { _sliderMusica, _sliderEfectos, _sliderBrillo, _sliderContraste, cajaControles, _botonVolver });
        EncadenarNavegacion(_navegacionPrincipal);

        if (_incluirCuenta) ConstruirModal(raiz, ancho);
    }

    /// <summary>Tabla Acción / Teclado / Control. Devuelve la caja (seleccionable, para poder llegar con el scroll).</summary>
    private Selectable ConstruirControles()
    {
        Image caja = CrearImagen("Controles", _contenido, Color.white);
        VerticalLayoutGroup v = caja.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(20, 20, 14, 16);
        v.spacing = 6f;
        ConfigurarLayout(v);
        caja.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // Resaltado con el tinte del Selectable: un poco más clara cuando tiene el foco
        Selectable sel = caja.gameObject.AddComponent<Selectable>();
        sel.targetGraphic = caja;
        ColorBlock cb = sel.colors;
        cb.normalColor = COLOR_CAJA;
        cb.highlightedColor = COLOR_CAJA;
        cb.selectedColor = new Color(0.2f, 0.27f, 0.35f, 1f);
        cb.pressedColor = cb.selectedColor;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        sel.colors = cb;
        AgregarHoverSelecciona(caja.gameObject);

        CrearFilaControles(caja.rectTransform, "Acción", "Teclado", "Control", COLOR_ETIQUETA, _tamTexto * 0.55f);
        for (int i = 0; i < CONTROLES.GetLength(0); i++)
            CrearFilaControles(caja.rectTransform, CONTROLES[i, 0], CONTROLES[i, 1], CONTROLES[i, 2],
                               COLOR_TEXTO, _tamTexto * 0.65f);

        return sel;
    }

    private void CrearFilaControles(Transform padre, string accion, string teclado, string control, Color color, float tam)
    {
        GameObject fila = new GameObject("Fila_" + accion, typeof(RectTransform));
        fila.transform.SetParent(padre, false);
        HorizontalLayoutGroup h = fila.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 12f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        fila.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // Columnas proporcionales: la acción ocupa más
        AgregarColumna(fila.transform, accion, color, tam, 1.7f, TextAlignmentOptions.MidlineLeft);
        AgregarColumna(fila.transform, teclado, color, tam, 1f, TextAlignmentOptions.Center);
        AgregarColumna(fila.transform, control, color, tam, 1f, TextAlignmentOptions.Center);
    }

    private void AgregarColumna(Transform fila, string texto, Color color, float tam, float peso, TextAlignmentOptions alineacion)
    {
        TextMeshProUGUI t = CrearTexto("Col", fila, texto, tam, color);
        t.alignment = alineacion;
        LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = peso;
        le.preferredWidth = 0f; // el ancho lo reparten los pesos, no el largo del texto
    }

    private void ConstruirCuenta()
    {
        Image caja = CrearImagen("Cuenta", _contenido, COLOR_CAJA);
        VerticalLayoutGroup v = caja.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(24, 24, 18, 22);
        v.spacing = 12f;
        ConfigurarLayout(v);
        caja.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        TextMeshProUGUI etiqueta = CrearTexto("Etiqueta", caja.rectTransform, "CUENTA", _tamTexto * 0.55f, COLOR_ETIQUETA);
        etiqueta.characterSpacing = 8f;
        Expandir(etiqueta);

        _textoEstadoCuenta = CrearTexto("Estado", caja.rectTransform, "", _tamTexto * 0.8f, COLOR_TEXTO);
        Expandir(_textoEstadoCuenta);

        _botonCuenta = CrearBoton("Iniciar sesión", caja.rectTransform, AlPresionarCuenta);
        _textoBotonCuenta = _botonCuenta.GetComponentInChildren<TextMeshProUGUI>();
    }

    private void ConstruirModal(RectTransform raiz, float anchoPanel)
    {
        Image fondo = CrearImagen("InicioSesion", raiz, COLOR_FONDO_OSCURO);
        Estirar(fondo.rectTransform);
        _modal = fondo.gameObject;

        Image caja = CrearImagen("Recuadro", fondo.rectTransform, COLOR_PANEL);
        RectTransform rc = caja.rectTransform;
        rc.anchorMin = rc.anchorMax = rc.pivot = new Vector2(0.5f, 0.5f);
        Outline borde = caja.gameObject.AddComponent<Outline>();
        borde.effectColor = new Color(1f, 1f, 1f, 0.25f);
        borde.effectDistance = new Vector2(2f, -2f);

        VerticalLayoutGroup v = caja.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(36, 36, 28, 28);
        v.spacing = 14f;
        ConfigurarLayout(v);
        ContentSizeFitter fit = caja.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        rc.sizeDelta = new Vector2(Mathf.Max(anchoPanel - 80f, 400f), 0f);

        TextMeshProUGUI titulo = CrearTexto("Titulo", rc, "Iniciar sesión", _tamTexto * 1.1f, COLOR_TEXTO);
        titulo.alignment = TextAlignmentOptions.Center;
        titulo.fontStyle = FontStyles.Bold;
        Expandir(titulo);

        _inputEmail = CrearCampo(rc, "Email", TMP_InputField.ContentType.EmailAddress);
        _inputPassword = CrearCampo(rc, "Contraseña", TMP_InputField.ContentType.Password);
        _inputPassword.onSubmit.AddListener(_ => Entrar());
        _inputEmail.onSubmit.AddListener(_ =>
        {
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                Seleccionar(_inputPassword.gameObject);
        });

        _textoEstadoLogin = CrearTexto("Estado", rc, "", _tamTexto * 0.6f, COLOR_TEXTO_SUAVE);
        _textoEstadoLogin.alignment = TextAlignmentOptions.Center;
        Expandir(_textoEstadoLogin);

        // Botones en fila
        GameObject fila = new GameObject("Botones", typeof(RectTransform));
        fila.transform.SetParent(rc, false);
        HorizontalLayoutGroup h = fila.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 24f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        fila.AddComponent<LayoutElement>().flexibleWidth = 1f;

        _botonEntrar = CrearBoton("Entrar", fila.transform, Entrar);
        _botonCancelar = CrearBoton("Cancelar", fila.transform, () => { AudioManager.Confirmar(); CerrarModal(); });
        // Más angostos que los del menú para que quepan los dos en una fila
        float anchoFila = Mathf.Min(280f, (rc.sizeDelta.x - 72f - 24f) / 2f);
        _botonEntrar.GetComponent<LayoutElement>().preferredWidth = anchoFila;
        _botonCancelar.GetComponent<LayoutElement>().preferredWidth = anchoFila;

        TextMeshProUGUI nota = CrearTexto("NotaCrearCuenta", rc, TEXTO_CREAR_CUENTA, _tamTexto * 0.55f, COLOR_TEXTO_SUAVE);
        nota.alignment = TextAlignmentOptions.Center;
        nota.fontStyle = FontStyles.Italic;
        Expandir(nota);

        _navegacionModal.AddRange(new Selectable[] { _inputEmail, _inputPassword, _botonEntrar, _botonCancelar });
        EncadenarNavegacion(_navegacionModal);
        // Entrar ↔ Cancelar con izquierda / derecha
        Navigation ne = _botonEntrar.navigation; ne.selectOnRight = _botonCancelar; _botonEntrar.navigation = ne;
        Navigation nc = _botonCancelar.navigation; nc.selectOnLeft = _botonEntrar; nc.selectOnUp = _inputPassword; _botonCancelar.navigation = nc;

        _modal.SetActive(false);
    }

    // ── Piezas reutilizables ─────────────────────────────────────────────────

    private void LeerEstilo(Button plantilla)
    {
        if (plantilla == null) return;

        _plantillaBoton = plantilla;
        TMP_Text t = plantilla.GetComponentInChildren<TMP_Text>(true);
        if (t != null)
        {
            _fuente = t.font;
            _tamTexto = t.fontSize > 0f ? t.fontSize : _tamTexto;
            _colorTextoBoton = t.color;
        }
    }

    private static Vector2 CopiarEscala(Button plantilla, CanvasScaler destino)
    {
        Canvas canvasMenu = plantilla != null ? plantilla.GetComponentInParent<Canvas>(true) : null;
        CanvasScaler origen = canvasMenu != null ? canvasMenu.rootCanvas.GetComponent<CanvasScaler>() : null;

        if (origen != null)
        {
            destino.uiScaleMode = origen.uiScaleMode;
            destino.referenceResolution = origen.referenceResolution;
            destino.screenMatchMode = origen.screenMatchMode;
            destino.matchWidthOrHeight = origen.matchWidthOrHeight;
            destino.scaleFactor = origen.scaleFactor;
        }
        else
        {
            destino.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            destino.referenceResolution = new Vector2(1920f, 1080f);
            destino.matchWidthOrHeight = 0.5f;
        }

        return destino.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
            ? destino.referenceResolution
            : new Vector2(Screen.width, Screen.height) / Mathf.Max(0.01f, destino.scaleFactor);
    }

    private void CrearScroll(RectTransform padre, float top, float bottom)
    {
        GameObject goScroll = new GameObject("Scroll", typeof(RectTransform));
        goScroll.transform.SetParent(padre, false);
        RectTransform rs = (RectTransform)goScroll.transform;
        rs.anchorMin = Vector2.zero;
        rs.anchorMax = Vector2.one;
        rs.offsetMin = new Vector2(28f, bottom);
        rs.offsetMax = new Vector2(-28f, -top);

        GameObject goView = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        goView.transform.SetParent(rs, false);
        _viewport = (RectTransform)goView.transform;
        Estirar(_viewport);
        // Imagen transparente para que la rueda del mouse funcione en los huecos
        Image hit = goView.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);

        GameObject goCont = new GameObject("Contenido", typeof(RectTransform));
        goCont.transform.SetParent(_viewport, false);
        _contenido = (RectTransform)goCont.transform;
        _contenido.anchorMin = new Vector2(0f, 1f);
        _contenido.anchorMax = new Vector2(1f, 1f);
        _contenido.pivot = new Vector2(0.5f, 1f);
        _contenido.sizeDelta = Vector2.zero;

        VerticalLayoutGroup v = goCont.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(8, 8, 8, 16);
        v.spacing = 16f;
        ConfigurarLayout(v);
        goCont.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scroll = goScroll.AddComponent<ScrollRect>();
        _scroll.viewport = _viewport;
        _scroll.content = _contenido;
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 40f;
    }

    private static void ConfigurarLayout(VerticalLayoutGroup v)
    {
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = false; // los textos y filas se estiran con flexibleWidth; los botones no
        v.childForceExpandHeight = false;
    }

    private void CrearEtiquetaSeccion(string texto)
    {
        TextMeshProUGUI t = CrearTexto("Seccion_" + texto, _contenido, texto, _tamTexto * 0.55f, COLOR_ETIQUETA);
        t.characterSpacing = 8f;
        Expandir(t);
    }

    private void CrearSeparador()
    {
        Image linea = CrearImagen("Separador", _contenido, new Color(1f, 1f, 1f, 0.12f));
        LayoutElement le = linea.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 2f;
        le.flexibleWidth = 1f;
    }

    private Slider CrearFilaSlider(string nombre, UnityAction<float> alCambiar)
    {
        GameObject fila = new GameObject("Fila_" + nombre, typeof(RectTransform));
        fila.transform.SetParent(_contenido, false);
        HorizontalLayoutGroup h = fila.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 20f;
        h.padding = new RectOffset(12, 12, 4, 4);
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        LayoutElement leFila = fila.AddComponent<LayoutElement>();
        leFila.flexibleWidth = 1f;
        leFila.minHeight = 52f;

        TextMeshProUGUI etiqueta = CrearTexto("Etiqueta", fila.transform, nombre, _tamTexto * 0.8f, COLOR_TEXTO);
        etiqueta.alignment = TextAlignmentOptions.MidlineLeft;
        etiqueta.gameObject.AddComponent<LayoutElement>().preferredWidth = 200f;

        GameObject goSlider = DefaultControls.CreateSlider(new DefaultControls.Resources());
        goSlider.name = "Slider_" + nombre;
        goSlider.transform.SetParent(fila.transform, false);
        LayoutElement leSlider = goSlider.AddComponent<LayoutElement>();
        leSlider.flexibleWidth = 1f;
        leSlider.preferredHeight = 30f;

        Slider s = goSlider.GetComponent<Slider>();
        s.minValue = 0f;
        s.maxValue = 1f;
        // Colores: barra gris, relleno amarillo, perilla blanca (amarilla con foco)
        foreach (Image img in goSlider.GetComponentsInChildren<Image>(true))
        {
            if (img.name == "Background") img.color = COLOR_BARRA;
            else if (img.name == "Fill") img.color = COLOR_FOCO;
            else if (img.name == "Handle") img.color = Color.white;
        }
        if (s.handleRect != null) s.handleRect.sizeDelta = new Vector2(26f, 10f);
        ColorBlock cb = s.colors;
        cb.highlightedColor = cb.selectedColor = Color.white;
        s.colors = cb;
        s.onValueChanged.AddListener(alCambiar);
        AgregarHoverSelecciona(goSlider);

        TextMeshProUGUI valor = CrearTexto("Valor", fila.transform, "100%", _tamTexto * 0.7f, COLOR_TEXTO_SUAVE);
        valor.alignment = TextAlignmentOptions.MidlineRight;
        valor.gameObject.AddComponent<LayoutElement>().preferredWidth = 90f;

        _etiquetaDeSlider[s] = etiqueta;
        _valorDeSlider[s] = valor;
        return s;
    }

    private TMP_InputField CrearCampo(Transform padre, string placeholder, TMP_InputField.ContentType tipo)
    {
        GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = "Campo_" + placeholder;
        go.transform.SetParent(padre, false);

        TMP_InputField campo = go.GetComponent<TMP_InputField>();
        campo.contentType = tipo;
        campo.lineType = TMP_InputField.LineType.SingleLine;
        campo.characterLimit = 120;
        campo.pointSize = _tamTexto * 0.7f;

        if (campo.textComponent != null)
        {
            if (_fuente != null) campo.textComponent.font = _fuente;
            campo.textComponent.color = new Color(0.12f, 0.12f, 0.15f, 1f);
        }
        TMP_Text ph = campo.placeholder as TMP_Text;
        if (ph != null)
        {
            if (_fuente != null) ph.font = _fuente;
            ph.text = placeholder;
            ph.fontStyle = FontStyles.Italic;
        }

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = _tamTexto * 1.6f;
        le.flexibleWidth = 1f;

        ColorBlock cb = campo.colors;
        cb.selectedColor = new Color(1f, 0.97f, 0.85f, 1f);
        campo.colors = cb;
        return campo;
    }

    private float AltoBoton()
    {
        return _plantillaBoton != null ? Mathf.Max(48f, ((RectTransform)_plantillaBoton.transform).rect.height) : 64f;
    }

    /// <summary>Botón con el mismo aspecto que los del menú principal (sprite, colores, fuente y contorno).</summary>
    private Button CrearBoton(string texto, Transform padre, UnityAction alPresionar)
    {
        GameObject go = new GameObject("Boton_" + texto, typeof(RectTransform));
        go.transform.SetParent(padre, false);

        Image img = go.AddComponent<Image>();
        Button b = go.AddComponent<Button>();
        Outline outline = go.AddComponent<Outline>();

        Vector2 tam = new Vector2(360f, AltoBoton());
        Image imgPlantilla = _plantillaBoton != null ? _plantillaBoton.GetComponent<Image>() : null;
        if (imgPlantilla != null)
        {
            img.sprite = imgPlantilla.sprite;
            img.type = imgPlantilla.type;
            img.color = imgPlantilla.color;
            img.pixelsPerUnitMultiplier = imgPlantilla.pixelsPerUnitMultiplier;
            b.colors = _plantillaBoton.colors;
            b.transition = _plantillaBoton.transition;
            b.spriteState = _plantillaBoton.spriteState;
            tam.x = Mathf.Clamp(((RectTransform)_plantillaBoton.transform).rect.width, 260f, 420f);

            Outline o = _plantillaBoton.GetComponent<Outline>();
            if (o != null) { outline.effectColor = o.effectColor; outline.effectDistance = o.effectDistance; }
            else { outline.effectColor = COLOR_FOCO; outline.effectDistance = new Vector2(4f, -4f); }
        }
        else
        {
            img.color = new Color(0.25f, 0.5f, 0.75f, 1f);
            outline.effectColor = COLOR_FOCO;
            outline.effectDistance = new Vector2(4f, -4f);
        }
        outline.enabled = false;
        ((RectTransform)go.transform).sizeDelta = tam;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredWidth = tam.x;
        le.preferredHeight = tam.y;

        TextMeshProUGUI t = CrearTexto("Texto", go.transform, texto, _tamTexto * 0.85f, _colorTextoBoton);
        t.alignment = TextAlignmentOptions.Center;
        Estirar(t.rectTransform);

        b.onClick.AddListener(alPresionar);
        AgregarHoverSelecciona(go);
        _botones.Add(b);
        return b;
    }

    /// <summary>Al pasar el mouse por encima, el elemento toma el foco (igual que en el menú principal).</summary>
    private void AgregarHoverSelecciona(GameObject go)
    {
        EventTrigger trigger = go.AddComponent<EventTrigger>();
        EventTrigger.Entry entrada = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entrada.callback.AddListener(_ =>
        {
            Selectable s = go.GetComponent<Selectable>();
            if (s == null || !s.interactable || EventSystem.current == null) return;
            // No quitarle el foco a un campo de texto en el que se está escribiendo
            GameObject actual = EventSystem.current.currentSelectedGameObject;
            if (actual != null && actual.GetComponent<TMP_InputField>() != null) return;
            _seleccionPorMouse = true;
            EventSystem.current.SetSelectedGameObject(go);
        });
        trigger.triggers.Add(entrada);
    }

    private static void EncadenarNavegacion(List<Selectable> lista)
    {
        for (int i = 0; i < lista.Count; i++)
        {
            Navigation n = new Navigation { mode = Navigation.Mode.Explicit };
            n.selectOnUp = i > 0 ? lista[i - 1] : null;
            n.selectOnDown = i < lista.Count - 1 ? lista[i + 1] : null;
            lista[i].navigation = n;
        }
    }

    private TextMeshProUGUI CrearTexto(string nombre, Transform padre, string texto, float tam, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (_fuente != null) t.font = _fuente;
        t.text = texto;
        t.fontSize = tam;
        t.color = color;
        t.raycastTarget = false;
        t.richText = true;
        return t;
    }

    private static void Expandir(TextMeshProUGUI t)
    {
        t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
    }

    private static Image CrearImagen(string nombre, Transform padre, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static void Estirar(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private static void Centrar(RectTransform r, Vector2 tam)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = tam;
        r.anchoredPosition = Vector2.zero;
    }

    private static void AnclarArriba(RectTransform r, float margenArriba, float alto)
    {
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(24f, -margenArriba - alto);
        r.offsetMax = new Vector2(-24f, -margenArriba);
    }
}
