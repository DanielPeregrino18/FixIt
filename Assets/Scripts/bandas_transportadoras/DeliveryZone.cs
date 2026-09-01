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
        player.heldItem      = null;
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
            rb.isKinematic    = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Activar collider para que la banda la detecte
        if (laptop.GetComponent<Collider>() != null)
            laptop.GetComponent<Collider>().enabled = true;

        // Destruir después de unos segundos cuando salga de escena
        Destroy(laptop, tiempoDestruccion);
    }

    protected override void UpdateVisuals()
    {
        if (tableRenderer == null) return;

        if (_playersLooking.Count > 0)
        {
            tableRenderer.material.EnableKeyword("_EMISSION");
            tableRenderer.material.SetColor("_EmissionColor", 
                Color.white * 0.15f);
        }
        else
        {
            tableRenderer.material.DisableKeyword("_EMISSION");
        }
    }
}
