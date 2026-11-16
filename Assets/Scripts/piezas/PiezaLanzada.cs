using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vuelo de un objeto lanzado (piezas RAM, HDD, CPU y el extintor). Se agrega solo al lanzar y se quita al aterrizar.
///
/// La trayectoria se calcula ANTES de lanzar (ver <see cref="CalcularTrayectoria"/>): rebota en
/// paredes, mesas y bote, y siempre termina en suelo libre. Así nunca cae sobre una mesa ni fuera
/// del mapa. Durante el vuelo la pieza es kinemática y sin collider; un compañero con las manos
/// libres y de frente puede atraparla.
/// </summary>
public class PiezaLanzada : MonoBehaviour
{
    // ── Datos del vuelo ───────────────────────────────────────────────────────
    private List<Vector3> _ruta;        // puntos en planta (la Y se ignora)
    private float _largo;
    private Vector3 _inicio;            // posición en la mano al lanzar
    private float _alturaFinal;         // Y donde aterriza
    private float _alturaArco;
    private float _duracion;
    private float _t;
    private bool _aterrizada;
    private float _tiempoVigilancia;
    private float _sueloY;

    private PlayerInteraction _lanzador;
    private PlayerInteraction[] _jugadores;
    private float _radioAtrapar;
    private float _anguloAtrapar;
    private Vector3 _ejeGiro;
    private Quaternion _rotacionInicial;

    const float VIGILAR_TRAS_ATERRIZAR = 2f;   // segundos que revisa que no se caiga del mapa
    const float SEPARACION_PARED = 0.3f;       // qué tan lejos de la pared queda el punto de rebote
    const float PASO_RETROCESO = 0.25f;        // al buscar suelo libre, cuánto retrocede cada intento
    const float EXTENSION_MAXIMA = 2f;         // cuánto puede estirarse para caer del otro lado de una mesa

    /// <summary>Por qué falló el último cálculo de trayectoria (para el mensaje en consola).</summary>
    public static string UltimoMotivoFallo { get; private set; }

    /// <summary>True mientras la pieza va por el aire (para que nadie la recoja a medio vuelo).</summary>
    public bool EnVuelo => !_aterrizada;

    // ═══════════════════════════════════════════════════════════════════════
    // CÁLCULO DE LA TRAYECTORIA
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calcula la ruta en planta desde <paramref name="origen"/> hacia <paramref name="direccion"/>,
    /// rebotando hasta <paramref name="rebotesMax"/> veces, y la recorta hasta el último punto donde
    /// la pieza puede caer en el suelo. Devuelve false si no hay ningún punto válido.
    /// </summary>
    public static bool CalcularTrayectoria(Vector3 origen, Vector3 direccion, float distancia,
                                           int rebotesMax, float sueloY, float alturaSondeo,
                                           out List<Vector3> ruta)
    {
        ruta = new List<Vector3>();
        direccion.y = 0f;
        if (direccion.sqrMagnitude < 0.0001f) return false;
        direccion.Normalize();

        Vector3 pos = new Vector3(origen.x, sueloY + alturaSondeo, origen.z);
        ruta.Add(pos);

        float restante = distancia;
        for (int rebote = 0; rebote <= rebotesMax && restante > 0.05f; rebote++)
        {
            if (LanzarRayoSolido(pos, direccion, restante, out RaycastHit hit))
            {
                float avance = Mathf.Max(0f, hit.distance - SEPARACION_PARED);
                pos += direccion * avance;
                ruta.Add(pos);
                restante -= avance;

                Vector3 normal = hit.normal;
                normal.y = 0f;
                if (normal.sqrMagnitude < 0.0001f || rebote == rebotesMax) break;

                direccion = Vector3.Reflect(direccion, normal.normalized);
                direccion.y = 0f;
                direccion.Normalize();
            }
            else
            {
                pos += direccion * restante;
                ruta.Add(pos);
                break;
            }
        }

        // Si cae sobre una mesa (la pasó por arriba pero le faltó poco), primero se intenta
        // estirar un poco hacia adelante para que caiga del otro lado…
        if (ruta.Count >= 2 && !PuntoDeAterrizajeValido(ruta[ruta.Count - 1], sueloY, out _))
        {
            Vector3 fin = ruta[ruta.Count - 1];
            Vector3 dirFinal = Plano(fin - ruta[ruta.Count - 2]);
            if (dirFinal.sqrMagnitude > 0.0001f)
            {
                dirFinal.Normalize();
                for (float extra = PASO_RETROCESO; extra <= EXTENSION_MAXIMA; extra += PASO_RETROCESO)
                {
                    if (LanzarRayoSolido(fin, dirFinal, extra, out _)) break; // hay pared: no estirar
                    Vector3 p = fin + dirFinal * extra;
                    if (PuntoDeAterrizajeValido(p, sueloY, out _))
                    {
                        ruta[ruta.Count - 1] = p;
                        return true;
                    }
                }
            }
        }

        // …y si no, se recorta desde el final hasta encontrar suelo libre
        float largo = LargoRuta(ruta);
        string motivoFinal = null;
        for (float s = largo; s >= 0f; s -= PASO_RETROCESO)
        {
            Vector3 p = PuntoEnRuta(ruta, s);
            if (PuntoDeAterrizajeValido(p, sueloY, out string motivo))
            {
                RecortarRuta(ruta, s);
                return true;
            }
            if (motivoFinal == null) motivoFinal = motivo;
        }
        UltimoMotivoFallo = motivoFinal;
        return false;
    }

    /// <summary>Altura del piso bajo una posición (ignora jugadores y objetos sueltos).</summary>
    public static bool BuscarSuelo(Vector3 posicion, out float sueloY)
    {
        sueloY = 0f;
        float masCercano = float.MaxValue;
        foreach (RaycastHit h in Physics.RaycastAll(posicion + Vector3.up * 1f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (EsIgnorable(h.collider)) continue;
            if (h.distance < masCercano) { masCercano = h.distance; sueloY = h.point.y; }
        }
        return masCercano < float.MaxValue;
    }

    /// <summary>
    /// Un punto sirve para aterrizar si debajo hay suelo a la altura del piso del jugador
    /// y no hay nada sólido encima (mesas, bote, banda, paredes).
    /// </summary>
    static bool PuntoDeAterrizajeValido(Vector3 punto, float sueloY, out string motivo)
    {
        motivo = null;

        // 1) Nada sólido ocupando ese lugar (cubre estar dentro de una mesa o pegado a ella)
        Vector3 centro = new Vector3(punto.x, sueloY + 0.45f, punto.z);
        foreach (Collider c in Physics.OverlapSphere(centro, 0.3f, ~0, QueryTriggerInteraction.Ignore))
            if (EsSolido(c)) { motivo = $"ocupado por '{c.name}'"; return false; }

        // 2) Debajo hay suelo a la misma altura (si no, es un hueco o fuera del mapa)
        Vector3 arriba = new Vector3(punto.x, sueloY + 2f, punto.z);
        RaycastHit[] hits = Physics.SphereCastAll(arriba, 0.15f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
        float masCercano = float.MaxValue;
        RaycastHit mejor = default;
        foreach (RaycastHit h in hits)
        {
            if (EsIgnorable(h.collider)) continue;
            if (h.distance < masCercano) { masCercano = h.distance; mejor = h; }
        }
        if (masCercano == float.MaxValue) { motivo = "no hay piso debajo (fuera del mapa)"; return false; }

        // Mesas, bote y banda nunca son suelo, aunque estén bajitas
        if (mejor.collider.GetComponentInParent<InteractableTable>() != null)
        { motivo = $"encima de '{mejor.collider.name}'"; return false; }

        // El piso tiene que estar a la misma altura que donde está parado el jugador
        if (Mathf.Abs(mejor.point.y - sueloY) >= 0.25f)
        { motivo = $"'{mejor.collider.name}' está a otra altura ({mejor.point.y:0.00} vs piso {sueloY:0.00})"; return false; }

        return true;
    }

    /// <summary>Rayo horizontal grueso que solo choca con paredes y muebles (no jugadores, objetos sueltos ni mesas).</summary>
    static bool LanzarRayoSolido(Vector3 desde, Vector3 dir, float distancia, out RaycastHit mejor)
    {
        mejor = default;
        float masCercano = float.MaxValue;
        foreach (RaycastHit h in Physics.SphereCastAll(desde, 0.15f, dir, distancia, ~0, QueryTriggerInteraction.Ignore))
        {
            if (EsIgnorable(h.collider)) continue;
            // Mesas, bote, cafetera, etc.: la pieza las pasa por arriba (sus colliders son más
            // altos que el modelo para que el radar del jugador las detecte). Solo rebota en paredes.
            if (h.collider.GetComponentInParent<InteractableTable>() != null) continue;
            if (h.distance < masCercano) { masCercano = h.distance; mejor = h; }
        }
        // Si ya empezaba pegado a algo (jugador contra la pared), Unity da distancia 0:
        // se trata como choque inmediato y la pieza rebota hacia atrás.
        if (masCercano < float.MaxValue && mejor.distance <= 0f) mejor.normal = -dir;
        return masCercano < float.MaxValue;
    }

    /// <summary>Jugadores y objetos sueltos (piezas, laptops, extintor) no detienen el vuelo.</summary>
    static bool EsIgnorable(Collider c)
    {
        if (c.GetComponentInParent<PlayerInteraction>() != null) return true;
        if (c.GetComponentInParent<PickableItem>() != null) return true;
        if (c.GetComponentInParent<LaptopController>() != null) return true;
        if (c.GetComponentInParent<Extinguisher>() != null) return true;
        return false;
    }

    /// <summary>
    /// Lo que NO es suelo: cualquier cosa sólida que no sea ignorable (mesas, bote, banda, paredes,
    /// muebles). El piso no entra aquí porque las revisiones se hacen por encima de él.
    /// </summary>
    static bool EsSolido(Collider c) => !EsIgnorable(c);

    // ── Utilidades de la ruta ─────────────────────────────────────────────────

    static float LargoRuta(List<Vector3> ruta)
    {
        float l = 0f;
        for (int i = 1; i < ruta.Count; i++) l += Plano(ruta[i] - ruta[i - 1]).magnitude;
        return l;
    }

    static Vector3 PuntoEnRuta(List<Vector3> ruta, float s)
    {
        for (int i = 1; i < ruta.Count; i++)
        {
            float tramo = Plano(ruta[i] - ruta[i - 1]).magnitude;
            if (s <= tramo || i == ruta.Count - 1)
                return tramo < 0.0001f ? ruta[i] : Vector3.Lerp(ruta[i - 1], ruta[i], Mathf.Clamp01(s / tramo));
            s -= tramo;
        }
        return ruta[ruta.Count - 1];
    }

    static void RecortarRuta(List<Vector3> ruta, float s)
    {
        Vector3 final = PuntoEnRuta(ruta, s);
        float acumulado = 0f;
        int i = 1;
        for (; i < ruta.Count; i++)
        {
            float tramo = Plano(ruta[i] - ruta[i - 1]).magnitude;
            if (acumulado + tramo >= s) break;
            acumulado += tramo;
        }
        ruta.RemoveRange(i, ruta.Count - i);
        ruta.Add(final);
    }

    static Vector3 Plano(Vector3 v) { v.y = 0f; return v; }

    // ═══════════════════════════════════════════════════════════════════════
    // VUELO
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Arranca el vuelo de la pieza por la ruta ya calculada.</summary>
    public void Iniciar(List<Vector3> ruta, float sueloY, float velocidad, float alturaArco,
                        PlayerInteraction lanzador, float radioAtrapar, float anguloAtrapar)
    {
        _ruta = ruta;
        _largo = LargoRuta(ruta);
        _inicio = transform.position;
        _rotacionInicial = transform.rotation;
        _sueloY = sueloY;
        _alturaFinal = sueloY + 0.2f;
        _alturaArco = alturaArco;
        _duracion = Mathf.Max(0.25f, _largo / Mathf.Max(0.1f, velocidad));
        _t = 0f;
        _aterrizada = false;

        _lanzador = lanzador;
        _jugadores = FindObjectsByType<PlayerInteraction>();
        _radioAtrapar = radioAtrapar;
        _anguloAtrapar = anguloAtrapar;
        _ejeGiro = Random.onUnitSphere;

        // En vuelo: sin física, sin collider y sin poder recogerse
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        PickableItem pi = GetComponent<PickableItem>();
        if (pi != null) pi.isInteractable = false;
    }

    void Update()
    {
        if (_aterrizada) { VigilarCaida(); return; }
        if (_ruta == null) return;

        _t += Time.deltaTime / _duracion;
        float t = Mathf.Clamp01(_t);

        Vector3 enPlanta = PuntoEnRuta(_ruta, t * _largo);
        float y = Mathf.Lerp(_inicio.y, _alturaFinal, t) + _alturaArco * 4f * t * (1f - t);
        // Al principio sale desde la mano; se va mezclando con la ruta para no dar un salto
        Vector3 objetivo = new Vector3(enPlanta.x, y, enPlanta.z);
        float mezcla = Mathf.Clamp01(t / 0.15f);
        transform.position = Vector3.Lerp(new Vector3(_inicio.x, y, _inicio.z), objetivo, mezcla);
        transform.Rotate(_ejeGiro, 540f * Time.deltaTime, Space.World);

        if (IntentarAtrapar()) return;

        if (t >= 1f) Aterrizar();
    }

    /// <summary>Un compañero con las manos libres, cerca y de frente a la pieza, la atrapa.</summary>
    bool IntentarAtrapar()
    {
        if (_jugadores == null) return false;

        foreach (PlayerInteraction p in _jugadores)
        {
            if (p == null || p == _lanzador || !p.PuedeAtrapar()) continue;

            Vector3 haciaPieza = transform.position - p.transform.position;
            float altura = transform.position.y - _sueloY;
            if (altura < 0f || altura > 2.2f) continue;

            haciaPieza.y = 0f;
            if (haciaPieza.magnitude > _radioAtrapar) continue;

            Vector3 frente = p.transform.forward; frente.y = 0f;
            if (haciaPieza.sqrMagnitude > 0.01f && Vector3.Angle(frente, haciaPieza) > _anguloAtrapar) continue;

            _aterrizada = true;
            Destroy(this);            // primero quitar el vuelo…
            p.AtraparPieza(gameObject); // …y luego queda en sus manos
            return true;
        }
        return false;
    }

    void Aterrizar()
    {
        _aterrizada = true;
        transform.position = new Vector3(transform.position.x, _alturaFinal, transform.position.z);
        // Cae con la misma orientación con la que salió de la mano (como al soltarla normal)
        transform.rotation = _rotacionInicial;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        PickableItem pi = GetComponent<PickableItem>();
        if (pi != null) pi.isInteractable = true;

        EfectosPolvo.Puff(new Vector3(transform.position.x, _sueloY + 0.05f, transform.position.z), 10, 0.3f);
    }

    /// <summary>Red de seguridad: si tras aterrizar se cae por debajo del piso, vuelve al punto de caída.</summary>
    void VigilarCaida()
    {
        _tiempoVigilancia += Time.deltaTime;

        if (transform.position.y < _sueloY - 2f && _ruta != null)
        {
            Vector3 final = _ruta[_ruta.Count - 1];
            transform.position = new Vector3(final.x, _alturaFinal + 0.3f, final.z);
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }

        if (_tiempoVigilancia >= VIGILAR_TRAS_ATERRIZAR) Destroy(this);
    }
}
