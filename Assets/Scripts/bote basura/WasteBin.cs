using System.Collections;
using UnityEngine;

public class WasteBin : MonoBehaviour
{
    [Header("Posición encima del bote donde llega la laptop")]
    public Transform puntoLlegada;

    [Tooltip("Velocidad de deslizamiento hacia el bote")]
    public float velocidadDeslizamiento = 3f;

    [Tooltip("Segundos antes de destruir la laptop una vez que cayó")]
    public float tiempoDestruccion = 2f;

    // ─────────────────────────────────────────────────────────────────────────

    public void RecibirLaptop(GameObject laptop)
    {
        if (laptop == null) return;

        // Desemparentar del slot actual
        laptop.transform.SetParent(null);
        laptop.transform.localScale = Vector3.one;

        // Apagar físicas para controlar el movimiento manualmente
        Rigidbody rb = laptop.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        // Desactivar collider — ya no importa, la laptop va a desaparecer
        if (laptop.GetComponent<Collider>() != null)
            laptop.GetComponent<Collider>().enabled = false;

        // Notificar al GameManager
        if (GameManager.Instance != null)
            GameManager.Instance.RegistrarLaptopPerdida();

        // Iniciar deslizamiento
        StartCoroutine(DeslizarAlBote(laptop));

        Debug.Log("WasteBin: Laptop perdida en el bote de desechos.");
    }

    IEnumerator DeslizarAlBote(GameObject laptop)
    {
        if (laptop == null) yield break;

        // Fase 1 — deslizar hasta el punto encima del bote
        while (laptop != null &&
               Vector3.Distance(laptop.transform.position, puntoLlegada.position) > 0.05f)
        {
            laptop.transform.position = Vector3.MoveTowards(
                laptop.transform.position,
                puntoLlegada.position,
                velocidadDeslizamiento * Time.deltaTime);
            yield return null;
        }

        if (laptop == null) yield break;

        // Fase 2 — activar gravedad para que caiga dentro del bote
        // El collider sigue desactivado para que no choque con nada
        Rigidbody rb = laptop.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic    = false;
            rb.linearVelocity = Vector3.zero;
        }

        // Destruir después de que caiga
        Destroy(laptop, tiempoDestruccion);
    }
}