using System.IO;
using UnityEngine;

/// <summary>
/// Carga el JSON de un nivel desde StreamingAssets/Niveles/
/// </summary>
public class NivelLoader : MonoBehaviour
{
    public static NivelLoader Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Carga y devuelve el NivelData del nivel indicado.</summary>
    public NivelData CargarNivel(int numeroNivel)
    {
        string nombreArchivo = $"nivel_{numeroNivel:D2}.json";
        string ruta          = Path.Combine(Application.streamingAssetsPath, "Niveles", nombreArchivo);

        if (!File.Exists(ruta))
        {
            Debug.LogError($"NivelLoader: No se encontró el archivo {ruta}");
            return null;
        }

        string json     = File.ReadAllText(ruta);
        NivelData datos = JsonUtility.FromJson<NivelData>(json);

        Debug.Log($"NivelLoader: Nivel {numeroNivel} cargado con {datos.oleadas.Count} oleadas.");
        return datos;
    }
}
