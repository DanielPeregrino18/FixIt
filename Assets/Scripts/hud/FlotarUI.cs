using UnityEngine;

/// <summary>
/// Hace que un elemento de UI (Image, ícono, adorno) suba y baje suavemente
/// en el mismo lugar, en loop infinito. Agrégalo a cualquier decoración del
/// menú principal — cada una puede tener su propia velocidad/altura.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class FlotarUI : MonoBehaviour
{
    [Header("Configuración del movimiento")]
    [Tooltip("Qué tan lejos sube/baja, en píxeles")]
    public float altura = 15f;
    [Tooltip("Qué tan rápido oscila — más alto = más rápido")]
    public float velocidad = 1.5f;
    [Tooltip("Desfasa el punto de inicio del ciclo, para que varias decoraciones no suban/bajen todas exactamente igual al mismo tiempo")]
    public float desfaseInicial = 0f;

    private RectTransform _rect;
    private Vector2 _posicionOriginal;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _posicionOriginal = _rect.anchoredPosition;
    }

    void Update()
    {
        float offsetY = Mathf.Sin((Time.time + desfaseInicial) * velocidad) * altura;
        _rect.anchoredPosition = _posicionOriginal + new Vector2(0f, offsetY);
    }
}
