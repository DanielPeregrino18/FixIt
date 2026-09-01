using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Genera dinámicamente el calendario estilo "mes" (5 semanas x 7 días).
/// El número mostrado es el DÍA DEL MES, no el número de nivel:
///   - Días 1-26 en Lunes-Viernes → los 20 niveles reales, en orden.
///   - Sábados y domingos → celdas de descanso, siempre bloqueadas.
///   - Días 29-31 (última fila) → relleno decorativo, siempre bloqueadas.
///
/// Navegación: cualquier teclado (flechas) o control (D-pad/joystick) mueve
/// un cursor entre los niveles DESBLOQUEADOS. E / botón Sur confirma.
/// </summary>
public class CalendarioNiveles : MonoBehaviour
{
    [Header("Prefabs de celda")]
    public GameObject prefabDiaNivel;
    public GameObject prefabDiaDescanso;

    [Header("Contenedor de la grilla")]
    public Transform contenedorGrilla;

    [Header("UI de progreso general")]
    public TextMeshProUGUI textoProgreso;
    public Image barraProgreso;

    [Header("Referencia al selector")]
    public LevelSelector levelSelector;

    [Header("Configuración de navegación")]
    [Tooltip("Cuántos niveles jugables hay por fila (Lunes-Viernes = 5)")]
    public int diasPorFila = 5;
    [Tooltip("Escala de la celda con el cursor encima")]
    public float escalaFoco = 1.1f;
    [Tooltip("Segundos entre movimientos al mantener el joystick (no aplica a botones/flechas)")]
    public float delayJoystick = 0.2f;

    private const int TOTAL_DIAS_MES = 31;

    // ── Info de cada celda navegable (solo niveles desbloqueados) ─────────────
    private class CeldaNivelInfo
    {
        public GameObject celda;
        public int        numeroNivel;
        public bool       completado;
        public Outline    outline;
        public Transform  iconoJugar;
        public Transform  iconoCompletado;
    }

    private List<CeldaNivelInfo> _celdasNavegables = new List<CeldaNivelInfo>();
    private int   _cursorIndex   = 0;
    private float _timerJoystick = 0f;

    // ─────────────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        GenerarCalendario();
    }

    void Update()
    {
        if (_celdasNavegables.Count == 0) return;

        _timerJoystick -= Time.deltaTime;
        Vector2Int dir = Vector2Int.zero;
        bool moverse = false;

        // Teclado — un solo paso por pulsación
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame)  { dir = new Vector2Int(-1, 0); moverse = true; }
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame) { dir = new Vector2Int(1, 0);   moverse = true; }
            if (Keyboard.current.upArrowKey.wasPressedThisFrame)    { dir = new Vector2Int(0, -1);  moverse = true; }
            if (Keyboard.current.downArrowKey.wasPressedThisFrame)  { dir = new Vector2Int(0, 1);   moverse = true; }
        }

        // Cualquier control conectado — D-pad (un paso) o joystick (con delay)
        if (!moverse)
        {
            foreach (Gamepad gp in Gamepad.all)
            {
                if (gp.dpad.left.wasPressedThisFrame)  { dir = new Vector2Int(-1, 0); moverse = true; break; }
                if (gp.dpad.right.wasPressedThisFrame) { dir = new Vector2Int(1, 0);   moverse = true; break; }
                if (gp.dpad.up.wasPressedThisFrame)    { dir = new Vector2Int(0, -1);  moverse = true; break; }
                if (gp.dpad.down.wasPressedThisFrame)  { dir = new Vector2Int(0, 1);   moverse = true; break; }

                if (_timerJoystick <= 0f)
                {
                    Vector2 stick = gp.leftStick.ReadValue();
                    if      (stick.x < -0.5f) { dir = new Vector2Int(-1, 0); moverse = true; }
                    else if (stick.x >  0.5f) { dir = new Vector2Int(1, 0);   moverse = true; }
                    else if (stick.y >  0.5f) { dir = new Vector2Int(0, -1);  moverse = true; }
                    else if (stick.y < -0.5f) { dir = new Vector2Int(0, 1);   moverse = true; }
                }
                if (moverse) break;
            }
        }

        if (moverse)
        {
            MoverCursor(dir);
            _timerJoystick = delayJoystick;
        }

        // Confirmar — E (teclado) o botón Sur (A) de cualquier control
        bool confirmar = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        if (!confirmar)
        {
            foreach (Gamepad gp in Gamepad.all)
            {
                if (gp.buttonSouth.wasPressedThisFrame) { confirmar = true; break; }
            }
        }

        if (confirmar) ConfirmarSeleccion();
    }

    // ── Construcción del calendario ────────────────────────────────────────────

    void GenerarCalendario()
    {
        foreach (Transform hijo in contenedorGrilla)
            Destroy(hijo.gameObject);
        _celdasNavegables.Clear();

        int nivelDesbloqueado = ProgressManager.Instance != null
            ? ProgressManager.Instance.NivelDesbloqueado()
            : 1;

        int nivelActual = 1;

        for (int diaDelMes = 1; diaDelMes <= TOTAL_DIAS_MES; diaDelMes++)
        {
            int diaSemana = (diaDelMes - 1) % 7;
            bool esFinDeSemana = diaSemana >= 5;

            if (esFinDeSemana)
            {
                CrearCeldaDescanso(diaDelMes);
                continue;
            }

            if (nivelActual <= ProgressManager.TOTAL_NIVELES)
            {
                CrearCeldaNivel(diaDelMes, nivelActual, nivelDesbloqueado);
                nivelActual++;
            }
            else
            {
                CrearCeldaDescanso(diaDelMes);
            }
        }

        // Cursor arranca en el nivel más avanzado desbloqueado (el "siguiente" de siempre)
        _cursorIndex = _celdasNavegables.Count > 0 ? _celdasNavegables.Count - 1 : 0;
        ActualizarVisualCursor();

        ActualizarProgresoGeneral();
    }

    void CrearCeldaNivel(int diaDelMes, int numeroNivel, int nivelDesbloqueado)
    {
        GameObject celda = Instantiate(prefabDiaNivel, contenedorGrilla);

        TextMeshProUGUI textoNumero = celda.transform.Find("TextoNumero")?.GetComponent<TextMeshProUGUI>();
        if (textoNumero != null) textoNumero.text = diaDelMes.ToString();

        Transform contenedorEstrellas = celda.transform.Find("Estrellas");
        Transform iconoCandado        = celda.transform.Find("IconoCandado");
        Transform iconoJugar          = celda.transform.Find("IconoJugar");
        Transform iconoCompletado     = celda.transform.Find("IconoCompletado");

        int estrellas = ProgressManager.Instance != null
            ? ProgressManager.Instance.ObtenerEstrellas(numeroNivel)
            : 0;

        bool desbloqueado = numeroNivel <= nivelDesbloqueado;
        bool completado    = estrellas > 0;

        Button boton = celda.GetComponent<Button>();

        // IconoJugar ya NO es un estado permanente — solo lo enciende el cursor
        // (ActualizarVisualCursor) sobre la celda donde el jugador está parado.
        if (iconoJugar != null) iconoJugar.gameObject.SetActive(false);

        if (!desbloqueado)
        {
            if (iconoCandado    != null) iconoCandado.gameObject.SetActive(true);
            if (iconoCompletado != null) iconoCompletado.gameObject.SetActive(false);
            if (contenedorEstrellas != null) contenedorEstrellas.gameObject.SetActive(false);
            if (boton != null) boton.interactable = false;
        }
        else if (completado)
        {
            if (iconoCandado    != null) iconoCandado.gameObject.SetActive(false);
            if (iconoCompletado != null) iconoCompletado.gameObject.SetActive(true); // se oculta luego si queda con el foco
            MostrarEstrellas(contenedorEstrellas, estrellas);
            if (boton != null) boton.interactable = true;
        }
        else
        {
            // Desbloqueado, sin jugar todavía: vacío por defecto (nada al centro)
            // hasta que el cursor pase por aquí.
            if (iconoCandado    != null) iconoCandado.gameObject.SetActive(false);
            if (iconoCompletado != null) iconoCompletado.gameObject.SetActive(false);
            if (contenedorEstrellas != null) contenedorEstrellas.gameObject.SetActive(false);
            if (boton != null) boton.interactable = true;
        }

        Outline outline = celda.GetComponent<Outline>();
        if (outline != null) outline.enabled = false; // controlado por el cursor, no por creación

        if (boton != null)
        {
            int nivelCapturado = numeroNivel;
            boton.onClick.RemoveAllListeners();
            boton.onClick.AddListener(() => SeleccionarNivel(nivelCapturado));
        }

        // Solo los niveles desbloqueados entran a la lista navegable, en orden de lectura
        if (desbloqueado)
        {
            _celdasNavegables.Add(new CeldaNivelInfo
            {
                celda           = celda,
                numeroNivel     = numeroNivel,
                completado      = completado,
                outline         = outline,
                iconoJugar      = iconoJugar,
                iconoCompletado = iconoCompletado
            });
        }
    }

    void CrearCeldaDescanso(int diaDelMes)
    {
        if (prefabDiaDescanso == null) return;

        GameObject celda = Instantiate(prefabDiaDescanso, contenedorGrilla);
        TextMeshProUGUI textoNumero = celda.transform.Find("TextoNumero")?.GetComponent<TextMeshProUGUI>();
        if (textoNumero != null) textoNumero.text = diaDelMes.ToString();
    }

    // ── Navegación con cursor ──────────────────────────────────────────────────

    void MoverCursor(Vector2Int dir)
    {
        int nuevoIndex = _cursorIndex;

        if (dir.x != 0)      nuevoIndex = _cursorIndex + dir.x;
        else if (dir.y != 0) nuevoIndex = _cursorIndex + dir.y * diasPorFila;

        nuevoIndex = Mathf.Clamp(nuevoIndex, 0, _celdasNavegables.Count - 1);

        if (nuevoIndex != _cursorIndex)
        {
            _cursorIndex = nuevoIndex;
            ActualizarVisualCursor();
        }
    }

    void ActualizarVisualCursor()
    {
        for (int i = 0; i < _celdasNavegables.Count; i++)
        {
            bool esFoco = i == _cursorIndex;
            CeldaNivelInfo info = _celdasNavegables[i];

            if (info.outline    != null) info.outline.enabled = esFoco;
            if (info.iconoJugar != null) info.iconoJugar.gameObject.SetActive(esFoco);

            // Si el día ya estaba completado, su "X" se oculta mientras el cursor
            // esté encima (para dejar ver el ícono de jugar) y reaparece al salir.
            if (info.iconoCompletado != null)
                info.iconoCompletado.gameObject.SetActive(info.completado && !esFoco);

            info.celda.transform.localScale = esFoco ? Vector3.one * escalaFoco : Vector3.one;
        }
    }

    void ConfirmarSeleccion()
    {
        if (_cursorIndex < 0 || _cursorIndex >= _celdasNavegables.Count) return;
        SeleccionarNivel(_celdasNavegables[_cursorIndex].numeroNivel);
    }

    // ── Utilidades ─────────────────────────────────────────────────────────────

    void MostrarEstrellas(Transform contenedor, int cantidad)
    {
        if (contenedor == null) return;
        contenedor.gameObject.SetActive(true);

        for (int i = 0; i < contenedor.childCount; i++)
        {
            Transform estrella = contenedor.GetChild(i);
            Image img = estrella.GetComponent<Image>();
            if (img == null) continue;
            img.color = i < cantidad ? Color.white : new Color(1f, 1f, 1f, 0.25f);
        }
    }

    void SeleccionarNivel(int numeroNivel)
    {
        if (levelSelector != null)
            levelSelector.SeleccionarNivel(numeroNivel - 1);
    }

    void ActualizarProgresoGeneral()
    {
        if (ProgressManager.Instance == null) return;

        int completados = ProgressManager.Instance.NivelesCompletados();

        if (textoProgreso != null)
            textoProgreso.text = $"PROGRESO: {completados} / {ProgressManager.TOTAL_NIVELES} NIVELES COMPLETADOS";

        if (barraProgreso != null)
            barraProgreso.fillAmount = (float)completados / ProgressManager.TOTAL_NIVELES;
    }
}