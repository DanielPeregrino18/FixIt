using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Globo de cómic sobre la cabeza de un personaje (carita feliz o triste), sin sonido.
/// Aparece con un "pop", tiembla un poco como onomatopeya, sube ligeramente y se desvanece.
/// Sigue al personaje mientras dura. Las imágenes están en Resources/Globos.
///
/// Uso: GloboReaccion.Mostrar(jugador.transform, bien: true);
/// </summary>
public class GloboReaccion : MonoBehaviour
{
    const float DURACION = 1f;
    const float TAMANO_MUNDO = 0.85f;   // metros de ancho del globo
    const float ALTURA_SOBRE_CABEZA = 1.1f;
    const float SUBIDA = 0.25f;         // cuánto flota hacia arriba mientras dura
    const float DURACION_POP = 0.18f;
    const float INICIO_FADE = 0.75f;    // a partir de qué fracción se desvanece

    static Sprite _feliz, _triste;
    static readonly Dictionary<Transform, GloboReaccion> _activos = new Dictionary<Transform, GloboReaccion>();

    Transform _jugador;
    Image _img;
    float _t;
    float _alturaCabeza;
    float _giroBase;

    /// <summary>Muestra el globo sobre el personaje. Si ya tenía uno, lo reemplaza.</summary>
    public static void Mostrar(Transform jugador, bool bien)
    {
        if (jugador == null) return;
        if (_feliz == null) _feliz = Resources.Load<Sprite>("Globos/globo_feliz");
        if (_triste == null) _triste = Resources.Load<Sprite>("Globos/globo_triste");
        Sprite sprite = bien ? _feliz : _triste;
        if (sprite == null) { Debug.LogWarning("GloboReaccion: faltan las imágenes en Resources/Globos."); return; }

        if (_activos.TryGetValue(jugador, out GloboReaccion previo) && previo != null)
            Destroy(previo.gameObject);

        GameObject go = new GameObject("GloboReaccion");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100f, 100f);

        GameObject imgGo = new GameObject("Imagen", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(go.transform, false);
        RectTransform irt = (RectTransform)imgGo.transform;
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = irt.offsetMax = Vector2.zero;
        Image img = imgGo.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        GloboReaccion g = go.AddComponent<GloboReaccion>();
        g._jugador = jugador;
        g._img = img;
        g._giroBase = bien ? -8f : 8f; // ladeado como onomatopeya
        Collider col = jugador.GetComponent<Collider>();
        g._alturaCabeza = col != null ? col.bounds.max.y - jugador.position.y : 1.8f;
        _activos[jugador] = g;
        g.Actualizar();
    }

    void LateUpdate()
    {
        _t += Time.deltaTime;
        if (_t >= DURACION || _jugador == null) { Destroy(gameObject); return; }
        Actualizar();
    }

    void OnDestroy()
    {
        if (_jugador != null && _activos.TryGetValue(_jugador, out GloboReaccion g) && g == this)
            _activos.Remove(_jugador);
    }

    void Actualizar()
    {
        float p = _t / DURACION;

        // Posición: sobre la cabeza, subiendo un poco
        transform.position = _jugador.position + Vector3.up * (_alturaCabeza + ALTURA_SOBRE_CABEZA + SUBIDA * p);

        // Pop: crece de 0 a 1.2 y se asienta en 1
        float pop = Mathf.Clamp01(_t / DURACION_POP);
        float escala = pop < 1f ? Mathf.Lerp(0f, 1.2f, EaseOut(pop)) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((_t - DURACION_POP) / 0.12f));
        transform.localScale = Vector3.one * (TAMANO_MUNDO / 100f) * escala;

        // Mirando a la cámara, con un temblor que se calma (onomatopeya)
        float temblor = Mathf.Sin(_t * 40f) * 10f * Mathf.Clamp01(1f - _t / 0.45f);
        if (Camera.main != null)
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position, Camera.main.transform.up)
                                 * Quaternion.Euler(0f, 0f, _giroBase + temblor);

        // Se desvanece al final
        float alfa = p < INICIO_FADE ? 1f : 1f - (p - INICIO_FADE) / (1f - INICIO_FADE);
        if (_img != null) { Color c = _img.color; c.a = alfa; _img.color = c; }
    }

    static float EaseOut(float x) => 1f - (1f - x) * (1f - x);
}
