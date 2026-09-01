using System.Collections;
using UnityEngine;

/// <summary>
/// Cafetera. El jugador interactúa con E para tomar café.
/// Tarda 3 segundos y restaura la stamina al 100%.
/// Hereda el brillo de InteractableTable.
/// </summary>
public class CoffeeMachine : InteractableTable
{
    [Header("Configuración")]
    [Tooltip("Segundos que tarda el jugador en tomar el café")]
    public float tiempoTomandoCafe = 3f;

    // ── Estado interno ────────────────────────────────────────────────────────
    private bool _enUso = false;

    // ─────────────────────────────────────────────────────────────────────────

    public override void Interact(PlayerInteraction player)
    {
        if (_enUso)
        {
            Debug.Log("CoffeeMachine: La cafetera ya está en uso.");
            return;
        }

        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm == null) return;

        // Si ya tiene stamina al máximo, no hace nada
        if (pm.staminaActual >= pm.staminaMaxima)
        {
            Debug.Log("CoffeeMachine: El jugador ya tiene la energía al máximo.");
            return;
        }

        StartCoroutine(TomarCafe(player, pm));
    }

    IEnumerator TomarCafe(PlayerInteraction player, PlayerMovement pm)
    {
        _enUso = true;

        // Bloquear movimiento mientras toma el café
        pm.movimientoBloqueado = true;

        Debug.Log("CoffeeMachine: ☕ Tomando café...");

        yield return new WaitForSeconds(tiempoTomandoCafe);

        // Restaurar stamina al 100%
        pm.staminaActual = pm.staminaMaxima;

        // Desbloquear movimiento
        pm.movimientoBloqueado = false;

        _enUso = false;

        Debug.Log("CoffeeMachine: ✅ ¡Energía restaurada al 100%!");
    }
}
