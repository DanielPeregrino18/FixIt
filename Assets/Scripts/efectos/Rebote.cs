using UnityEngine;

/// <summary>
/// Squash & stretch: el objeto se aplasta (o estira) y rebota como gelatina hasta quedar normal.
/// Uso: <c>Rebote.Aplicar(objeto, fuerza)</c>. Fuerza positiva = primero se aplasta (al caer o
/// colocar algo); negativa = primero se estira (al recogerlo, al lanzar).
///
/// Muchos scripts leen o cambian la escala de los objetos (mesas, la mano, la banda). Para no
/// pelear con ellos, la deformación se QUITA al inicio de cada frame (este Update corre antes que
/// todos) y se vuelve a poner en LateUpdate: el resto del código siempre ve la escala real.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class Rebote : MonoBehaviour
{
    private Vector3 _aplicado = Vector3.one; // factor puesto en el último LateUpdate
    private float _fuerza;
    private float _duracion;
    private float _t;
    private bool _activo;
    private int _ejeVertical = 1; // eje local que apunta hacia arriba en el mundo (0=X, 1=Y, 2=Z)

    /// <summary>Hace rebotar al objeto. Si ya rebotaba más fuerte en este mismo instante, se respeta el más fuerte.</summary>
    public static void Aplicar(GameObject objeto, float fuerza, float duracion = 0.4f)
    {
        if (objeto == null || Mathf.Approximately(fuerza, 0f)) return;
        Rebote r = objeto.GetComponent<Rebote>();
        if (r == null) r = objeto.AddComponent<Rebote>();
        r.Iniciar(fuerza, duracion);
    }

    void Iniciar(float fuerza, float duracion)
    {
        // Dos rebotes en el mismo frame (p. ej. entregar + soltar): gana el más fuerte
        if (_activo && _t < 0.05f && Mathf.Abs(fuerza) < Mathf.Abs(_fuerza)) return;

        _fuerza = Mathf.Clamp(fuerza, -0.6f, 0.6f);
        _duracion = Mathf.Max(0.1f, duracion);
        _t = 0f;
        _activo = true;
        _ejeVertical = EjeHaciaArriba();
    }

    /// <summary>Qué eje local del objeto apunta más hacia arriba (los modelos importados suelen venir girados).</summary>
    int EjeHaciaArriba()
    {
        float x = Mathf.Abs(Vector3.Dot(transform.right, Vector3.up));
        float y = Mathf.Abs(Vector3.Dot(transform.up, Vector3.up));
        float z = Mathf.Abs(Vector3.Dot(transform.forward, Vector3.up));
        if (x >= y && x >= z) return 0;
        return y >= z ? 1 : 2;
    }

    void Update()
    {
        Deshacer();
        if (!_activo) return;

        _t += Time.deltaTime;
        if (_t >= _duracion) _activo = false;
    }

    void LateUpdate()
    {
        if (!_activo) return;

        // Resorte amortiguado: arranca en (1 - fuerza), oscila y se calma en 1
        float k = Mathf.Clamp01(_t / _duracion);
        float alto = 1f - _fuerza * Mathf.Exp(-4f * k) * Mathf.Cos(k * Mathf.PI * 3f) * (1f - k);
        alto = Mathf.Max(0.2f, alto);
        float ancho = 1f / Mathf.Sqrt(alto); // conserva el volumen: si baja, se ensancha

        _aplicado = _ejeVertical switch
        {
            0 => new Vector3(alto, ancho, ancho),
            2 => new Vector3(ancho, ancho, alto),
            _ => new Vector3(ancho, alto, ancho),
        };
        transform.localScale = Vector3.Scale(transform.localScale, _aplicado);
    }

    void Deshacer()
    {
        if (_aplicado == Vector3.one) return;
        Vector3 s = transform.localScale;
        transform.localScale = new Vector3(s.x / _aplicado.x, s.y / _aplicado.y, s.z / _aplicado.z);
        _aplicado = Vector3.one;
    }

    void OnDisable()
    {
        // Si se apaga a medio rebote, que no se quede deformado
        Deshacer();
        _activo = false;
    }
}
