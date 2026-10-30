using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

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
    public GameObject botonSalir;

    private bool _pausado = false;
    private List<GameObject> _botones = new List<GameObject>();
    private int _indice = 0;
    private float _timerNav = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (botonReanudar != null) _botones.Add(botonReanudar);
        if (botonReiniciar != null) _botones.Add(botonReiniciar);
        if (botonSalir != null) _botones.Add(botonSalir);
    }

    void Start()
    {
        if (canvasPausa != null) canvasPausa.SetActive(false);
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.EstaJugando) return;

        if (_pausado) ActualizarNavegacion();

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

        _indice = 0;
        ActualizarSeleccion();
    }

    public void Reanudar()
    {
        _pausado = false;
        Time.timeScale = 1f;
        if (AudioManager.Instance != null) AudioManager.Instance.ReanudarMusica();
        if (canvasPausa != null) canvasPausa.SetActive(false);
    }

    /// <summary>Botón "Reiniciar Nivel" de la pausa.</summary>
    public void ReiniciarNivel()
    {
        _pausado = false;
        if (canvasPausa != null) canvasPausa.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.Reintentar();
    }

    /// <summary>Botón "Salir al Menú" de la pausa.</summary>
    public void SalirAlMenu()
    {
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