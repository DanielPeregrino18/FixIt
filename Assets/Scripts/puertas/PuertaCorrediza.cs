using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puerta corrediza con sensor de movimiento: se abre rápido cuando un personaje se acerca
/// y se cierra sola cuando ya no hay nadie cerca.
///
/// Agrégala al objeto de la puerta (la hoja que se mueve). Se desliza sobre su eje Z local,
/// una distancia igual a su ancho (lo toma del BoxCollider). Selecciónala en el editor para ver
/// en verde la zona del sensor y en azul dónde queda abierta.
///
/// Si "Solo En Niveles Con Servidores" está activo, la puerta queda bloqueada (es una pared de
/// vidrio) en los niveles cuyo JSON no trae servidores. Eso lo decide WaveManager al cargar el nivel.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class PuertaCorrediza : MonoBehaviour
{
    [Header("Bloqueo")]
    [Tooltip("Activado = solo se abre en los niveles que tienen servidores que apagar (puerta del cuarto de servidores). " +
             "Desactivado = funciona en todos los niveles (p. ej. la del cuarto de almacenes)")]
    public bool soloEnNivelesConServidores = false;
    [Tooltip("Activado = solo se abre en los niveles que piden piezas (laptops que no son 'fácil') o tienen incendios " +
             "(puerta del cuarto de almacenes: ahí están las piezas y el extintor)")]
    public bool soloEnNivelesConPiezasOIncendios = false;

    [Header("Movimiento")]
    [Tooltip("Hacia qué lado se recorre: 1 o -1 sobre su eje Z local (la flecha azul de la puerta). Pon el mismo valor en las dos puertas")]
    public float ladoApertura = 1f;
    [Tooltip("Metros que se recorre al abrir. 0 = automático (el ancho de la puerta)")]
    public float distanciaApertura = 0f;
    [Tooltip("Segundos que tarda en abrirse (rápido)")]
    public float tiempoAbrir = 0.2f;
    [Tooltip("Segundos que tarda en cerrarse")]
    public float tiempoCerrar = 0.45f;
    [Tooltip("Segundos que espera sin nadie cerca antes de empezar a cerrarse")]
    public float esperaAntesDeCerrar = 0.4f;

    [Header("Sensor de movimiento")]
    [Tooltip("Qué tan lejos de la puerta (a cada lado) detecta a un personaje")]
    public float alcanceSensor = 2f;
    [Tooltip("Margen extra del sensor a lo ancho de la puerta")]
    public float margenLateralSensor = 0.5f;

    // ── Estado ────────────────────────────────────────────────────────────────
    private static readonly List<PuertaCorrediza> _todas = new List<PuertaCorrediza>();
    private static bool _nivelConServidores = false;
    private static bool _nivelConPiezasOIncendios = false;

    private BoxCollider _col;
    private Rigidbody _rb;
    private Vector3 _posCerrada;   // posición local cerrada
    private Vector3 _desplazamiento; // de cerrada a abierta, en el espacio del padre
    private float _apertura;       // 0 = cerrada, 1 = abierta
    private float _tiempoSinNadie;
    private readonly Collider[] _buffer = new Collider[32];

    /// <summary>¿Puede abrirse en el nivel actual?</summary>
    public bool Habilitada =>
        (!soloEnNivelesConServidores || _nivelConServidores) &&
        (!soloEnNivelesConPiezasOIncendios || _nivelConPiezasOIncendios);

    // ═══════════════════════════════════════════════════════════════════════
    // API para WaveManager
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Al iniciar un nivel: decide qué puertas funcionan y las cierra todas al instante.</summary>
    public static void ConfigurarNivel(bool hayServidores, bool hayPiezasOIncendios)
    {
        _nivelConServidores = hayServidores;
        _nivelConPiezasOIncendios = hayPiezasOIncendios;
        foreach (PuertaCorrediza p in _todas)
            if (p != null) p.CerrarAlInstante();
    }

    // ═══════════════════════════════════════════════════════════════════════

    void Awake()
    {
        _col = GetComponent<BoxCollider>();

        // Rigidbody kinemático: así la puerta se mueve bien entre las físicas (empuja en vez de atravesar)
        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        _posCerrada = transform.localPosition;
        _desplazamiento = CalcularDesplazamiento();
    }

    void OnEnable() { if (!_todas.Contains(this)) _todas.Add(this); }
    void OnDisable() { _todas.Remove(this); }

    Vector3 CalcularDesplazamiento()
    {
        BoxCollider col = _col != null ? _col : GetComponent<BoxCollider>();
        float ancho = distanciaApertura > 0f
            ? distanciaApertura
            : (col != null ? col.size.z * Mathf.Abs(transform.localScale.z) : 2f);
        // Eje Z local de la puerta expresado en el espacio de su padre
        Vector3 eje = transform.localRotation * Vector3.forward;
        return eje.normalized * ancho * Mathf.Sign(ladoApertura == 0f ? 1f : ladoApertura);
    }

    void FixedUpdate()
    {
        bool hayAlguien = Habilitada && HayAlguienCerca();

        if (hayAlguien) _tiempoSinNadie = 0f;
        else _tiempoSinNadie += Time.fixedDeltaTime;

        bool abrir = hayAlguien || (_apertura > 0f && _tiempoSinNadie < esperaAntesDeCerrar);
        // Nunca cerrarse encima de un objeto suelto que quedó en el umbral
        if (!abrir && _apertura > 0f && HayObjetoEnElUmbral()) abrir = true;

        float objetivo = abrir ? 1f : 0f;
        float velocidad = 1f / Mathf.Max(0.05f, abrir ? tiempoAbrir : tiempoCerrar);
        _apertura = Mathf.MoveTowards(_apertura, objetivo, velocidad * Time.fixedDeltaTime);

        MoverA(_apertura);
    }

    void MoverA(float apertura)
    {
        float suave = apertura * apertura * (3f - 2f * apertura); // arranca y frena suave
        Vector3 local = _posCerrada + _desplazamiento * suave;
        Vector3 mundo = transform.parent != null ? transform.parent.TransformPoint(local) : local;
        if (_rb != null) _rb.MovePosition(mundo);
        else transform.localPosition = local;
    }

    void CerrarAlInstante()
    {
        _apertura = 0f;
        _tiempoSinNadie = esperaAntesDeCerrar;
        transform.localPosition = _posCerrada;
        if (_rb != null) _rb.position = transform.position;
    }

    // ── Sensor ────────────────────────────────────────────────────────────────

    /// <summary>Centro y tamaño del sensor en el mundo (sobre la posición cerrada, no se mueve con la hoja).</summary>
    void ZonaSensor(out Vector3 centro, out Vector3 medioTamaño, out Quaternion rot)
    {
        BoxCollider col = _col != null ? _col : GetComponent<BoxCollider>();
        Vector3 escala = transform.lossyScale;
        Vector3 posCerradaMundo = transform.parent != null
            ? transform.parent.TransformPoint(Application.isPlaying ? _posCerrada : transform.localPosition)
            : (Application.isPlaying ? _posCerrada : transform.localPosition);

        rot = transform.rotation;
        Vector3 centroLocal = col != null ? Vector3.Scale(col.center, escala) : Vector3.zero;
        centro = posCerradaMundo + rot * centroLocal;

        float ancho = col != null ? col.size.z * Mathf.Abs(escala.z) : 2f;
        float alto = col != null ? col.size.y * Mathf.Abs(escala.y) : 2f;
        // X local = a través de la puerta (los dos lados), Z local = a lo ancho
        medioTamaño = new Vector3(alcanceSensor, alto * 0.5f, ancho * 0.5f + margenLateralSensor);
    }

    bool HayAlguienCerca()
    {
        ZonaSensor(out Vector3 centro, out Vector3 medio, out Quaternion rot);
        int n = Physics.OverlapBoxNonAlloc(centro, medio, _buffer, rot, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (_buffer[i] != null && _buffer[i].GetComponentInParent<PlayerInteraction>() != null)
                return true;
        return false;
    }

    /// <summary>Objetos sueltos (piezas, laptops, extintor) justo en el hueco de la puerta.</summary>
    bool HayObjetoEnElUmbral()
    {
        BoxCollider col = _col;
        if (col == null) return false;
        ZonaSensor(out Vector3 centro, out _, out Quaternion rot);
        Vector3 escala = transform.lossyScale;
        Vector3 medio = new Vector3(0.4f, col.size.y * Mathf.Abs(escala.y) * 0.5f, col.size.z * Mathf.Abs(escala.z) * 0.5f);

        int n = Physics.OverlapBoxNonAlloc(centro, medio, _buffer, rot, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = _buffer[i];
            if (c == null || c.transform.IsChildOf(transform)) continue;
            if (c.GetComponentInParent<PickableItem>() != null ||
                c.GetComponentInParent<LaptopController>() != null ||
                c.GetComponentInParent<Extinguisher>() != null)
                return true;
        }
        return false;
    }

    // ── Gizmos: ver el sensor y la posición abierta en el editor ──────────────

    void OnDrawGizmosSelected()
    {
        ZonaSensor(out Vector3 centro, out Vector3 medio, out Quaternion rot);

        Gizmos.matrix = Matrix4x4.TRS(centro, rot, Vector3.one);
        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawCube(Vector3.zero, medio * 2f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Vector3.zero, medio * 2f);

        // Dónde queda abierta
        Vector3 desp = Application.isPlaying ? _desplazamiento : CalcularDesplazamiento();
        Vector3 despMundo = transform.parent != null ? transform.parent.TransformVector(desp) : desp;
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            Vector3 tam = Vector3.Scale(col.size, transform.lossyScale);
            Gizmos.matrix = Matrix4x4.TRS(centro + despMundo, rot, Vector3.one);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.9f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(Mathf.Abs(tam.x), Mathf.Abs(tam.y), Mathf.Abs(tam.z)));
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}
