using UnityEngine;

/// <summary>
/// Componente genérico para cualquier objeto recogible del suelo.
/// Agrégalo a los prefabs de RAM, HDD, CPU y cualquier objeto futuro.
/// Las laptops ya tienen LaptopController que cumple la misma función.
/// </summary>
public class PickableItem : MonoBehaviour
{
    public bool isInteractable = true;
}
