using UnityEngine;

/// <summary>
/// Zona de entrega. El jugador deja la laptop aquí con E.
/// Si está completa suma al contador, si no cuenta como perdida.
/// Hereda el brillo de InteractableTable.
/// </summary>
public class DeliveryZone : InteractableTable
{
    [Header("Punto donde se coloca la laptop en la banda")]
    public Transform tableSocket;

    [Tooltip("Segundos antes de destruir la laptop una vez entregada")]
    public float tiempoDestruccion = 4f;

    [Header("Iconos de resultado (aparecen flotando sobre la laptop al entregarla)")]
    public Sprite iconoCorrecto;
    public Sprite iconoIncorrecto;
    [Tooltip("Qué tan arriba de la laptop aparece el ícono")]
    public float alturaIcono = 1f;
    [Tooltip("Tamaño del ícono en pantalla")]
    public float tamanoIcono = 60f;
    [Tooltip("Rotación extra para corregir la inclinación de la palomita")]
    public Vector3 rotacionIconoCorrecto = Vector3.zero;
    [Tooltip("Rotación extra para corregir la inclinación de la tacha")]
    public Vector3 rotacionIconoIncorrecto = Vector3.zero;

    // ─────────────────────────────────────────────────────────────────────────

    public override void Interact(PlayerInteraction player)
    {
        // Solo actúa si el jugador trae algo
        if (!player.isHoldingItem)
        {
            Debug.Log("DeliveryZone: El jugador no trae ninguna laptop.");
            return;
        }

        // Solo acepta laptops, no piezas de hardware
        Laptop laptopScript = player.heldItem.GetComponent<Laptop>();
        if (laptopScript == null)
        {
            Debug.Log("DeliveryZone: Solo se pueden entregar laptops aquí.");
            return;
        }

        EntregarLaptop(player, laptopScript);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void EntregarLaptop(PlayerInteraction player, Laptop laptopScript)
    {
        GameObject laptop = player.heldItem;
        bool estaCompleta = laptopScript.EstaLista();

        // Mostrar la palomita o la tacha flotando sobre la laptop
        Vector3 posicionIcono = tableSocket != null ? tableSocket.position : laptop.transform.position;
        IconoFlotanteTemporal.Mostrar(posicionIcono, estaCompleta ? iconoCorrecto : iconoIncorrecto,
                                      alturaSobrePosicion: alturaIcono, tamano: tamanoIcono,
                                      rotacionExtra: estaCompleta ? rotacionIconoCorrecto : rotacionIconoIncorrecto);

        // Ya quedó registrada: la limpieza de fin de nivel no debe contarla otra vez
        laptopScript.yaEntregada = true;

        // Registrar en el GameManager
        if (GameManager.Instance != null)
        {
            if (estaCompleta)
            {
                GameManager.Instance.RegistrarLaptopEntregada();
                Debug.Log("DeliveryZone: ✅ Laptop entregada correctamente.");
            }
            else
            {
                GameManager.Instance.RegistrarLaptopPerdida();
                Debug.Log("DeliveryZone: ❌ Laptop entregada incompleta — cuenta como perdida.");
            }
        }

        // Soltar la laptop del jugador
        player.isHoldingItem = false;
        player.heldItem = null;
        if (player.anim != null) player.anim.SetBool("isHolding", false);
        player.AjustarCollider(false);

        // Colocar la laptop en la banda
        laptop.transform.SetParent(null);

        if (tableSocket != null)
        {
            laptop.transform.position = tableSocket.position;
            laptop.transform.rotation = tableSocket.rotation;
        }

        laptop.transform.localScale = Vector3.one;

        // Activar físicas para que la banda la mueva
        Rigidbody rb = laptop.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Activar collider para que la banda la detecte
        if (laptop.GetComponent<Collider>() != null)
            laptop.GetComponent<Collider>().enabled = true;

        // Destruir después de unos segundos cuando salga de escena
        Destroy(laptop, tiempoDestruccion);
    }
}