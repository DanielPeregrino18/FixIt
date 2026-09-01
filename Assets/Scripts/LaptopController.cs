using UnityEngine;

public class LaptopController : MonoBehaviour
{
    private Vector3 targetPosition;
    private bool isMoving = false;
    public bool isInteractable = false; 
    
    // NUEVO: Bandera para saber si ya pasó por el sensor
    public bool yaLlego = false; 
    
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void MoverASlot(Vector3 nuevaPosicion)
    {
        yaLlego = true; // Bloqueamos para que el sensor no la vuelva a registrar

        targetPosition = nuevaPosicion;
        isMoving = true;
        isInteractable = false; 
        rb.isKinematic = true; 
    }

    void Update()
    {
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, 5f * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                isMoving = false;
                isInteractable = true; 
            }
        }
    }
}