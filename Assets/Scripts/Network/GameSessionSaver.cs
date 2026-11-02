using UnityEngine;
using System.Collections.Generic;

public class GameSessionSaver : MonoBehaviour
{
    public static GameSessionSaver Instance { get; private set; }

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
    /// Guarda una sesión de juego completada
    /// </summary>
    public void SaveSession(string levelId, string worldId, int score, int stars,
                           int ticketsCompleted, int ticketsFailed, int durationSeconds)
    {
        string userId = PlayerDataManager.Instance?.CurrentUserId;

        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("⚠️ No hay usuario logueado, no se puede guardar sesión");
            return;
        }

        var session = new GameSession
        {
            levelId = levelId,
            worldId = worldId,
            players = new List<string> { userId },
            score = score,
            starsEarned = stars,
            ticketsCompleted = ticketsCompleted,
            ticketsFailed = ticketsFailed,
            durationSeconds = durationSeconds,
            equipment = "Laptop",
            repairType = "Otros",
            completed = true
        };

        Debug.Log($"💾 Guardando sesión: {levelId} - Score: {score}, Stars: {stars}");

        FixItNetworkManager.Instance.PostSession(
            session: session,
            onSuccess: (savedSession) =>
            {
                Debug.Log($"✅ Sesión guardada: {savedSession._id}");

                // Actualizar progreso local
                PlayerDataManager.Instance.UpdateProgressLocally(
                    worldId, levelId, score, stars);

                // Sincronizar con el servidor
                SyncProgress(worldId, levelId, score, stars);
            },
            onError: (error) =>
            {
                Debug.LogError($"❌ Error guardando sesión: {error}");
            }
        );
    }

    private void SyncProgress(string worldId, string levelId, int score, int stars)
    {
        string userId = PlayerDataManager.Instance.CurrentUserId;

        FixItNetworkManager.Instance.UpdateProgress(
            userId: userId,
            worldId: worldId,
            levelId: levelId,
            score: score,
            starsEarned: stars,
            onSuccess: (progress) =>
            {
                Debug.Log($"✅ Progreso sincronizado con servidor");
                PlayerDataManager.Instance.SetProgress(progress);
            },
            onError: (error) =>
            {
                Debug.LogError($"❌ Error sincronizando progreso: {error}");
            }
        );
    }

    /// <summary>
    /// Actualiza estadísticas del usuario (llamar al final de cada sesión)
    /// </summary>
    public void UpdateUserStats(int gamesPlayed, int ticketsCompleted, int ticketsFailed,
                                int playTimeSeconds, int score, int stars)
    {
        string userId = PlayerDataManager.Instance?.CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return;

        FixItNetworkManager.Instance.UpdateUserStats(
            userId: userId,
            gamesPlayed: gamesPlayed,
            ticketsCompleted: ticketsCompleted,
            ticketsFailed: ticketsFailed,
            playTimeSeconds: playTimeSeconds,
            score: score,
            stars: stars,
            onSuccess: (stats) =>
            {
                Debug.Log($"✅ Estadísticas actualizadas");
            },
            onError: (error) =>
            {
                Debug.LogError($"❌ Error actualizando stats: {error}");
            }
        );
    }
}