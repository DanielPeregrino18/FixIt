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
    private RemoteProgressStore _remoto;

    /// <summary>Avisa cuando la nube trajo progreso nuevo, para que la UI se actualice.</summary>
    public event System.Action ProgresoActualizado;

    public const int TOTAL_NIVELES = 20;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Siempre guarda en local; solo sincroniza con la nube si hay una sesión iniciada.
        // La cuenta se asigna en Start, cuando PlayerDataManager ya restauró la sesión guardada.
        _remoto = new RemoteProgressStore(null, this);
        _remoto.ProgresoCambiado += () => ProgresoActualizado?.Invoke();
        _store = _remoto;
    }

    void Start()
    {
        if (PlayerDataManager.Instance == null)
        {
            Debug.LogWarning("ProgressManager: no hay PlayerDataManager en la escena; el progreso será solo local.");
            return;
        }

        PlayerDataManager.Instance.UsuarioCambiado += AsignarUsuarioActual;
        AsignarUsuarioActual();
    }

    void OnDestroy()
    {
        if (PlayerDataManager.Instance != null)
            PlayerDataManager.Instance.UsuarioCambiado -= AsignarUsuarioActual;
    }

    void AsignarUsuarioActual()
    {
        _remoto?.CambiarUsuario(PlayerDataManager.Instance?.CurrentUserId);
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
