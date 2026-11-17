using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Evita que los personajes se suban a las mesas.
///
/// 1. Los personajes atraviesan los objetos sueltos (piezas, laptops, extintor): así ya no
///    pueden usarlos de escalón o rampa. Agarrarlos sigue igual, porque la detección para
///    recoger usa BoxCast/OverlapBox, que no depende de los choques físicos.
///    El "ignorar choque" se pierde cuando un collider se apaga (pasa cada vez que se
///    recoge un objeto), por eso se vuelve a aplicar continuamente.
///
/// 2. Red de seguridad: si aun así un personaje queda parado encima de una mesa o del bote,
///    se le baja al punto de suelo libre más cercano.
///
/// PlayerMovement lo agrega solo; no hace falta ponerlo en los prefabs.
/// </summary>
[DisallowMultipleComponent]
public class AntiEscalada : MonoBehaviour
{
    [Header("Atravesar objetos sueltos")]
    [Tooltip("Cada cuántos segundos se vuelve a aplicar el 'no chocar' con los objetos sueltos")]
    public float intervaloRefresco = 0.1f;

    [Header("Red de seguridad")]
    [Tooltip("Segundos que puede estar encima de una mesa antes de bajarlo")]
    public float tiempoEncimaAntesDeBajar = 0.3f;
    [Tooltip("Hasta qué distancia busca suelo libre para bajarlo")]
    public float radioBusquedaSuelo = 4f;

    // ── Compartido entre todos los personajes ────────────────────────────────
    private static readonly List<Collider> _collidersObjetos = new List<Collider>();
    private static float _ultimaBusquedaObjetos = -1f;

    private Collider[] _misColliders;
    private Rigidbody _rb;
    private float _proximoRefresco;
    private float _tiempoEncima;
    private readonly RaycastHit[] _hits = new RaycastHit[16];
    private readonly Collider[] _overlap = new Collider[16];

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _misColliders = GetComponentsInChildren<Collider>(true);
    }

    void FixedUpdate()
    {
        if (Time.time >= _proximoRefresco)
        {
            _proximoRefresco = Time.time + intervaloRefresco;
            IgnorarObjetosSueltos();
        }

        RevisarSiEstaEncimaDeMesa();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1. Atravesar objetos sueltos
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Lista de colliders de objetos sueltos; se busca una sola vez por refresco para todos los jugadores.</summary>
    static List<Collider> CollidersObjetos(float intervalo)
    {
        if (Time.time - _ultimaBusquedaObjetos < intervalo * 0.5f && _ultimaBusquedaObjetos >= 0f)
            return _collidersObjetos;

        _ultimaBusquedaObjetos = Time.time;
        _collidersObjetos.Clear();
        foreach (PickableItem p in FindObjectsByType<PickableItem>()) Agregar(p);
        foreach (LaptopController l in FindObjectsByType<LaptopController>()) Agregar(l);
        foreach (Extinguisher e in FindObjectsByType<Extinguisher>()) Agregar(e);
        return _collidersObjetos;
    }

    static void Agregar(Component c)
    {
        if (c == null) return;
        foreach (Collider col in c.GetComponentsInChildren<Collider>())
            if (!_collidersObjetos.Contains(col)) _collidersObjetos.Add(col);
    }

    void IgnorarObjetosSueltos()
    {
        List<Collider> objetos = CollidersObjetos(intervaloRefresco);
        foreach (Collider mio in _misColliders)
        {
            if (!Activo(mio) || mio.isTrigger) continue;
            foreach (Collider obj in objetos)
                if (Activo(obj) && !obj.isTrigger)
                    Physics.IgnoreCollision(mio, obj, true);
        }
    }

    // IgnoreCollision da error si alguno de los dos está apagado
    static bool Activo(Collider c) => c != null && c.enabled && c.gameObject.activeInHierarchy;

    // ═══════════════════════════════════════════════════════════════════════
    // 2. Red de seguridad: bajarlo si quedó encima de una mesa
    // ═══════════════════════════════════════════════════════════════════════

    void RevisarSiEstaEncimaDeMesa()
    {
        if (_rb == null || _rb.isKinematic) { _tiempoEncima = 0f; return; }

        if (!EstaEncimaDeMesa(out float piesY))
        {
            _tiempoEncima = 0f;
            return;
        }

        _tiempoEncima += Time.fixedDeltaTime;
        if (_tiempoEncima < tiempoEncimaAntesDeBajar) return;
        _tiempoEncima = 0f;

        if (BuscarSueloLibre(piesY, out Vector3 suelo))
        {
            Vector3 destino = suelo + Vector3.up * (transform.position.y - piesY + 0.02f);
            _rb.position = destino;
            transform.position = destino;
            _rb.linearVelocity = Vector3.zero;
            Debug.Log($"AntiEscalada: {name} estaba encima de una mesa, se bajó al suelo.");
        }
    }

    /// <summary>¿Lo que tiene justo debajo de los pies es una mesa (o el bote)?</summary>
    bool EstaEncimaDeMesa(out float piesY)
    {
        Bounds b = Limites();
        piesY = b.min.y;
        Vector3 origen = new Vector3(b.center.x, piesY + 0.3f, b.center.z);

        int n = Physics.RaycastNonAlloc(origen, Vector3.down, _hits, 0.6f, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue;
        Collider debajo = null;
        for (int i = 0; i < n; i++)
        {
            Collider c = _hits[i].collider;
            if (c == null || EsMio(c) || EsObjetoSuelto(c)) continue;
            if (_hits[i].distance < mejor) { mejor = _hits[i].distance; debajo = c; }
        }
        return debajo != null && EsMesa(debajo);
    }

    /// <summary>Busca en anillos alrededor el punto de suelo (más bajo que los pies, que no sea mesa) donde quepa el personaje.</summary>
    bool BuscarSueloLibre(float piesY, out Vector3 punto)
    {
        Bounds b = Limites();
        Vector3 medio = new Vector3(b.extents.x, b.extents.y, b.extents.z);
        const int DIRECCIONES = 16;

        for (float r = 0.5f; r <= radioBusquedaSuelo; r += 0.35f)
        {
            for (int i = 0; i < DIRECCIONES; i++)
            {
                float ang = (i / (float)DIRECCIONES) * Mathf.PI * 2f;
                Vector3 xz = new Vector3(b.center.x + Mathf.Cos(ang) * r, 0f, b.center.z + Mathf.Sin(ang) * r);
                Vector3 desde = new Vector3(xz.x, piesY + 0.5f, xz.z);

                if (!SueloDebajo(desde, out RaycastHit hit)) continue;
                if (EsMesa(hit.collider) || hit.normal.y < 0.7f) continue;
                if (hit.point.y > piesY - 0.4f) continue; // tiene que ser más abajo (el suelo, no otra mesa)

                // ¿Cabe ahí? (caja del personaje un poco levantada para no tocar el suelo)
                Vector3 centro = hit.point + Vector3.up * (medio.y + 0.05f);
                if (HayAlgoSolido(centro, medio * 0.95f)) continue;

                punto = hit.point;
                return true;
            }
        }
        punto = Vector3.zero;
        return false;
    }

    bool SueloDebajo(Vector3 desde, out RaycastHit mejorHit)
    {
        mejorHit = default;
        int n = Physics.RaycastNonAlloc(desde, Vector3.down, _hits, 6f, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue;
        bool hay = false;
        for (int i = 0; i < n; i++)
        {
            Collider c = _hits[i].collider;
            if (c == null || EsMio(c) || EsObjetoSuelto(c) || EsOtroJugador(c)) continue;
            if (_hits[i].distance < mejor) { mejor = _hits[i].distance; mejorHit = _hits[i]; hay = true; }
        }
        return hay;
    }

    bool HayAlgoSolido(Vector3 centro, Vector3 medio)
    {
        int n = Physics.OverlapBoxNonAlloc(centro, medio, _overlap, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = _overlap[i];
            if (c == null || EsMio(c) || EsObjetoSuelto(c)) continue;
            return true;
        }
        return false;
    }

    // ── Utilidades ───────────────────────────────────────────────────────────

    Bounds Limites()
    {
        bool hay = false;
        Bounds b = new Bounds(transform.position, Vector3.one * 0.5f);
        foreach (Collider c in _misColliders)
        {
            if (!Activo(c) || c.isTrigger) continue;
            if (!hay) { b = c.bounds; hay = true; }
            else b.Encapsulate(c.bounds);
        }
        return b;
    }

    bool EsMio(Collider c) => c.transform.IsChildOf(transform);

    static bool EsMesa(Collider c) =>
        c.GetComponentInParent<InteractableTable>() != null || c.GetComponentInParent<WasteBin>() != null;

    static bool EsObjetoSuelto(Collider c) =>
        c.GetComponentInParent<PickableItem>() != null ||
        c.GetComponentInParent<LaptopController>() != null ||
        c.GetComponentInParent<Extinguisher>() != null;

    static bool EsOtroJugador(Collider c) => c.GetComponentInParent<PlayerInteraction>() != null;
}
