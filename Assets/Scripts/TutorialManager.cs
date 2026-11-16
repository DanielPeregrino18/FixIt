using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Muestra imágenes de tutorial al iniciar ciertos niveles. El juego queda congelado
/// (timeScale 0) mientras se ven. Cualquier jugador avanza / cierra con E (teclado) o
/// botón Sur / A (control). Se muestran cada vez que se entra al nivel (también al reiniciar).
///
/// Estructura en la escena: un panel a pantalla completa (hijo del Canvas del juego, al final
/// de la jerarquía para quedar encima de todo) con la imagen "Hoja" de fondo y, encima,
/// el Image que cambia de sprite por cada página del tutorial.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    /// <summary>true mientras hay un tutorial en pantalla.</summary>
    public static bool Activo { get; private set; }

    [Serializable]
    public class TutorialNivel
    {
        [Tooltip("Número de nivel (no el día del calendario)")]
        public int nivel;
        [Tooltip("Imágenes en orden; cada A pasa a la siguiente")]
        public Sprite[] imagenes;
        [Tooltip("Título de cada imagen, en el mismo orden (si falta alguno, no se muestra título)")]
        public string[] titulos;

        public TutorialNivel(int nivel) { this.nivel = nivel; }
    }

    [Header("UI")]
    [Tooltip("Panel completo (contiene la hoja de fondo y la imagen del tutorial)")]
    public GameObject panelTutorial;
    [Tooltip("Image que cambia de sprite con cada página (va delante de la hoja)")]
    public Image imagenTutorial;
    [Tooltip("Texto (TextMeshPro) con el título de la página actual")]
    public TMP_Text textoTitulo;

    [Header("Tutoriales por nivel")]
    public List<TutorialNivel> tutoriales = new List<TutorialNivel>
    {
        new TutorialNivel(1),   // día 1  (2 imágenes)
        new TutorialNivel(3),   // día 3  (1 imagen)
        new TutorialNivel(6),   // día 8  (1 imagen)
        new TutorialNivel(11),  // día 15 (1 imagen)
    };

    [Tooltip("Segundos tras aparecer en los que se ignora la A (evita cerrarlo con la misma pulsación que inició el nivel)")]
    public float retrasoEntrada = 0.3f;

    private TutorialNivel _actual;
    private int _pagina;
    private float _ignorarHasta;
    private bool _cerrarPendiente;
    private bool _avanzarPendiente;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Activo = false;
    }

    void Start()
    {
        if (panelTutorial != null) panelTutorial.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) { Instance = null; Activo = false; }
    }

    /// <summary>Muestra el tutorial del nivel si existe. Devuelve true si se mostró.</summary>
    public bool MostrarParaNivel(int nivel)
    {
        Ocultar();

        foreach (TutorialNivel t in tutoriales)
        {
            if (t.nivel != nivel || t.imagenes == null || t.imagenes.Length == 0) continue;

            _actual = t;
            _pagina = 0;
            _cerrarPendiente = false;
            _avanzarPendiente = false;
            _ignorarHasta = Time.unscaledTime + retrasoEntrada;

            MostrarPagina();
            if (panelTutorial != null) panelTutorial.SetActive(true);

            Activo = true;
            Time.timeScale = 0f;

            // Música de historietas/tutoriales (si ya sonaba por la historieta, sigue sin cortarse)
            if (AudioManager.Instance != null) AudioManager.Instance.IniciarMusicaHistorieta();
            return true;
        }
        return false;
    }

    /// <summary>Oculta el tutorial sin reanudar nada (lo usa el GameManager al cambiar de nivel).</summary>
    public void Ocultar()
    {
        Activo = false;
        _actual = null;
        _cerrarPendiente = false;
        _avanzarPendiente = false;
        if (panelTutorial != null) panelTutorial.SetActive(false);
    }

    void Update()
    {
        if (!Activo || _actual == null) return;
        if (Time.unscaledTime < _ignorarHasta) return;
        if (_avanzarPendiente || _cerrarPendiente) return;

        if (!ConfirmarPresionado()) return;

        AudioManager.Confirmar();

        if (_pagina + 1 < _actual.imagenes.Length)
        {
            _pagina++;
            MostrarPagina();
            _ignorarHasta = Time.unscaledTime + 0.1f;
        }
        else
        {
            // Se cierra en LateUpdate para que el resto de scripts (que ya corrieron este
            // frame con timeScale 0) no interpreten esta misma A como una interacción.
            _cerrarPendiente = true;
        }
    }

    void LateUpdate()
    {
        if (!_cerrarPendiente) return;
        _cerrarPendiente = false;

        Activo = false;
        _actual = null;
        if (panelTutorial != null) panelTutorial.SetActive(false);

        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.ArrancarMusicaNivel();
    }

    void MostrarPagina()
    {
        if (_actual == null) return;

        if (imagenTutorial != null)
        {
            imagenTutorial.sprite = _actual.imagenes[_pagina];
            imagenTutorial.preserveAspect = true;
        }

        if (textoTitulo != null)
        {
            string titulo = (_actual.titulos != null && _pagina < _actual.titulos.Length)
                ? _actual.titulos[_pagina] : "";
            textoTitulo.text = titulo;
            textoTitulo.gameObject.SetActive(!string.IsNullOrEmpty(titulo));
        }
    }

    static bool ConfirmarPresionado()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) return true;
        foreach (Gamepad gp in Gamepad.all)
            if (gp.buttonSouth.wasPressedThisFrame) return true;
        return false;
    }
}