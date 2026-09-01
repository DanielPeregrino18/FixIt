using UnityEngine;

/// <summary>
/// Va en el prefab del jugador principal.
/// Reemplaza el modelo visual por el del personaje elegido en el lobby.
/// </summary>
public class CharacterSwapper : MonoBehaviour
{
    /// <summary>Cambia el modelo visual y devuelve la foto del personaje para el HUD.</summary>
    public Sprite SwapModel(GameObject characterPrefab, int jugadorId)
    {
        if (characterPrefab == null)
        {
            Debug.LogWarning("CharacterSwapper: No hay prefab de personaje asignado.");
            return null;
        }

        // 1. Instanciar el prefab elegido temporalmente (desactivado)
        GameObject temp = Instantiate(characterPrefab, Vector3.zero, Quaternion.identity);
        temp.SetActive(false);

        // 2. Obtener referencias del nuevo personaje
        CharacterReference charRef = temp.GetComponent<CharacterReference>();

        // 3. Encontrar Armature y Mesh_0 del nuevo personaje
        Transform nuevoArmature = temp.transform.Find("Armature");
        Transform nuevoMesh     = temp.transform.Find("Mesh_0");

        // 4. Re-emparentar preservando transforms originales
        if (nuevoArmature != null)
        {
            Vector3    armPos   = nuevoArmature.localPosition;
            Quaternion armRot   = nuevoArmature.localRotation;
            Vector3    armScale = nuevoArmature.localScale;

            nuevoArmature.SetParent(transform);
            nuevoArmature.localPosition = armPos;
            nuevoArmature.localRotation = armRot;
            nuevoArmature.localScale    = armScale;
        }

        if (nuevoMesh != null)
        {
            Vector3    meshPos   = nuevoMesh.localPosition;
            Quaternion meshRot   = nuevoMesh.localRotation;
            Vector3    meshScale = nuevoMesh.localScale;

            nuevoMesh.SetParent(transform);
            nuevoMesh.localPosition = meshPos;
            nuevoMesh.localRotation = meshRot;
            nuevoMesh.localScale    = meshScale;
        }

        // 5. Guardar referencias DESPUÉS de re-emparentar
        Transform newHeadBone   = charRef != null ? charRef.headBone   : null;
        Transform newItemSocket = charRef != null ? charRef.itemSocket  : null;
        bool invertir           = charRef != null && charRef.invertirDireccion;
        Sprite fotoPersonaje    = charRef != null ? charRef.fotoPersonaje : null;

        // 6. Destruir el temp — Armature y Mesh_0 ya están bajo nuestro jugador
        Destroy(temp);

        // 7. Destruir Armature y Mesh_0 viejos
        foreach (Transform hijo in transform)
        {
            if ((hijo.name == "Armature" || hijo.name == "Mesh_0") &&
                hijo != nuevoArmature && hijo != nuevoMesh)
                Destroy(hijo.gameObject);
        }

        // 8. Actualizar ProceduralHead
        ProceduralHead proceduralHead = GetComponent<ProceduralHead>();
        if (proceduralHead != null && newHeadBone != null)
        {
            proceduralHead.headBone         = newHeadBone;
            proceduralHead.invertirDireccion = invertir;
            Debug.Log($"CharacterSwapper: headBone={newHeadBone.name}, invertir={invertir}");
        }

        // 9. Actualizar PlayerInteraction
        PlayerInteraction playerInteraction = GetComponent<PlayerInteraction>();
        if (playerInteraction != null && newItemSocket != null)
        {
            playerInteraction.itemSocket = newItemSocket;
            Debug.Log("CharacterSwapper: itemSocket actualizado.");
        }

        Debug.Log($"CharacterSwapper: Modelo cambiado exitosamente para jugador {jugadorId}.");
        return fotoPersonaje;
    }
}