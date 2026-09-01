using UnityEngine;

public class HardwareDispenser : InteractableTable
{
    [Header("Configuración del Almacén")]
    public GameObject piezaPrefab;

    [Header("Punto de spawn de la pieza")]
    public Transform spawnPoint;

    public override void Interact(PlayerInteraction player)
    {
        if (player.isHoldingItem)
        {
            Debug.Log("HardwareDispenser: El jugador ya carga algo.");
            return;
        }

        if (piezaPrefab == null)
        {
            Debug.LogWarning("HardwareDispenser: No hay prefab asignado.");
            return;
        }

        DarPieza(player);
    }

    void DarPieza(PlayerInteraction player)
    {
        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        GameObject pieza = Instantiate(piezaPrefab, pos, Quaternion.identity);

        // Apagar físicas antes de emparentar
        if (pieza.GetComponent<Rigidbody>() != null)
            pieza.GetComponent<Rigidbody>().isKinematic = true;
        if (pieza.GetComponent<Collider>() != null)
            pieza.GetComponent<Collider>().enabled = false;

        // FIX: preservar escala visual al cambiar de padre
        Vector3 escalaOriginal = pieza.transform.lossyScale;
        pieza.transform.SetParent(player.itemSocket);
        pieza.transform.localPosition = Vector3.zero;
        pieza.transform.localRotation = Quaternion.identity;
        pieza.transform.localScale = new Vector3(
            escalaOriginal.x / player.itemSocket.lossyScale.x,
            escalaOriginal.y / player.itemSocket.lossyScale.y,
            escalaOriginal.z / player.itemSocket.lossyScale.z
        );

        player.isHoldingItem = true;
        player.heldItem      = pieza;
        if (player.anim != null) player.anim.SetBool("isHolding", true);
        player.AjustarCollider(true);

        Debug.Log("HardwareDispenser: Pieza entregada al jugador.");
    }
}