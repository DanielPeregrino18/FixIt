using UnityEngine;

/// <summary>
/// Va en cada prefab de personaje.
/// Almacena las referencias clave para que CharacterSwapper
/// pueda actualizarlas al cambiar el modelo visual.
/// </summary>
public class CharacterReference : MonoBehaviour
{
    [Tooltip("El hueso de la cabeza (cabeza o hueso_cabeza según el personaje)")]
    public Transform headBone;

    [Tooltip("El punto donde el jugador sostiene los objetos")]
    public Transform itemSocket;

    [Tooltip("Activa si la cabeza se mueve al revés (personajes con huesos invertidos)")]
    public bool invertirDireccion = false;

    [Tooltip("Foto de este personaje para el retrato del HUD")]
    public Sprite fotoPersonaje;
}