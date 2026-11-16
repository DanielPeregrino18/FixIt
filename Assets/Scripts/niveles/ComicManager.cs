using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Reproduce las historietas del juego. Cada página se muestra sobre fondo negro y las viñetas
/// se revelan una por una: cada A (cualquier jugador) / E muestra la siguiente; al terminar las
/// viñetas de una página pasa a la siguiente con un fundido, y al terminar la última se cierra.
/// Las viñetas se dibujan recortando la imagen de la página (RawImage + uvRect), así que
/// lo que queda entre viñetas se ve negro.
/// </summary>
public class ComicManager : MonoBehaviour
{
    public static ComicManager Instance { get; private set; }

    /// <summary>true mientras hay una historieta en pantalla.</summary>
    public static bool Activo { get; private set; }

    public enum Momento { Inicio, Final }

    [Serializable]
    public class PaginaComic
    {
        public Sprite imagen;
        [Tooltip("Una viñeta por elemento, en orden de lectura. X, Y, Ancho, Alto en PÍXELES de la imagen original, " +
                 "con el origen (0,0) en la esquina SUPERIOR izquierda.")]
        public Vector4[] vinetas;
    }

    [Serializable]
    public class Comic
    {
        public string nombre;
        [Tooltip("Número de nivel (no el día del calendario)")]
        public int nivel;
        public Momento momento;
        public PaginaComic[] paginas;
    }

    [Header("UI")]
    [Tooltip("Panel a pantalla completa con fondo NEGRO (se activa/desactiva solo)")]
    public GameObject panelComic;
    [Tooltip("Contenedor de la página, centrado, con CanvasGroup (el script le pone el Aspect Ratio Fitter)")]
    public RectTransform contenedorPagina;

    [Header("Animación")]
    public float duracionRevelado = 0.35f;
    [Tooltip("Escala inicial de cada viñeta al aparecer (1 = sin efecto)")]
    [Range(0.8f, 1f)] public float escalaInicialVineta = 0.95f;
    public float duracionCambioPagina = 0.3f;

    [Header("Historietas")]
    public List<Comic> historietas = new List<Comic>
    {
        new Comic { nombre = "Nivel 1 - Inicio",  nivel = 1,  momento = Momento.Inicio },
        new Comic { nombre = "Nivel 6 - Inicio",  nivel = 6,  momento = Momento.Inicio },
        new Comic { nombre = "Nivel 6 - Final",   nivel = 6,  momento = Momento.Final  },
        new Comic { nombre = "Nivel 11 - Inicio", nivel = 11, momento = Momento.Inicio },
        new Comic { nombre = "Nivel 13 - Final",  nivel = 13, momento = Momento.Final  },
        new Comic { nombre = "Nivel 14 - Final",  nivel = 14, momento = Momento.Final  },
        new Comic { nombre = "Nivel 20 - Final",  nivel = 20, momento = Momento.Final  },
    };

    [Tooltip("Segundos tras aparecer en los que se ignora la A")]
    public float retrasoEntrada = 0.4f;

    // ── Estado interno ────────────────────────────────────────────────────────
    private Comic _actual;
    private Momento _momentoActual;
    private Action _alTerminar;
    private int _pagina;
    private int _vinetasReveladas;
    private bool _ocupado;           // animando (revelado o cambio de página)
    private float _ignorarHasta;
    private Coroutine _rutina;

    private CanvasGroup _grupoPagina;
    private AspectRatioFitter _fitter;
    private readonly List<RawImage> _vinetasUI = new List<RawImage>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Activo = false;

        if (contenedorPagina != null)
        {
            _grupoPagina = contenedorPagina.GetComponent<CanvasGroup>();
            if (_grupoPagina == null) _grupoPagina = contenedorPagina.gameObject.AddComponent<CanvasGroup>();

            _fitter = contenedorPagina.GetComponent<AspectRatioFitter>();
            if (_fitter == null) _fitter = contenedorPagina.gameObject.AddComponent<AspectRatioFitter>();
            _fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        }
    }

    void Start()
    {
        if (panelComic != null) panelComic.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) { Instance = null; Activo = false; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // API PÚBLICA
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>¿Existe una historieta (con páginas) para ese nivel y momento?</summary>
    public bool Existe(int nivel, Momento momento) => Buscar(nivel, momento) != null;

    /// <summary>
    /// Muestra la historieta del nivel/momento. Devuelve true si se mostró (y entonces
    /// alTerminar se llamará al cerrarla); false si no hay historieta (alTerminar NO se llama).
    /// </summary>
    public bool Mostrar(int nivel, Momento momento, Action alTerminar)
    {
        Comic c = Buscar(nivel, momento);
        if (c == null || panelComic == null || contenedorPagina == null) return false;

        if (_rutina != null) StopCoroutine(_rutina);

        _actual = c;
        _momentoActual = momento;
        _alTerminar = alTerminar;
        _pagina = 0;
        _ocupado = true;
        _ignorarHasta = Time.unscaledTime + retrasoEntrada;

        Activo = true;
        Time.timeScale = 0f;

        if (AudioManager.Instance != null) AudioManager.Instance.IniciarMusicaHistorieta();

        // El panel es negro y cubre todo: ya no hace falta el fundido, se quita de golpe.
        panelComic.SetActive(true);
        if (FundidoPantalla.Instance != null) FundidoPantalla.Instance.TransparenteInstantaneo();

        _rutina = StartCoroutine(PrepararPagina(primera: true));
        return true;
    }

    /// <summary>Cierra de golpe sin llamar a alTerminar (por ejemplo al salir al menú).</summary>
    public void CancelarSinCallback()
    {
        if (_rutina != null) { StopCoroutine(_rutina); _rutina = null; }
        _actual = null;
        _alTerminar = null;
        Activo = false;
        if (panelComic != null) panelComic.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INPUT
    // ═══════════════════════════════════════════════════════════════════════

    void Update()
    {
        if (!Activo || _actual == null || _ocupado) return;
        if (Time.unscaledTime < _ignorarHasta) return;
        if (!ConfirmarPresionado()) return;

        AudioManager.Confirmar();

        if (_vinetasReveladas < _vinetasActivas)
        {
            _rutina = StartCoroutine(RevelarSiguiente());
        }
        else if (_pagina + 1 < _actual.paginas.Length)
        {
            _pagina++;
            _rutina = StartCoroutine(PrepararPagina(primera: false));
        }
        else
        {
            _rutina = StartCoroutine(Cerrar());
        }
    }

    static bool ConfirmarPresionado()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) return true;
        foreach (Gamepad gp in Gamepad.all)
            if (gp.buttonSouth.wasPressedThisFrame) return true;
        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SECUENCIAS
    // ═══════════════════════════════════════════════════════════════════════

    IEnumerator PrepararPagina(bool primera)
    {
        _ocupado = true;

        // Si venimos de otra página, se funde a negro la anterior
        if (!primera) yield return FundirGrupo(1f, 0f, duracionCambioPagina);
        else _grupoPagina.alpha = 0f;

        ConstruirVinetas(_actual.paginas[_pagina]);
        _vinetasReveladas = 0;

        yield return FundirGrupo(0f, 1f, duracionCambioPagina);

        // La primera viñeta aparece sola
        yield return RevelarSiguiente();
    }

    IEnumerator RevelarSiguiente()
    {
        _ocupado = true;

        if (_vinetasReveladas < _vinetasActivas)
        {
            RawImage v = _vinetasUI[_vinetasReveladas];
            _vinetasReveladas++;

            RectTransform rt = v.rectTransform;
            float t = 0f;
            while (t < duracionRevelado)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duracionRevelado));
                Color c = v.color; c.a = k; v.color = c;
                rt.localScale = Vector3.one * Mathf.Lerp(escalaInicialVineta, 1f, k);
                yield return null;
            }
            Color cf = v.color; cf.a = 1f; v.color = cf;
            rt.localScale = Vector3.one;
        }
        else
        {
            // Página sin viñetas definidas: se muestra completa
            _vinetasReveladas++;
        }

        _ignorarHasta = Time.unscaledTime + 0.05f;
        _ocupado = false;
        _rutina = null;
    }

    IEnumerator Cerrar()
    {
        _ocupado = true;
        yield return FundirGrupo(1f, 0f, duracionCambioPagina);

        Action callback = _alTerminar;
        Momento momento = _momentoActual;

        _actual = null;
        _alTerminar = null;
        _rutina = null;
        Activo = false;

        if (momento == Momento.Inicio)
        {
            // Historieta de inicio: el nivel arranca (o lo congela el tutorial, si lo hay).
            // Negro de golpe debajo del panel para que no haya parpadeo, y luego se aclara.
            Time.timeScale = 1f;
            if (FundidoPantalla.Instance != null) FundidoPantalla.Instance.NegroInstantaneo();
            panelComic.SetActive(false);
            callback?.Invoke();
            if (FundidoPantalla.Instance != null) FundidoPantalla.Instance.Aclarar();
        }
        else
        {
            // Historieta de final: el que la pidió decide qué sigue (resultados).
            // La música baja aquí porque después no hay tutorial que la continúe.
            if (AudioManager.Instance != null) AudioManager.Instance.DetenerMusicaHistorieta(true);
            if (FundidoPantalla.Instance != null) FundidoPantalla.Instance.NegroInstantaneo();
            panelComic.SetActive(false);
            callback?.Invoke();
        }
    }

    IEnumerator FundirGrupo(float desde, float hasta, float duracion)
    {
        float t = 0f;
        _grupoPagina.alpha = desde;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            _grupoPagina.alpha = Mathf.Lerp(desde, hasta, Mathf.Clamp01(t / duracion));
            yield return null;
        }
        _grupoPagina.alpha = hasta;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCCIÓN DE LA PÁGINA
    // ═══════════════════════════════════════════════════════════════════════

    void ConstruirVinetas(PaginaComic pagina)
    {
        // Ocultar las que sobren de la página anterior
        foreach (RawImage r in _vinetasUI) r.gameObject.SetActive(false);
        _vinetasActivas = 0;

        if (pagina == null || pagina.imagen == null) return;

        Sprite sp = pagina.imagen;
        Texture2D tex = sp.texture;
        Rect tr = sp.textureRect;                 // región del sprite dentro de la textura
        float W = tr.width, H = tr.height;

        _fitter.aspectRatio = W / H;

        Vector4[] lista = pagina.vinetas;
        int n = lista != null ? lista.Length : 0;

        // Sin viñetas definidas: una sola "viñeta" con toda la página
        if (n == 0)
        {
            lista = new[] { new Vector4(0, 0, W, H) };
            n = 1;
        }

        for (int i = 0; i < n; i++)
        {
            RawImage ui = ObtenerVineta(i);
            Vector4 v = lista[i];

            ui.texture = tex;
            ui.uvRect = new Rect(
                (tr.x + v.x) / tex.width,
                (tr.y + (H - (v.y + v.w))) / tex.height,
                v.z / tex.width,
                v.w / tex.height);

            RectTransform rt = ui.rectTransform;
            rt.anchorMin = new Vector2(v.x / W, 1f - (v.y + v.w) / H);
            rt.anchorMax = new Vector2((v.x + v.z) / W, 1f - v.y / H);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one * escalaInicialVineta;

            ui.color = new Color(1f, 1f, 1f, 0f);
            ui.gameObject.SetActive(true);
        }

        _vinetasActivas = n;
    }

    private int _vinetasActivas;

    RawImage ObtenerVineta(int indice)
    {
        while (_vinetasUI.Count <= indice)
        {
            GameObject go = new GameObject("Vineta_" + _vinetasUI.Count, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(contenedorPagina, false);
            RawImage r = go.GetComponent<RawImage>();
            r.raycastTarget = false;
            _vinetasUI.Add(r);
        }
        return _vinetasUI[indice];
    }

    Comic Buscar(int nivel, Momento momento)
    {
        foreach (Comic c in historietas)
            if (c.nivel == nivel && c.momento == momento && c.paginas != null && c.paginas.Length > 0)
                return c;
        return null;
    }
}