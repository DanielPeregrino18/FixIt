using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoginManager : MonoBehaviour
{
    [Header("UI - Login Panel")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private InputField emailInput;
    [SerializeField] private InputField passwordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private Text errorMessage;
    [SerializeField] private Text statusMessage;

    [Header("UI - Loading")]
    [SerializeField] private GameObject loadingPanel;

    [Header("Settings")]
    [SerializeField] private string gameSceneName = "SampleScene";

    private void Start()
    {
        if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
        if (registerButton != null) registerButton.onClick.AddListener(OnRegisterClicked);

        ShowLoginPanel();
    }

    private void OnLoginClicked()
    {
        string email = emailInput.text.Trim();
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowError("Por favor completa todos los campos");
            return;
        }

        ShowLoading("Iniciando sesión...");

        FixItNetworkManager.Instance.Login(
            email: email,
            password: password,
            onSuccess: (user) =>
            {
                Debug.Log($"✅ Login exitoso: {user.username} (ID: {user._id})");

                // Guardar datos del jugador
                PlayerDataManager.Instance.SetCurrentUser(user);

                ShowStatus($"¡Bienvenido {user.username}!");

                // Cargar progreso y luego ir al juego
                LoadProgressAndEnterGame();
            },
            onError: (error) =>
            {
                Debug.LogError($"❌ Login falló: {error}");
                ShowError("Email o contraseña incorrectos");
                ShowLoginPanel();
            }
        );
    }

    private void OnRegisterClicked()
    {
        string email = emailInput.text.Trim();
        string password = passwordInput.text;
        string username = email.Split('@')[0];

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowError("Por favor completa todos los campos");
            return;
        }

        if (password.Length < 6)
        {
            ShowError("La contraseña debe tener al menos 6 caracteres");
            return;
        }

        ShowLoading("Creando cuenta...");

        FixItNetworkManager.Instance.Register(
            username: username,
            email: email,
            password: password,
            onSuccess: (user) =>
            {
                Debug.Log($"✅ Registro exitoso: {user.username}");

                PlayerDataManager.Instance.SetCurrentUser(user);

                ShowStatus($"¡Cuenta creada! Bienvenido {user.username}");

                LoadProgressAndEnterGame();
            },
            onError: (error) =>
            {
                Debug.LogError($"❌ Registro falló: {error}");
                ShowError("Error al crear cuenta. El email puede estar en uso.");
                ShowLoginPanel();
            }
        );
    }

    private void LoadProgressAndEnterGame()
    {
        string userId = PlayerDataManager.Instance.CurrentUserId;

        FixItNetworkManager.Instance.GetProgress(
            userId: userId,
            onSuccess: (progress) =>
            {
                Debug.Log($"✅ Progreso cargado: {progress.worlds.Length} mundos");
                PlayerDataManager.Instance.SetProgress(progress);

                // Ir al juego después de un breve delay
                Invoke(nameof(EnterGameScene), 1f);
            },
            onError: (error) =>
            {
                Debug.LogWarning($"⚠️ Sin progreso previo (usuario nuevo): {error}");
                // Usuario nuevo, crear progreso vacío
                PlayerDataManager.Instance.SetProgress(new UserProgress());
                Invoke(nameof(EnterGameScene), 1f);
            }
        );
    }

    private void EnterGameScene()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    #region UI Helpers
    private void ShowLoginPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (errorMessage != null) errorMessage.text = "";
    }

    private void ShowLoading(string message)
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (statusMessage != null) statusMessage.text = message;
    }

    private void ShowError(string message)
    {
        if (errorMessage != null) errorMessage.text = message;
    }

    private void ShowStatus(string message)
    {
        if (statusMessage != null) statusMessage.text = message;
    }
    #endregion
}