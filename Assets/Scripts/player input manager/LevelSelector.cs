using UnityEngine;

/// <summary>
/// Recibe la selección de nivel (desde el calendario) e inicia el juego.
/// </summary>
public class LevelSelector : MonoBehaviour
{
    [Header("HUDs de jugadores (en orden)")]
    public PlayerHUD[] hudsJugadores;

    /// <param name="nivelIndex">Índice base 0 (nivel 1 = índice 0).</param>
    public void SeleccionarNivel(int nivelIndex)
    {
        int numeroNivel = nivelIndex + 1;

        // Primero mostrar el Canvas_Juego para que los objetos estén activos
        GameFlowManager.Instance.MostrarJuego();

        // Activar scripts de juego y vincular HUDs
        PlayerJoinHandler[] jugadores = FindObjectsByType<PlayerJoinHandler>(FindObjectsSortMode.None);
        foreach (PlayerJoinHandler j in jugadores)
            j.ActivarModoJuego(true, hudsJugadores);

        // Cargar e iniciar el nivel — WaveManager arranca el GameManager también
        NivelData datos = NivelLoader.Instance.CargarNivel(numeroNivel);
        if (datos != null)
            WaveManager.Instance.IniciarNivel(datos);
        else
            Debug.LogError($"LevelSelector: No se pudo cargar el nivel {numeroNivel}.");
    }
}