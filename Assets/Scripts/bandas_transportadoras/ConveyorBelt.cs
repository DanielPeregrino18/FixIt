using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    [Header("Configuración Física")]
    public float speed = 2f;
    public Vector3 direction = Vector3.forward; 

    [Header("Configuración del Salto Visual")]
    [Tooltip("Qué tan rápido da el salto de .01 en segundos")]
    public float tiempoEntreSaltos = 0.05f; 
    
    private MeshRenderer meshRenderer;
    private Material beltMaterial;
    
    private float currentOffset = 0f;
    private float timer = 0f;

    void Start()
    {
        // CAMBIO CRÍTICO AQUÍ: Le decimos que busque en los objetos "hijos"
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        
        if (meshRenderer != null)
        {
            beltMaterial = meshRenderer.material;
        }
        else
        {
            // Trampa de error para la consola de Unity
            Debug.LogError("¡No se encontró el MeshRenderer! Revisa que el modelo 3D sea hijo de este objeto.");
        }
    }

 void Update()
    {
        if (beltMaterial != null)
        {
            timer += Time.deltaTime;

            if (timer >= tiempoEntreSaltos)
            {
                currentOffset += 0.01f; 

                // --- CAMBIO AQUÍ: Agregamos un signo '-' antes del offset ---
                // De esta forma, aunque el número crece (1, 2, 3...), 
                // para el material el valor que le llega es (-1, -2, -3...),
                // invirtiendo la dirección visual.
                if (beltMaterial.HasProperty("_BaseMap"))
                {
                    beltMaterial.SetTextureOffset("_BaseMap", new Vector2(0, -currentOffset));
                }
                
                if (beltMaterial.HasProperty("_MainTex"))
                {
                    beltMaterial.SetTextureOffset("_MainTex", new Vector2(0, -currentOffset));
                }
                // ------------------------------------------------------------

                timer = 0f; 
            }
        }
    }

    void OnTriggerStay(Collider other)
    {
        Rigidbody rb = other.GetComponent<Rigidbody>();

        if (rb != null)
        {
            Vector3 movement = transform.TransformDirection(direction) * speed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + movement);
        }
    }
}