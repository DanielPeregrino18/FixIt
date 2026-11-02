using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Cliente HTTP centralizado para los microservicios de FixIt.
/// Añade este componente a un GameObject de la primera escena.
/// </summary>
public class FixItNetworkManager : MonoBehaviour
{
    public static FixItNetworkManager Instance { get; private set; }

    [Header("Service URLs")]
    [SerializeField] private string userBaseUrl = "https://fixit-user-service.onrender.com";
    [SerializeField] private string progressBaseUrl = "https://fixit-progress-service.onrender.com";
    [SerializeField] private string sessionBaseUrl = "https://fixit-session-service.onrender.com";
    [SerializeField] private string newsBaseUrl = "https://fixit-news-service.onrender.com";

    [Header("Request Settings")]
    [Tooltip("Los cold starts de Render pueden tardar más que una petición normal.")]
    [SerializeField, Min(1)] private int requestTimeoutSeconds = 60;

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

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Registra una cuenta y devuelve el usuario creado.</summary>
    public void Register(string username, string email, string password, Action<User> onSuccess, Action<string> onError)
    {
        StartCoroutine(SendRequest("POST", BuildUrl(userBaseUrl, "/users/register"),
            new CredentialsPayload { username = username, email = email, password = password },
            response => ParseUser(response, onSuccess, onError), onError));
    }

    /// <summary>Inicia sesión con email y contraseña.</summary>
    public void Login(string email, string password, Action<User> onSuccess, Action<string> onError)
    {
        StartCoroutine(SendRequest("POST", BuildUrl(userBaseUrl, "/users/login"),
            new CredentialsPayload { email = email, password = password },
            response => ParseUser(response, onSuccess, onError), onError));
    }

    /// <summary>Obtiene las estadísticas acumuladas del usuario.</summary>
    public void GetUserStats(string userId, Action<UserStats> onSuccess, Action<string> onError)
    {
        string path = "/users/" + UnityWebRequest.EscapeURL(userId) + "/stats";
        StartCoroutine(SendRequest("GET", BuildUrl(userBaseUrl, path), null,
            response => ParseStats(response, onSuccess, onError), onError));
    }

    /// <summary>Incrementa las estadísticas acumuladas; todos los valores deben ser >= 0.</summary>
    public void UpdateUserStats(string userId, int gamesPlayed, int ticketsCompleted, int ticketsFailed,
        int playTimeSeconds, int score, int stars, Action<UserStats> onSuccess, Action<string> onError)
    {
        if (gamesPlayed < 0 || ticketsCompleted < 0 || ticketsFailed < 0 ||
            playTimeSeconds < 0 || score < 0 || stars < 0)
        {
            ReportArgumentError("Las estadísticas incrementales deben ser mayores o iguales a cero.", onError);
            return;
        }

        string path = "/users/" + UnityWebRequest.EscapeURL(userId) + "/stats";
        StatsIncrement payload = new StatsIncrement
        {
            gamesPlayed = gamesPlayed,
            ticketsCompleted = ticketsCompleted,
            ticketsFailed = ticketsFailed,
            playTimeSeconds = playTimeSeconds,
            score = score,
            stars = stars
        };

        StartCoroutine(SendRequest("PATCH", BuildUrl(userBaseUrl, path), payload,
            response => ParseStats(response, onSuccess, onError), onError));
    }

    /// <summary>Obtiene el progreso completo del usuario, incluidos los mapas dinámicos del backend.</summary>
    public void GetProgress(string userId, Action<UserProgress> onSuccess, Action<string> onError)
    {
        string path = "/progress/" + UnityWebRequest.EscapeURL(userId);
        StartCoroutine(SendRequest("GET", BuildUrl(progressBaseUrl, path), null, response =>
        {
            try
            {
                if (onSuccess != null) onSuccess(UserProgress.FromJson(response));
            }
            catch (Exception exception)
            {
                ReportParseError("progreso", response, exception, onError);
            }
        }, onError));
    }

    /// <summary>Envía un resultado; el backend conserva solo la mejor puntuación del nivel.</summary>
    public void UpdateProgress(string userId, string worldId, string levelId, int score, int starsEarned,
        Action<UserProgress> onSuccess, Action<string> onError)
    {
        if (score < 0 || starsEarned < 0)
        {
            ReportArgumentError("La puntuación y las estrellas deben ser mayores o iguales a cero.", onError);
            return;
        }

        string path = "/progress/" + UnityWebRequest.EscapeURL(userId);
        ProgressUpdate payload = new ProgressUpdate
        {
            worldId = worldId,
            levelId = levelId,
            score = score,
            starsEarned = starsEarned
        };

        StartCoroutine(SendRequest("PATCH", BuildUrl(progressBaseUrl, path), payload, response =>
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                if (onSuccess != null) onSuccess(null);
                return;
            }

            try
            {
                if (onSuccess != null) onSuccess(UserProgress.FromJson(response));
            }
            catch (Exception exception)
            {
                ReportParseError("progreso", response, exception, onError);
            }
        }, onError));
    }

    /// <summary>Registra una partida terminada o abandonada.</summary>
    public void PostSession(GameSession session, Action<GameSession> onSuccess, Action<string> onError)
    {
        if (session == null)
        {
            ReportArgumentError("La sesión no puede ser null.", onError);
            return;
        }

        StartCoroutine(SendRequest("POST", BuildUrl(sessionBaseUrl, "/sessions"), session,
            response => ParseSession(response, onSuccess, onError), onError));
    }

    /// <summary>Registra una partida terminada usando un payload limpio (sin _id para evitar errores de casteo).</summary>
    public void PostSessionClean(SessionCreateRequest sessionRequest, Action<GameSession> onSuccess, Action<string> onError)
    {
        if (sessionRequest == null)
        {
            ReportArgumentError("La petición de sesión no puede ser null.", onError);
            return;
        }

        StartCoroutine(SendRequest("POST", BuildUrl(sessionBaseUrl, "/sessions"), sessionRequest,
            response =>
            {
                // 🔍 ESTO NOS DIRÁ EXACTAMENTE QUÉ ESTÁ DEVOLVIENDO EL BACKEND
                Debug.Log($"📥 RESPUESTA RAW DEL SERVIDOR: '{response}'");

                ParseSession(response, onSuccess, onError);
            },
            onError));
    }

    /// <summary>Obtiene las noticias publicadas, ordenadas por fecha más reciente primero.</summary>
    public void GetNews(Action<List<NewsItem>> onSuccess, Action<string> onError)
    {
        StartCoroutine(SendRequest("GET", BuildUrl(newsBaseUrl, "/news"), null, response =>
        {
            try
            {
                NewsItem[] items;
                if (response.TrimStart().StartsWith("[", StringComparison.Ordinal))
                {
                    items = JsonUtility.FromJson<NewsArrayResponse>("{\"items\":" + response + "}").items;
                }
                else
                {
                    NewsResponse wrapped = JsonUtility.FromJson<NewsResponse>(response);
                    items = wrapped != null
                        ? (wrapped.news ?? wrapped.items ?? wrapped.data)
                        : null;
                }

                if (items == null)
                    throw new FormatException("La respuesta no contiene una lista de noticias reconocible.");

                if (onSuccess != null) onSuccess(new List<NewsItem>(items));
            }
            catch (Exception exception)
            {
                ReportParseError("noticias", response, exception, onError);
            }
        }, onError));
    }

    /// <summary>Consulta el health del servicio de progreso y devuelve el JSON sin modificar.</summary>
    public void GetUserHealth(Action<string> onSuccess, Action<string> onError)
    {
        GetServiceHealth(userBaseUrl, onSuccess, onError);
    }

    /// <summary>Consulta el health del servicio de progreso y devuelve el JSON sin modificar.</summary>
    public void GetProgressHealth(Action<string> onSuccess, Action<string> onError)
    {
        GetServiceHealth(progressBaseUrl, onSuccess, onError);
    }

    /// <summary>Consulta el health del servicio de sesiones y devuelve el JSON sin modificar.</summary>
    public void GetSessionHealth(Action<string> onSuccess, Action<string> onError)
    {
        GetServiceHealth(sessionBaseUrl, onSuccess, onError);
    }

    /// <summary>Consulta el health del servicio de noticias y devuelve el JSON sin modificar.</summary>
    public void GetNewsHealth(Action<string> onSuccess, Action<string> onError)
    {
        GetServiceHealth(newsBaseUrl, onSuccess, onError);
    }

    private void GetServiceHealth(string serviceBaseUrl, Action<string> onSuccess, Action<string> onError)
    {
        StartCoroutine(SendRequest("GET", BuildUrl(serviceBaseUrl, "/health"), null,
            onSuccess, onError));
    }

    private IEnumerator SendRequest(string method, string url, object payload,
        Action<string> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest request = new UnityWebRequest(url, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.Max(1, requestTimeoutSeconds);

            if (payload != null)
            {
                string json = JsonUtility.ToJson(payload);
                Debug.Log($"📤 ENVIANDO A {url}");
                Debug.Log($"📦 PAYLOAD: {json}");
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            }

            request.SetRequestHeader("Accept", "application/json");
            if (payload != null) request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorMessage = string.Format("HTTP {0} en {1}: {2}. Respuesta: {3}",
                    request.responseCode, url, request.error, responseBody);
                Debug.LogError("FixItNetworkManager: " + errorMessage);
                if (onError != null) onError(errorMessage);
                yield break;
            }

            if (onSuccess != null) onSuccess(responseBody);
        }
    }

    private static string BuildUrl(string baseUrl, string path)
    {
        return (baseUrl ?? string.Empty).TrimEnd('/') + path;
    }

    private static void ParseUser(string json, Action<User> onSuccess, Action<string> onError)
    {
        try
        {
            UserResponse wrapped = JsonUtility.FromJson<UserResponse>(json);
            User user = wrapped != null ? wrapped.user : null;
            if (user == null) user = JsonUtility.FromJson<User>(json);
            if (user == null) throw new FormatException("La respuesta no contiene un usuario.");
            if (onSuccess != null) onSuccess(user);
        }
        catch (Exception exception)
        {
            ReportParseError("usuario", json, exception, onError);
        }
    }

    private static void ParseStats(string json, Action<UserStats> onSuccess, Action<string> onError)
    {
        try
        {
            StatsResponse wrapped = JsonUtility.FromJson<StatsResponse>(json);
            UserStats stats = wrapped != null ? wrapped.stats : null;
            if (stats == null) stats = JsonUtility.FromJson<UserStats>(json);
            if (stats == null) throw new FormatException("La respuesta no contiene estadísticas.");
            if (onSuccess != null) onSuccess(stats);
        }
        catch (Exception exception)
        {
            ReportParseError("estadísticas", json, exception, onError);
        }
    }

    private static void ParseSession(string json, Action<GameSession> onSuccess, Action<string> onError)
    {
        try
        {
            SessionResponse wrapped = JsonUtility.FromJson<SessionResponse>(json);
            GameSession session = wrapped != null ? wrapped.session : null;

            // ⚠️ PROBLEMA: Si session es null, debería llamar a onError, no onSuccess
            if (session == null) session = JsonUtility.FromJson<GameSession>(json);
            if (session == null) throw new FormatException("La respuesta no contiene una sesión.");

            if (onSuccess != null) onSuccess(session);
        }
        catch (Exception exception)
        {
            ReportParseError("sesión", json, exception, onError);
        }
    }

    private static void ReportParseError(string resource, string response, Exception exception, Action<string> onError)
    {
        string message = "No se pudo interpretar la respuesta de " + resource + ": " + exception.Message;
        Debug.LogError("FixItNetworkManager: " + message + " Respuesta: " + response);
        if (onError != null) onError(message);
    }

    private static void ReportArgumentError(string message, Action<string> onError)
    {
        Debug.LogError("FixItNetworkManager: " + message);
        if (onError != null) onError(message);
    }

    [Serializable]
    private class CredentialsPayload
    {
        public string username;
        public string email;
        public string password;
    }

    [Serializable]
    private class StatsIncrement
    {
        public int gamesPlayed;
        public int ticketsCompleted;
        public int ticketsFailed;
        public int playTimeSeconds;
        public int score;
        public int stars;
    }

    [Serializable]
    private class ProgressUpdate
    {
        public string worldId;
        public string levelId;
        public int score;
        public int starsEarned;
    }

    [Serializable]
    private class UserResponse { public User user; }
    [Serializable]
    private class StatsResponse { public UserStats stats; }
    [Serializable]
    private class SessionResponse { public GameSession session; }
    [Serializable]
    private class NewsResponse
    {
        public NewsItem[] news;
        public NewsItem[] items;
        public NewsItem[] data;
    }
    [Serializable]
    private class NewsArrayResponse { public NewsItem[] items; }
}