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

    // ── Estado interno ────────────────────────────────────────────────────────
    private int _indice = 0;
    private float _timerNavStick = 0f;
    private float _timerEntrada = 0f;
    private bool _hoverConfigurado = false;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        CrearBotonSupervivencia();
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