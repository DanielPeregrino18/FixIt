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
        Transform nuevoMesh = temp.transform.Find("Mesh_0");

        if (nuevoArmature == null)
            Debug.LogWarning($"CharacterSwapper: NO se encontró 'Armature' en {characterPrefab.name}. Hijos reales: {NombresDeHijos(temp.transform)}");
        if (nuevoMesh == null)
            Debug.LogWarning($"CharacterSwapper: NO se encontró 'Mesh_0' en {characterPrefab.name}. Hijos reales: {NombresDeHijos(temp.transform)}");

        // 4. Re-emparentar preservando transforms originales
        if (nuevoArmature != null)
        {
            Vector3 armPos = nuevoArmature.localPosition;
            Quaternion armRot = nuevoArmature.localRotation;
            Vector3 armScale = nuevoArmature.localScale;

            nuevoArmature.SetParent(transform);
            nuevoArmature.localPosition = armPos;
            nuevoArmature.localRotation = armRot;
            nuevoArmature.localScale = armScale;

            // El Prefab de origen trae guardado su propio Layer (normalmente
            // "Default"), que no coincide con el Layer real del objeto al que
            // se está pegando (por ejemplo, "Vitrina1" en el lobby). Forzamos
            // que toda la jerarquía nueva herede el Layer del objeto raíz.
            AplicarLayerATodos(nuevoArmature, gameObject.layer);
        }

        if (nuevoMesh != null)
        {
            Vector3 meshPos = nuevoMesh.localPosition;
            Quaternion meshRot = nuevoMesh.localRotation;
            Vector3 meshScale = nuevoMesh.localScale;

            nuevoMesh.SetParent(transform);
            nuevoMesh.localPosition = meshPos;
            nuevoMesh.localRotation = meshRot;
            nuevoMesh.localScale = meshScale;

            AplicarLayerATodos(nuevoMesh, gameObject.layer);

            // El SkinnedMeshRenderer calcula sus límites visuales (bounds) una
            // sola vez al activarse. Si eso pasa justo cuando se está pegando
            // el hueso nuevo, puede quedar mal calculado para siempre en esta
            // sesión, y Unity "cree" que el personaje está fuera de cámara aunque
            // se vea perfecto — dejando de animarlo en silencio. Forzamos que
            // recalcule esos límites en cada frame, en vez de una sola vez.
            SkinnedMeshRenderer smr = nuevoMesh.GetComponent<SkinnedMeshRenderer>();
            if (smr != null) smr.updateWhenOffscreen = true;
        }

        // 5. Guardar referencias DESPUÉS de re-emparentar, pero SIEMPRE antes
        // de Destroy(temp) — leer charRef después de esa línea regresa valores
        // en blanco aunque el Prefab tenga los datos correctos guardados.
        Transform newHeadBone = charRef != null ? charRef.headBone : null;
        Transform newItemSocket = charRef != null ? charRef.itemSocket : null;
        bool invertir = charRef != null && charRef.invertirDireccion;
        Sprite fotoPersonaje = charRef != null ? charRef.fotoPersonaje : null;
        Vector3 offsetMartillo = charRef != null ? charRef.offsetMartillo : Vector3.zero;
        Vector3 rotacionMartillo = charRef != null ? charRef.rotacionMartillo : Vector3.zero;
        float escalaMartillo = charRef != null ? charRef.escalaMartillo : 2f;
        Vector3 offsetLata = charRef != null ? charRef.offsetLata : Vector3.zero;
        Vector3 rotacionLata = charRef != null ? charRef.rotacionLata : Vector3.zero;
        float escalaLata = charRef != null ? charRef.escalaLata : 2f;
        Vector3 offsetTaza = charRef != null ? charRef.offsetTaza : Vector3.zero;
        Vector3 rotacionTaza = charRef != null ? charRef.rotacionTaza : Vector3.zero;
        float escalaTaza = charRef != null ? charRef.escalaTaza : 2f;

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
            proceduralHead.headBone = newHeadBone;
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

        // 9b. Traspasar el ajuste del martillo de este personaje (offset,
        // rotación, escala) — usando las variables ya capturadas arriba,
        // no charRef directamente (ya no es seguro leerlo aquí).
        if (playerInteraction != null)
        {
            playerInteraction.offsetMartillo = offsetMartillo;
            playerInteraction.rotacionMartillo = rotacionMartillo;
            playerInteraction.escalaMartillo = escalaMartillo;
            playerInteraction.offsetLata = offsetLata;
            playerInteraction.rotacionLata = rotacionLata;
            playerInteraction.escalaLata = escalaLata;
            playerInteraction.offsetTaza = offsetTaza;
            playerInteraction.rotacionTaza = rotacionTaza;
            playerInteraction.escalaTaza = escalaTaza;
        }

        // 10. Si este personaje necesita poses distintas, usar su Override
        // Controller (mismo "cerebro" de estados/transiciones, solo cambian
        // los clips). Si no tiene uno asignado, se queda con el Controller base.
        Animator anim = GetComponent<Animator>();
        if (anim != null && charRef != null && charRef.overrideAnimator != null)
        {
            anim.runtimeAnimatorController = charRef.overrideAnimator;
            Debug.Log($"CharacterSwapper: Override Controller aplicado para jugador {jugadorId}.");
        }

        // 11. El Animator "memoriza" los huesos al activarse — como acabamos de
        // destruir el Armature viejo y pegar uno nuevo, hay que decirle que
        // vuelva a escanear la jerarquía. Sin esto, sigue animando huesos que
        // ya no existen y no se ve ningún movimiento (sin ningún error en consola).
        //
        // Esto se hace con un frame de retraso (corrutina) a propósito: si se
        // hace en el mismo instante que todo lo demás, la propia inicialización
        // interna del Animator (que ocurre ese mismo frame) puede sobreescribirlo
        // después de nosotros, dejando todo como si nunca lo hubiéramos tocado.
        if (anim != null)
            StartCoroutine(ForzarAnimatorSiempreActivo(anim));

        Debug.Log($"CharacterSwapper: Modelo cambiado exitosamente para jugador {jugadorId}.");
        return fotoPersonaje;
    }

    System.Collections.IEnumerator ForzarAnimatorSiempreActivo(Animator anim)
    {
        yield return null; // esperar 1 frame completo a que el Animator termine su propia inicialización

        if (anim == null) yield break;

        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; // nunca dejar de animar por bounds/cámara
        anim.Rebind();
        anim.Update(0f);
        Debug.Log("CharacterSwapper: Animator rebindeado y forzado a Always Animate (con 1 frame de retraso).");
    }

    /// <summary>Aplica un Layer a un objeto y a todos sus hijos, recursivamente.</summary>
    void AplicarLayerATodos(Transform raiz, int layer)
    {
        raiz.gameObject.layer = layer;
        foreach (Transform hijo in raiz)
            AplicarLayerATodos(hijo, layer);
    }

    /// <summary>Lista los nombres de los hijos directos, para diagnosticar cuando Find() no encuentra algo.</summary>
    string NombresDeHijos(Transform padre)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (Transform hijo in padre)
            sb.Append($"'{hijo.name}', ");
        return sb.Length > 0 ? sb.ToString() : "(sin hijos)";
    }
}