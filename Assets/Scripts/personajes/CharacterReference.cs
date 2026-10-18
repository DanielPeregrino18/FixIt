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

    [Tooltip("Opcional: si este personaje necesita poses distintas (brazos con otra orientación), " +
             "asigna aquí un Animator Override Controller con los clips corregidos para él. " +
             "Si se deja vacío, usa el Animator Controller base sin cambios.")]
    public RuntimeAnimatorController overrideAnimator;

    [Header("Ajuste del martillo (minijuego de reparación)")]
    [Tooltip("Cada personaje puede necesitar su propio offset/rotación porque el hueso de la mano está orientado distinto")]
    public Vector3 offsetMartillo = Vector3.zero;
    public Vector3 rotacionMartillo = Vector3.zero;
    [Tooltip("Tamaño REAL deseado del martillo para este personaje (ya compensa la escala interna de su hueso)")]
    public float escalaMartillo = 2f;

    [Header("Ajuste de la lata de aire (minijuego de limpieza)")]
    public Vector3 offsetLata = Vector3.zero;
    public Vector3 rotacionLata = Vector3.zero;
    public float escalaLata = 2f;

    [Header("Ajuste de la taza de café")]
    public Vector3 offsetTaza = Vector3.zero;
    public Vector3 rotacionTaza = Vector3.zero;
    public float escalaTaza = 2f;
}