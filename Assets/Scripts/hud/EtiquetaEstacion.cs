using UnityEngine;

/// <summary>
/// Letrero flotante encima de una estación (almacén de RAM, HDD, CPU, etc.).
/// Muestra un ícono que mira hacia la cámara y sube y baja suavemente.
///
/// Uso: agrégalo al objeto de la estación, arrastra el sprite en "Icono" y ajusta
/// altura / tamaño. No necesita Canvas: crea un SpriteRenderer por su cuenta.
/// </summary>
public class EtiquetaEstacion : MonoBehaviour
{
    [Header("Icono")]
    [Tooltip("Sprite que se muestra sobre la estación")]
    public Sprite icono;
    [Tooltip("Alto del ícono en unidades del mundo (sin importar el tamaño del PNG)")]
    public float tamano = 1f;

    [Header("Posición")]
    [Tooltip("Qué tan arriba del objeto aparece")]
    public float altura = 2f;
    [Tooltip("Desplazamiento extra (X, Y, Z del mundo), por si el centro del objeto no es el ideal")]
    public Vector3 desplazamiento = Vector3.zero;

    [Header("Orientación")]
    [Tooltip("Rotación extra en grados para corregir la inclinación del ícono respecto a la cámara")]
    public Vector3 rotacionExtra = Vector3.zero;

    [Header("Flotado")]
    [Tooltip("Cuánto sube y baja (unidades del mundo). 0 = quieto")]
    public float amplitudFlotado = 0.1f;
    public float velocidadFlotado = 2f;
    [Tooltip("Desfasa el ciclo para que varias etiquetas no suban todas a la vez")]
    public float desfaseInicial = 0f;

    [Header("Dibujado")]
    [Tooltip("Súbelo si otro sprite o ícono tapa a este")]
    public int ordenDeDibujado = 0;

    private Transform      _etiqueta;
    private SpriteRenderer _renderer;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        GameObject obj = new GameObject("Etiqueta_" + gameObject.name);
        _etiqueta = obj.transform;

        _renderer = obj.AddComponent<SpriteRenderer>();
        _renderer.sortingOrder = ordenDeDibujado;
        AplicarSprite();
    }

    void OnEnable()
    {
        if (_etiqueta != null)
        {
            _etiqueta.gameObject.SetActive(true);
            Actualizar();
        }
    }

    void OnDisable()
    {
        if (_etiqueta != null) _etiqueta.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_etiqueta != null) Destroy(_etiqueta.gameObject);
    }

    void LateUpdate()
    {
        Actualizar();
    }

    // ─────────────────────────────────────────────────────────────────────────

    void AplicarSprite()
    {
        if (_renderer == null) return;

        _renderer.sprite = icono;
        _renderer.sortingOrder = ordenDeDibujado;

        // Escalar para que el alto del ícono mida "tamano" unidades, sin importar el PNG
        if (icono != null && icono.bounds.size.y > 0.0001f)
        {
            float escala = tamano / icono.bounds.size.y;
            _etiqueta.localScale = new Vector3(escala, escala, escala);
        }
    }

    void Actualizar()
    {
        if (_etiqueta == null) return;

        if (_renderer.sprite != icono) AplicarSprite();

        float flotado = Mathf.Sin((Time.time + desfaseInicial) * velocidadFlotado) * amplitudFlotado;
        Vector3 posicion = transform.position + Vector3.up * (altura + flotado) + desplazamiento;
        _etiqueta.position = posicion;

        // Mirar hacia la cámara (cámara fija: mismo método que los íconos de entrega)
        Camera cam = Camera.main;
        if (cam != null)
            _etiqueta.rotation = Quaternion.LookRotation(posicion - cam.transform.position)
                                 * Quaternion.Euler(rotacionExtra);
    }

    /// <summary>Muestra en el Scene una esfera donde aparecerá el ícono (al seleccionar el objeto).</summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * altura + desplazamiento, tamano * 0.5f);
    }
}
