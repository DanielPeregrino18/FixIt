using UnityEngine;

public class ProceduralHead : MonoBehaviour
{
    [Header("⚙️ Referencias")]
    [Tooltip("Arrastra aquí el hueso de la cabeza o cuello.")]
    public Transform headBone;

    [Header("🎛️ Configuración de Bamboleo")]
    [Tooltip("Multiplicador principal. Súbelo para que la cabeza se incline mucho más.")]
    [Range(1f, 30f)] public float leanMultiplier = 12f;

    [Tooltip("El límite máximo de grados que la cabeza puede girar (Evita que se rompa el cuello).")]
    [Range(10f, 70f)] public float maxLeanAngle = 45f;

    [Tooltip("Qué tan rápido regresa la cabeza a su posición original.")]
    [Range(1f, 25f)] public float smoothSpeed = 12f;

    [Tooltip("Activa esto si el modelo fue rotado 180° en Y y el bamboleo va al revés.")]
    public bool invertirDireccion = false;

    // Variables internas
    private Quaternion _initialRotation;
    private Vector3 _lastPosition;
    private Vector3 _currentVelocity;

    void Start()
    {
        if (headBone != null)
        {
            _initialRotation = headBone.localRotation;
        }
        _lastPosition = transform.position;
    }

    void Update()
    {
        // FIX: si deltaTime es 0 (juego pausado o frame congelado), no calculamos velocidad
        // para evitar una división 0/0 que produce NaN.
        if (Time.deltaTime <= 0f)
        {
            _currentVelocity = Vector3.zero;
            _lastPosition = transform.position;
            return;
        }

        _currentVelocity = (transform.position - _lastPosition) / Time.deltaTime;
        _lastPosition = transform.position;
    }

    void LateUpdate()
    {
        if (headBone == null) return;

        Vector3 localVelocity = transform.InverseTransformDirection(_currentVelocity);

        float direccion = invertirDireccion ? -1f : 1f;
        float targetPitch = -localVelocity.z * leanMultiplier * direccion;
        float targetRoll  = -localVelocity.x * leanMultiplier * direccion;

        // FIX: si por algún motivo llegó a ser NaN o Infinity, lo reseteamos a 0
        // (Mathf.Clamp NO filtra NaN, por eso necesitamos esta verificación extra)
        if (float.IsNaN(targetPitch) || float.IsInfinity(targetPitch)) targetPitch = 0f;
        if (float.IsNaN(targetRoll)  || float.IsInfinity(targetRoll))  targetRoll  = 0f;

        targetPitch = Mathf.Clamp(targetPitch, -maxLeanAngle, maxLeanAngle);
        targetRoll  = Mathf.Clamp(targetRoll, -maxLeanAngle, maxLeanAngle);

        Quaternion tilt = Quaternion.Euler(targetPitch, 0, targetRoll);
        Quaternion targetRotation = _initialRotation * tilt;

        headBone.localRotation = Quaternion.Slerp(headBone.localRotation, targetRotation, Time.deltaTime * smoothSpeed);
    }
}