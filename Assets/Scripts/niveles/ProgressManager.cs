using UnityEngine;

/// <summary>
/// Punto único de acceso al progreso del jugador. El resto del juego
/// llama a ProgressManager.Instance, nunca a IProgressStore directamente.
/// Aquí es donde se decide qué implementación usar (local o remota).
/// </summary>
public class ProgressManager : MonoBehaviour
{
    public static ProgressManager Instance { get; private set; }

    private IProgressStore _store;

    public const int TOTAL_NIVELES = 20;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        string testUserId = "6ae69f4cf3d30eec400e04e5";
        
        _store = new RemoteProgressStore(testUserId, this); // ← Usa el remoto
    }

    public int ObtenerEstrellas(int nivel) => _store.ObtenerEstrellas(nivel);

    public void GuardarResultado(int nivel, int estrellas) => _store.GuardarEstrellas(nivel, estrellas);

    public int NivelDesbloqueado() => _store.ObtenerNivelDesbloqueado();

    public bool EstaDesbloqueado(int nivel) => nivel <= NivelDesbloqueado();

    /// <summary>Total de niveles completados (con al menos 1 estrella).</summary>
    public int NivelesCompletados()
    {
        int completados = 0;
        for (int i = 1; i <= TOTAL_NIVELES; i++)
            if (_store.ObtenerEstrellas(i) > 0) completados++;
        return completados;
    }

    /// <summary>Reinicia todo el progreso (útil para pruebas o "borrar partida").</summary>
    public void ReiniciarProgreso()
    {
        for (int i = 1; i <= TOTAL_NIVELES; i++)
            PlayerPrefs.DeleteKey("FixIT_Estrellas_Nivel_" + i);
        PlayerPrefs.DeleteKey("FixIT_NivelDesbloqueado");
        PlayerPrefs.Save();
        Debug.Log("ProgressManager: Progreso reiniciado.");
    }
}
