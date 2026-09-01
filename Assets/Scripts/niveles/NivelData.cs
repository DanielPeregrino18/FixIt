using System.Collections.Generic;

/// <summary>
/// Clases que representan la estructura del JSON de un nivel.
/// </summary>

[System.Serializable]
public class OleadaData
{
    public float segundo;           // En qué segundo del nivel llega esta oleada
    public List<string> laptops;    // Tipos: "facil", "media", "dificil", "muy_dificil"
}

[System.Serializable]
public class ServidorData
{
    public int   id;        // ID del servidor en la escena (1, 2, 3)
    public float segundo;   // Segundo del nivel en que inicia su incendio
}

[System.Serializable]
public class NivelData
{
    public int  nivel;                  // Número del nivel (1-20)
    public bool incendios;              // ¿Se activan incendios de laptops?
    public List<ServidorData> servidores = new List<ServidorData>(); // Servidores programados ([] = ninguno)
    public List<OleadaData>   oleadas;  // Oleadas del nivel
}