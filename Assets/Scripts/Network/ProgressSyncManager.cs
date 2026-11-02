using UnityEngine;

public class ProgressSyncManager : MonoBehaviour
{
    public static ProgressSyncManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Carga el progreso del jugador actual
    /// </summary>
    public void LoadCurrentPlayerProgress(System.Action<UserProgress> onComplete)
    {
        string userId = PlayerDataManager.Instance?.CurrentUserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("⚠️ No hay usuario logueado");
            onComplete?.Invoke(null);
            return;
        }

        FixItNetworkManager.Instance.GetProgress(
            userId: userId,
            onSuccess: (progress) =>
            {
                Debug.Log($"✅ Progreso cargado: {progress.worlds.Length} mundos");
                PlayerDataManager.Instance.SetProgress(progress);
                onComplete?.Invoke(progress);
            },
            onError: (error) =>
            {
                Debug.LogError($"❌ Error cargando progreso: {error}");
                onComplete?.Invoke(null);
            }
        );
    }

    /// <summary>
    /// Obtiene el mejor score de un nivel específico
    /// </summary>
    public int GetBestScore(string worldId, string levelId)
    {
        UserProgress progress = PlayerDataManager.Instance?.CurrentProgress;
        if (progress == null) return 0;

        WorldProgress world = progress.GetWorld(worldId);
        if (world == null) return 0;

        LevelProgress level = world.GetLevel(levelId);
        if (level == null) return 0;

        return level.bestScore;
    }

    /// <summary>
    /// Obtiene las mejores estrellas de un nivel
    /// </summary>
    public int GetBestStars(string worldId, string levelId)
    {
        UserProgress progress = PlayerDataManager.Instance?.CurrentProgress;
        if (progress == null) return 0;

        WorldProgress world = progress.GetWorld(worldId);
        if (world == null) return 0;

        LevelProgress level = world.GetLevel(levelId);
        if (level == null) return 0;

        return level.bestStars;
    }

    /// <summary>
    /// Verifica si un nivel fue completado
    /// </summary>
    public bool IsLevelCompleted(string worldId, string levelId)
    {
        return GetBestStars(worldId, levelId) > 0;
    }
}