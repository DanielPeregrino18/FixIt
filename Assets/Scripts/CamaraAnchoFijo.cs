using UnityEngine;

/// <summary>
/// Mantiene SIEMPRE visible el mismo ancho de escenario, sin importar la proporción de la pantalla.
///
/// Una cámara normal tiene fijo el campo de visión VERTICAL: en una pantalla más cuadrada que
/// 16:9 (16:10, 4:3...) se pierden los costados. Este script hace lo contrario: cuando la
/// pantalla es menos ancha que la referencia, abre el campo vertical (se ve más arriba y más abajo)
/// para que el ancho visible no cambie. En pantallas iguales o más anchas que la referencia
/// no toca nada.
///
/// Ponlo en la cámara que dibuja el juego. Los valores de FOV / tamaño que ya tiene la cámara
/// en el Inspector se toman como la referencia para la proporción indicada.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CamaraAnchoFijo : MonoBehaviour
{
    [Tooltip("Proporción (ancho / alto) con la que se diseñó la cámara. 16:9 = 1.7778")]
    public float aspectoReferencia = 16f / 9f;

    private Camera _cam;
    private float  _fovReferencia;
    private float  _tamanoOrtoReferencia;
    private int    _anchoPrevio = -1;
    private int    _altoPrevio  = -1;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _fovReferencia        = _cam.fieldOfView;
        _tamanoOrtoReferencia = _cam.orthographicSize;
        Aplicar();
    }

    void LateUpdate()
    {
        // Solo recalcula si cambió el tamaño de la ventana / resolución
        if (Screen.width != _anchoPrevio || Screen.height != _altoPrevio)
            Aplicar();
    }

    void Aplicar()
    {
        _anchoPrevio = Screen.width;
        _altoPrevio  = Screen.height;
        if (_anchoPrevio <= 0 || _altoPrevio <= 0) return;

        float aspecto = (float)_anchoPrevio / _altoPrevio;

        if (aspecto >= aspectoReferencia)
        {
            // Igual o más ancha que la referencia: valores originales
            _cam.fieldOfView    = _fovReferencia;
            _cam.orthographicSize = _tamanoOrtoReferencia;
            return;
        }

        if (_cam.orthographic)
        {
            _cam.orthographicSize = _tamanoOrtoReferencia * aspectoReferencia / aspecto;
        }
        else
        {
            // FOV horizontal de la referencia → FOV vertical que lo mantiene en este aspecto
            float fovHorizontal = 2f * Mathf.Atan(Mathf.Tan(_fovReferencia * Mathf.Deg2Rad * 0.5f) * aspectoReferencia);
            float fovVertical   = 2f * Mathf.Atan(Mathf.Tan(fovHorizontal * 0.5f) / aspecto);
            _cam.fieldOfView    = fovVertical * Mathf.Rad2Deg;
        }
    }
}
