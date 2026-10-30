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

    /// <summary>
    /// Devuelve a cada jugador a su punto de spawn (por su número de jugador).
    /// Se llama al empezar o reiniciar un nivel.
    /// </summary>
    public void ReposicionarJugadores()
    {
        foreach (PlayerJoinHandler j in FindObjectsByType<PlayerJoinHandler>(FindObjectsSortMode.None))
        {
            int indice = j.JugadorId - 1;
            if (indice < 0 || indice >= spawnPoints.Length || spawnPoints[indice] == null) continue;

            Transform punto = spawnPoints[indice];
            j.transform.SetPositionAndRotation(punto.position, punto.rotation);

            Rigidbody rb = j.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = punto.position;
                rb.rotation = punto.rotation;
            }
        }

        Physics.SyncTransforms();
    }
}