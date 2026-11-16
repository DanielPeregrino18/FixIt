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

    /// <summary>True si hay una cuenta con sesión iniciada. Sin sesión, el juego guarda solo en local.</summary>
    public bool HaySesion => !string.IsNullOrEmpty(CurrentUserId);

    /// <summary>Se dispara al iniciar o cerrar sesión.</summary>
    public event System.Action UsuarioCambiado;

    // La sesión se recuerda entre ejecuciones (solo id, nombre y email; nunca la contraseña)
    private const string KEY_SESION_ID = "FixIT_Sesion_Id";
    private const string KEY_SESION_NOMBRE = "FixIT_Sesion_Nombre";
    private const string KEY_SESION_EMAIL = "FixIT_Sesion_Email";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        RestaurarSesionGuardada();
    }

    private void RestaurarSesionGuardada()
    {
        string id = PlayerPrefs.GetString(KEY_SESION_ID, "");
        if (string.IsNullOrEmpty(id)) return;

        currentUser = new User
        {
            _id = id,
            username = PlayerPrefs.GetString(KEY_SESION_NOMBRE, ""),
            email = PlayerPrefs.GetString(KEY_SESION_EMAIL, "")
        };
        Debug.Log($"👤 Sesión restaurada: {currentUser.username}");
    }

    public void SetCurrentUser(User user) => IniciarSesion(user);

    /// <summary>Guarda el usuario como sesión activa (también para la próxima vez que se abra el juego).</summary>
    public void IniciarSesion(User user)
    {
        if (user == null || string.IsNullOrEmpty(user._id)) return;

        currentUser = user;
        PlayerPrefs.SetString(KEY_SESION_ID, user._id);
        PlayerPrefs.SetString(KEY_SESION_NOMBRE, user.username ?? "");
        PlayerPrefs.SetString(KEY_SESION_EMAIL, user.email ?? "");
        PlayerPrefs.Save();

        Debug.Log($"👤 Jugador establecido: {user.username}");
        UsuarioCambiado?.Invoke();
    }

    /// <summary>Cierra la sesión. El progreso guardado en la PC se queda como progreso local.</summary>
    public void CerrarSesion()
    {
        if (currentUser == null) return;

        currentUser = null;
        currentProgress = null;
        PlayerPrefs.DeleteKey(KEY_SESION_ID);
        PlayerPrefs.DeleteKey(KEY_SESION_NOMBRE);
        PlayerPrefs.DeleteKey(KEY_SESION_EMAIL);
        PlayerPrefs.Save();

        Debug.Log("👤 Sesión cerrada: el juego sigue en local");
        UsuarioCambiado?.Invoke();
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

    public void ClearData() => CerrarSesion();
}