using UnityEngine;
using UnityEngine.InputSystem;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Canvas de cada pantalla")]
    public GameObject canvasMenu;
    public GameObject canvasLobby;
    public GameObject canvasSeleccion;
    public GameObject canvasJuego;

    /// <summary>True mientras la pantalla de selección de personaje (lobby) está visible.</summary>
    public bool EnLobby => canvasLobby != null && canvasLobby.activeInHierarchy;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        MostrarMenu();
    }

    public void MostrarMenu()
    {
        Activar(canvasMenu);
        // En el menú nadie puede unirse
        if (PlayerInputManager.instance != null)
            PlayerInputManager.instance.DisableJoining();
    }

    public void MostrarLobby()
    {
        Activar(canvasLobby);
        // Solo en el lobby se puede unir un jugador
        if (PlayerInputManager.instance != null)
            PlayerInputManager.instance.EnableJoining();
    }

    public void MostrarSeleccionNivel()
    {
        Activar(canvasSeleccion);
        // Ya no se pueden unir más jugadores
        if (PlayerInputManager.instance != null)
            PlayerInputManager.instance.DisableJoining();
    }

    public void MostrarJuego()
    {
        Activar(canvasJuego);
    }

    void Activar(GameObject canvasActivo)
    {
        if (canvasMenu != null) canvasMenu.SetActive(canvasActivo == canvasMenu);
        if (canvasLobby != null) canvasLobby.SetActive(canvasActivo == canvasLobby);
        if (canvasSeleccion != null) canvasSeleccion.SetActive(canvasActivo == canvasSeleccion);
        if (canvasJuego != null) canvasJuego.SetActive(canvasActivo == canvasJuego);
    }
}