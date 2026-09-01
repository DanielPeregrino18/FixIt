using UnityEngine;

/// <summary>
/// Datos de un jugador en el lobby.
/// </summary>
public class PlayerData
{
    public int      jugadorId;        // 1, 2, 3 o 4
    public int      personajeIndex;   // índice del personaje elegido
    public bool     listo;            // presionó A por segunda vez
    public string   dispositivo;      // "Keyboard" o "Gamepad"

    public PlayerData(int id, string dispositivo)
    {
        jugadorId      = id;
        personajeIndex = 0;
        listo          = false;
        this.dispositivo = dispositivo;
    }
}
