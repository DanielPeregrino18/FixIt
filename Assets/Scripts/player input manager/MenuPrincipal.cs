using UnityEngine;

/// <summary>
/// Botones del menú principal.
/// </summary>
public class MenuPrincipal : MonoBehaviour
{
    /// <summary>Botón JUGAR — lleva al lobby y habilita que los jugadores se unan.</summary>
    public void Jugar()
    {
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

    /// <summary>Botón CONFIGURACIÓN — placeholder por ahora.</summary>
    public void Configuracion()
    {
        Debug.Log("MenuPrincipal: Configuración aún no implementada.");
    }
}
