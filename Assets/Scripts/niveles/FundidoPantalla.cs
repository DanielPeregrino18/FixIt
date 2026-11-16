using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Fundido a negro de pantalla completa. Usa tiempo real (unscaled), así que funciona
/// aunque el juego esté en pausa (timeScale 0).
/// Va en un Canvas propio (Screen Space - Overlay, Sort Order alto) que contiene una
/// Image negra a pantalla completa y un CanvasGroup.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class FundidoPantalla : MonoBehaviour
{
    public static FundidoPantalla Instance { get; private set; }

    [Header("Duraciones (segundos)")]
    [Tooltip("Al iniciar un nivel: de negro a la escena")]
    public float duracionEntradaNivel = 0.5f;
    [Tooltip("Fundido a negro estándar")]
    public float duracionSalida = 0.3f;

    private CanvasGroup _grupo;
    private Coroutine   _corrutina;

    /// <summary>true mientras la pantalla está (casi) completamente negra.</summary>
    public bool EstaOscuro => _grupo != null && _grupo.alpha >= 0.99f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _grupo = GetComponent<CanvasGroup>();
        _grupo.alpha = 0f;
        _grupo.blocksRaycasts = false;
        _grupo.interactable   = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Pantalla negra de golpe, sin animación.</summary>
    public void NegroInstantaneo()
    {
        Detener();
        _grupo.alpha = 1f;
    }

    /// <summary>De negro a transparente. duracion &lt;= 0 usa la duración de entrada de nivel.</summary>
    public void Aclarar(float duracion = -1f, Action alTerminar = null)
    {
        if (duracion <= 0f) duracion = duracionEntradaNivel;
        Animar(0f, duracion, alTerminar);
    }

    /// <summary>De transparente a negro. duracion &lt;= 0 usa la duración de salida estándar.</summary>
    public void Oscurecer(float duracion = -1f, Action alTerminar = null)
    {
        if (duracion <= 0f) duracion = duracionSalida;
        Animar(1f, duracion, alTerminar);
    }

    /// <summary>Quita el negro de golpe (por ejemplo al salir al menú).</summary>
    public void TransparenteInstantaneo()
    {
        Detener();
        _grupo.alpha = 0f;
    }

    // ── Internos ──────────────────────────────────────────────────────────────

    void Detener()
    {
        if (_corrutina != null) { StopCoroutine(_corrutina); _corrutina = null; }
    }

    void Animar(float destino, float duracion, Action alTerminar)
    {
        Detener();
        _corrutina = StartCoroutine(AnimarAlpha(destino, duracion, alTerminar));
    }

    IEnumerator AnimarAlpha(float destino, float duracion, Action alTerminar)
    {
        float inicio = _grupo.alpha;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            _grupo.alpha = Mathf.Lerp(inicio, destino, Mathf.Clamp01(t / duracion));
            yield return null;
        }
        _grupo.alpha = destino;
        _corrutina = null;
        alTerminar?.Invoke();
    }
}
