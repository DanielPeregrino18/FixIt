using UnityEngine;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance { get; private set; }

    private User currentUser;
    private UserProgress currentProgress;

    public string CurrentUserId => currentUser?._id;
    public string CurrentUsername => currentUser?.username;
    public User CurrentUser => currentUser;
    public UserProgress CurrentProgress => currentProgress;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetCurrentUser(User user)
    {
        currentUser = user;
        Debug.Log($"👤 Jugador establecido: {user.username}");
    }

    public void SetProgress(UserProgress progress)
    {
        currentProgress = progress;
    }

    public void UpdateProgressLocally(string worldId, string levelId, int score, int stars)
    {
        if (currentProgress == null) return;

        // Buscar o crear el mundo
        WorldProgress world = currentProgress.GetWorld(worldId);
        if (world == null)
        {
            // Crear nuevo mundo
            var worldEntry = new WorldProgressEntry
            {
                key = worldId,
                value = new WorldProgress()
            };

            // Añadir al array
            var newWorlds = new WorldProgressEntry[currentProgress.worlds.Length + 1];
            System.Array.Copy(currentProgress.worlds, newWorlds, currentProgress.worlds.Length);
            newWorlds[currentProgress.worlds.Length] = worldEntry;
            currentProgress.worlds = newWorlds;

            world = worldEntry.value;
        }

        // Buscar o crear el nivel
        LevelProgress level = world.GetLevel(levelId);
        if (level == null)
        {
            var levelEntry = new LevelProgressEntry
            {
                key = levelId,
                value = new LevelProgress()
            };

            var newLevels = new LevelProgressEntry[world.levels.Length + 1];
            System.Array.Copy(world.levels, newLevels, world.levels.Length);
            newLevels[world.levels.Length] = levelEntry;
            world.levels = newLevels;

            level = levelEntry.value;
        }

        // Actualizar solo si es mejor
        if (score > level.bestScore) level.bestScore = score;
        if (stars > level.bestStars) level.bestStars = stars;

        Debug.Log($"📊 Progreso local actualizado: {worldId}/{levelId} - Score: {score}, Stars: {stars}");
    }

    public void ClearData()
    {
        currentUser = null;
        currentProgress = null;
    }
}