using UnityEngine;
using System.Collections;

/// <summary>
/// Implementación de IProgressStore que guarda y carga datos desde el backend de Render.
/// Con mapeo automático de niveles a mundos (1-5=Mundo1, 6-10=Mundo2, etc.)
/// </summary>
public class RemoteProgressStore : IProgressStore
{
    private readonly string _userId;
    private UserProgress _cachedProgress;
    private bool _isInitialized = false;
    private readonly MonoBehaviour _coroutineRunner;

    public RemoteProgressStore(string userId, MonoBehaviour coroutineRunner)
    {
        _userId = userId;
        _coroutineRunner = coroutineRunner;
        _coroutineRunner.StartCoroutine(CargarProgresoInicialCoroutine());
    }

    private IEnumerator CargarProgresoInicialCoroutine()
    {
        yield return null;

        int intentos = 0;
        while (FixItNetworkManager.Instance == null && intentos < 10)
        {
            yield return new WaitForSeconds(0.1f);
            intentos++;
        }

        if (FixItNetworkManager.Instance == null)
        {
            Debug.LogError("❌ FixItNetworkManager no se encontró.");
            _isInitialized = true;
            yield break;
        }

        Debug.Log($"📡 Solicitando progreso para usuario: {_userId}");

        FixItNetworkManager.Instance.GetProgress(
            userId: _userId,
            onSuccess: (progress) =>
            {
                _cachedProgress = progress;
                _isInitialized = true;
                Debug.Log($"✅ Progreso remoto cargado: {progress.worlds.Length} mundos");
            },
            onError: (error) =>
            {
                Debug.LogWarning($"⚠️ No se pudo cargar progreso remoto (¿Usuario nuevo?): {error}");
                _cachedProgress = new UserProgress { userId = _userId, worlds = new WorldProgressEntry[0] };
                _isInitialized = true;
            }
        );
    }

    /// <summary>
    /// Calcula automáticamente el mundo basado en el nivel.
    /// Nivel 1-5 = world_1, Nivel 6-10 = world_2, Nivel 11-15 = world_3, etc.
    /// </summary>
    private string ObtenerWorldIdParaNivel(int nivel)
    {
        int mundoNumero = ((nivel - 1) / 5) + 1;
        return $"world_{mundoNumero}";
    }

    /// <summary>
    /// Obtiene el ID del nivel (nivel_1, nivel_2, etc.)
    /// </summary>
    private string ObtenerLevelIdParaNivel(int nivel)
    {
        return $"nivel_{nivel}";
    }

    public int ObtenerEstrellas(int nivel)
    {
        if (!_isInitialized || _cachedProgress == null) return 0;

        string worldId = ObtenerWorldIdParaNivel(nivel);
        string levelId = ObtenerLevelIdParaNivel(nivel);

        WorldProgress world = _cachedProgress.GetWorld(worldId);

        if (world != null)
        {
            LevelProgress level = world.GetLevel(levelId);
            if (level != null)
            {
                return level.bestStars;
            }
        }
        return 0;
    }

    public void GuardarEstrellas(int nivel, int estrellas)
    {
        string worldId = ObtenerWorldIdParaNivel(nivel);
        string levelId = ObtenerLevelIdParaNivel(nivel);

        Debug.Log($"💾 Guardando progreso: {worldId}/{levelId} con {estrellas} estrellas");

        FixItNetworkManager.Instance.UpdateProgress(
            userId: _userId,
            worldId: worldId,
            levelId: levelId,
            score: 0,
            starsEarned: estrellas,
            onSuccess: (updatedProgress) =>
            {
                _cachedProgress = updatedProgress;
                Debug.Log($"✅ Progreso remoto guardado correctamente");
            },
            onError: (error) =>
            {
                Debug.LogError($"❌ Error guardando progreso remoto: {error}");
            }
        );
    }

    public int ObtenerNivelDesbloqueado()
    {
        if (!_isInitialized) return 1;

        int maxDesbloqueado = 1;

        for (int i = 1; i <= ProgressManager.TOTAL_NIVELES; i++)
        {
            if (ObtenerEstrellas(i) > 0)
            {
                maxDesbloqueado = i + 1;
            }
            else
            {
                break;
            }
        }

        return Mathf.Min(maxDesbloqueado, ProgressManager.TOTAL_NIVELES);
    }

    public void GuardarCambios()
    {
        throw new System.NotImplementedException();
    }
}