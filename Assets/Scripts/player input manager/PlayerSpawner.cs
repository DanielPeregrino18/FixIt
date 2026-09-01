using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Coloca a cada jugador en su punto de spawn al unirse.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [Header("Puntos de spawn (uno por jugador)")]
    public Transform[] spawnPoints;

    private int _jugadoresSpawneados = 0;

    public void OnPlayerJoined(PlayerInput playerInput)
    {
        if (_jugadoresSpawneados >= spawnPoints.Length) return;

        // Mover al jugador a su punto de spawn
        playerInput.transform.position = spawnPoints[_jugadoresSpawneados].position;
        playerInput.transform.rotation = spawnPoints[_jugadoresSpawneados].rotation;

        _jugadoresSpawneados++;
    }
}
