using UnityEngine;

public class HardwareDispenser : InteractableTable
{
    [Header("Configuración del Almacén")]
    public GameObject piezaPrefab;

    [Header("Punto de spawn de la pieza")]
    public Transform spawnPoint;

    [Header("Límite")]
    [Tooltip("Máximo de piezas de este tipo que puede haber en la escena a la vez (en manos, suelo, mesas o volando). " +
             "Las que ya se instalaron no cuentan. 0 = sin límite")]
    public int maxPiezasEnEscena = 5;

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

        if (LlegoAlLimite())
        {
            Debug.Log($"HardwareDispenser: ya hay {maxPiezasEnEscena} piezas de este tipo en la escena. Usa esas primero.");
            GloboReaccion.Mostrar(player.transform, bien: false);
            return;
        }

        DarPieza(player);
    }

    /// <summary>¿Ya hay el máximo de piezas de este tipo en la escena?</summary>
    bool LlegoAlLimite()
    {
        if (maxPiezasEnEscena <= 0) return false;
        HardwarePiece modelo = piezaPrefab.GetComponent<HardwarePiece>();
        if (modelo == null) return false;

        int cuantas = 0;
        foreach (HardwarePiece p in FindObjectsByType<HardwarePiece>())
            if (p != null && p.tipoPieza == modelo.tipoPieza) cuantas++;
        return cuantas >= maxPiezasEnEscena;
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
        player.heldItem = pieza;
        if (player.anim != null) player.anim.SetBool("isHolding", true);
        player.AjustarCollider(true);

        AudioManager.Expendedora();

        Debug.Log("HardwareDispenser: Pieza entregada al jugador.");
    }
}