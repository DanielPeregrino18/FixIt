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

    [Header("Prop cosmético (taza de café)")]
    [Tooltip("Prefab visual de la taza. Se instancia en la mano SOLO mientras dura la animación — nunca es un objeto recogible del mundo, no pasa por heldItem.")]
    public GameObject tazaPrefab;
    [Tooltip("Grados que se inclina el personaje mientras toma café — prueba cada eje libremente (X = adelante/atrás, Y = girar, Z = de lado). 0,0,0 = sin inclinar.")]
    public Vector3 inclinacionCafe = new Vector3(15f, 0f, 0f);

    // ── Estado interno ────────────────────────────────────────────────────────
    private bool _enUso = false;
    private GameObject _tazaInstanciada = null;

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

        // Guardar la rotación original para regresar exacto a como estaba,
        // sin importar hacia dónde estuviera mirando el jugador antes de esto.
        Quaternion rotacionOriginal = player.transform.rotation;
        player.transform.rotation = rotacionOriginal * Quaternion.Euler(inclinacionCafe);

        // Instanciar la taza directo en el hueso de la mano (no en ItemSocket
        // — nunca pasa por heldItem, es puramente visual y vive solo aquí).
        Transform armature = player.transform.Find("Armature");
        Transform manoDer = armature != null ? BuscarHijoPorNombre(armature, "base_mano_der") : null;

        if (tazaPrefab != null && manoDer != null)
        {
            _tazaInstanciada = Instantiate(tazaPrefab, manoDer);
            _tazaInstanciada.transform.localPosition = player.offsetTaza;
            _tazaInstanciada.transform.localRotation = Quaternion.Euler(player.rotacionTaza);

            // Compensar la escala real del hueso (varía entre personajes)
            Vector3 escalaHueso = manoDer.lossyScale;
            _tazaInstanciada.transform.localScale = new Vector3(
                player.escalaTaza / escalaHueso.x,
                player.escalaTaza / escalaHueso.y,
                player.escalaTaza / escalaHueso.z);

            Rigidbody rb = _tazaInstanciada.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            Collider col = _tazaInstanciada.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        if (player.anim != null)
            player.anim.SetBool("TomandoCafe", true);

        Debug.Log("CoffeeMachine: ☕ Tomando café...");

        yield return new WaitForSeconds(tiempoTomandoCafe);

        // Restaurar stamina al 100%
        pm.staminaActual = pm.staminaMaxima;

        // Regresar al personaje a su rotación exacta de antes
        player.transform.rotation = rotacionOriginal;

        // Quitar la taza y apagar el interruptor del Animator
        if (_tazaInstanciada != null) Destroy(_tazaInstanciada);
        _tazaInstanciada = null;

        if (player.anim != null)
            player.anim.SetBool("TomandoCafe", false);

        // Desbloquear movimiento
        pm.movimientoBloqueado = false;

        _enUso = false;

        Debug.Log("CoffeeMachine: ✅ ¡Energía restaurada al 100%!");
    }

    /// <summary>
    /// Busca un hijo por nombre en toda la jerarquía (sin importar cuántos
    /// niveles de profundidad, ni cómo se llame el padre). Se usa para
    /// encontrar la mano sin depender del nombre exacto de la pelvis,
    /// que puede variar entre personajes ("base" vs "Base").
    /// </summary>
    Transform BuscarHijoPorNombre(Transform padre, string nombre)
    {
        foreach (Transform hijo in padre)
        {
            if (hijo.name == nombre) return hijo;

            Transform encontrado = BuscarHijoPorNombre(hijo, nombre);
            if (encontrado != null) return encontrado;
        }
        return null;
    }
}