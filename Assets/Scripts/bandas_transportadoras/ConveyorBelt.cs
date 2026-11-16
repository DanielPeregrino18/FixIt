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

    // El desplazamiento de la textura se guarda en un MaterialPropertyBlock (vive en el renderer,
    // no en el material). Así la animación sigue aunque otro script cambie el material, como
    // DeliveryZone al resaltar la banda cuando un jugador la apunta (bandaNormal ↔ BandaGlow).
    private MaterialPropertyBlock bloque;
    private static readonly int ID_BASEMAP_ST = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int ID_MAINTEX_ST = Shader.PropertyToID("_MainTex_ST");

    private float currentOffset = 0f;
    private float timer = 0f;

    void Start()
    {
        // CAMBIO CRÍTICO AQUÍ: Le decimos que busque en los objetos "hijos"
        meshRenderer = GetComponentInChildren<MeshRenderer>();

        if (meshRenderer != null)
        {
            bloque = new MaterialPropertyBlock();
        }
        else
        {
            // Trampa de error para la consola de Unity
            Debug.LogError("¡No se encontró el MeshRenderer! Revisa que el modelo 3D sea hijo de este objeto.");
        }
    }

    void Update()
    {
        if (meshRenderer == null) return;

        timer += Time.deltaTime;
        if (timer < tiempoEntreSaltos) return;
        timer = 0f;

        // Repeat evita que el número crezca sin fin (la textura se repite cada 1.0 igual)
        currentOffset = Mathf.Repeat(currentOffset + 0.01f, 1f);

        // El signo '-' invierte la dirección visual de la textura
        AplicarDesplazamiento(-currentOffset);
    }

    void AplicarDesplazamiento(float offsetY)
    {
        // Se respeta el tiling del material que esté puesto en este momento
        Material mat = meshRenderer.sharedMaterial;
        Vector2 tiling = Vector2.one;
        if (mat != null)
        {
            if (mat.HasProperty(ID_BASEMAP_ST)) tiling = mat.GetTextureScale("_BaseMap");
            else if (mat.HasProperty(ID_MAINTEX_ST)) tiling = mat.GetTextureScale("_MainTex");
        }

        // _ST = (tiling x, tiling y, offset x, offset y)
        Vector4 st = new Vector4(tiling.x, tiling.y, 0f, offsetY);
        meshRenderer.GetPropertyBlock(bloque);
        bloque.SetVector(ID_BASEMAP_ST, st);
        bloque.SetVector(ID_MAINTEX_ST, st);
        meshRenderer.SetPropertyBlock(bloque);
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