using System.Collections.Generic;
using UnityEngine;

public class InteractableTable : MonoBehaviour
{
    // Lista por si 2 jugadores miran la mesa al mismo tiempo:
    // no se apaga el brillo si uno de ellos voltea.
    protected List<int> _playersLooking = new List<int>();

    [Header("Configuración Visual")]
    public MeshRenderer tableRenderer;
    public Material normalMaterial;
    public Material glowingMaterial;

    public void OnLookEnter(int playerId)
    {
        if (!_playersLooking.Contains(playerId))
        {
            _playersLooking.Add(playerId);
            UpdateVisuals();
        }
    }

    public void OnLookExit(int playerId)
    {
        if (_playersLooking.Contains(playerId))
        {
            _playersLooking.Remove(playerId);
            UpdateVisuals();
        }
    }

    protected virtual void UpdateVisuals()
    {
        if (tableRenderer == null) return;
        tableRenderer.material = _playersLooking.Count > 0 ? glowingMaterial : normalMaterial;
    }

    /// <summary>Interacción principal (E / botón A).</summary>
    public virtual void Interact(PlayerInteraction player)
    {
        Debug.Log("Interactuando con una mesa genérica");
    }

    /// <summary>
    /// Interacción secundaria (Q / botón X). El JUGADOR avisa a la mesa
    /// que está apuntando — así cada mesa solo recibe el input del jugador
    /// que realmente la está usando, no el input global de todos.
    /// </summary>
    public virtual void SecondaryInteract(PlayerInteraction player)
    {
        // Por defecto no hace nada. Repair/Cleaning/Normal la sobreescriben.
    }
}