using UnityEngine;

public class SlotTable : InteractableTable 
{
    [Header("Estado del Slot de Llegada")]
    public GameObject currentItemOnTable; // La laptop que trajo la banda
    public Transform tableSocket; // El punto físico donde la banda "tira" la laptop

    public override void Interact(PlayerInteraction player)
    {
        // Chequeo 1: ¿La mesa tiene la laptop registrada?
        if (currentItemOnTable == null) return;

        // Chequeo 2: ¿El jugador tiene las manos libres?
        if (player.isHoldingItem) return;

        // Chequeo 3: ¿El enchufe (Socket) de la mano existe?
        if (player.itemSocket == null) return;

        // 1. Asignamos variables
        player.isHoldingItem = true;
        player.heldItem = currentItemOnTable;
        
        if (player.anim != null) player.anim.SetBool("isHolding", true);
        player.AjustarCollider(true);

        // 2. Emparentar preservando la escala visual
        Vector3 escalaOriginal = currentItemOnTable.transform.lossyScale;
        currentItemOnTable.transform.SetParent(player.itemSocket);
        currentItemOnTable.transform.localPosition = Vector3.zero;
        currentItemOnTable.transform.localRotation = Quaternion.identity;
        currentItemOnTable.transform.localScale = new Vector3(
            escalaOriginal.x / player.itemSocket.lossyScale.x,
            escalaOriginal.y / player.itemSocket.lossyScale.y,
            escalaOriginal.z / player.itemSocket.lossyScale.z);

        // 3. Apagamos físicas
        if (currentItemOnTable.GetComponent<Rigidbody>() != null)
            currentItemOnTable.GetComponent<Rigidbody>().isKinematic = true;
        if (currentItemOnTable.GetComponent<Collider>() != null)
            currentItemOnTable.GetComponent<Collider>().enabled = false;
        if (currentItemOnTable.GetComponent<LaptopController>() != null)
            currentItemOnTable.GetComponent<LaptopController>().enabled = false;

        // 4. Avisar al sistema de incendios
        Laptop laptopScript = currentItemOnTable.GetComponent<Laptop>();
        if (laptopScript != null) laptopScript.OnRecogida(player);

        // 5. Vaciamos la mesa
        currentItemOnTable = null;
    }

    /// <summary>Llamado por la banda al empujar una laptop nueva a este slot.</summary>
    public bool ReceiveItemFromConveyor(GameObject incomingLaptop)
    {
        if (currentItemOnTable == null)
        {
            currentItemOnTable = incomingLaptop;
            currentItemOnTable.transform.SetParent(tableSocket);
            currentItemOnTable.transform.localPosition = Vector3.zero;
            currentItemOnTable.transform.localRotation = Quaternion.identity;
            
            return true;
        }
        
        return false; 
    }

    /// <summary>Resetea el slot al iniciar un nivel nuevo (borra referencia a laptop de la sesión anterior).</summary>
    public void LimpiarParaNuevoNivel()
    {
        currentItemOnTable = null;
    }
}