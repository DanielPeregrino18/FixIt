using UnityEngine;

/// <summary>
/// Datos y configuración del extintor. La lógica de USO (el cono de efecto)
/// vive en PlayerInteraction, este script solo guarda el estado de carga.
/// </summary>
public class Extinguisher : MonoBehaviour
{
    [Header("Carga")]
    public float cargaMaxima      = 100f;
    public float cargaActual      = 100f;
    [Tooltip("Cuánta carga se consume por segundo mientras se usa")]
    public float gastoPorSegundo  = 25f;

    [Header("Área de Efecto")]
    [Tooltip("Distancia máxima a la que alcanza el chorro")]
    public float radioEfecto = 4f;
    [Tooltip("Medio-ángulo del cono de efecto, en grados")]
    [Range(5f, 60f)]
    public float anguloEfecto = 25f;

    [Header("Efecto Visual (opcional, se conecta cuando tengas la partícula)")]
    public ParticleSystem espumaParticles;

    // ─────────────────────────────────────────────────────────────────────────

    public bool TieneCarga() => cargaActual > 0f;

    public float PorcentajeCarga() => cargaActual / cargaMaxima;

    public void Consumir(float deltaTiempo)
    {
        cargaActual -= gastoPorSegundo * deltaTiempo;
        cargaActual = Mathf.Clamp(cargaActual, 0f, cargaMaxima);
    }

    public void Recargar()
    {
        cargaActual = cargaMaxima;
        Debug.Log("Extinguisher: recargado al 100%.");
    }

    /// <summary>Suma una cantidad de carga (usado por la estación, carga progresiva).</summary>
    public void Cargar(float cantidad)
    {
        cargaActual = Mathf.Clamp(cargaActual + cantidad, 0f, cargaMaxima);
    }
}