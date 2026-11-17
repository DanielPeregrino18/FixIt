using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marca una laptop como dorada (solo en Supervivencia). Al entregarla bien, DeliveryZone le pide
/// al GameManager un poder al azar. La agrega WaveManager al crearla: la pinta de dorado, le pone
/// una luz cálida y suelta destellos para que se vea desde lejos.
/// </summary>
public class LaptopDorada : MonoBehaviour
{
    /// <summary>Doradas que existen ahora (para no tener dos a la vez).</summary>
    public static readonly List<LaptopDorada> Activas = new List<LaptopDorada>();

    public Color colorDorado = new Color(1f, 0.78f, 0.15f, 1f);
    public float intervaloDestellos = 0.45f;

    private float _proximoDestello;
    private Light _luz;

    void OnEnable() { if (!Activas.Contains(this)) Activas.Add(this); }
    void OnDisable() { Activas.Remove(this); }

    /// <summary>Pinta la laptop de dorado y le agrega la luz.</summary>
    public void Configurar(Color color)
    {
        colorDorado = color;

        MeshRenderer r = GetComponentInChildren<MeshRenderer>();
        if (r != null)
        {
            Material m = new Material(r.sharedMaterial);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.75f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.8f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 0.35f);
            }
            r.material = m;
        }

        GameObject go = new GameObject("LuzDorada");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.6f;
        _luz = go.AddComponent<Light>();
        _luz.type = LightType.Point;
        _luz.color = color;
        _luz.range = 2.5f;
        _luz.intensity = 2f;
        _luz.shadows = LightShadows.None;
    }

    void Update()
    {
        // La luz late suavemente
        if (_luz != null) _luz.intensity = 1.6f + Mathf.Sin(Time.time * 4f) * 0.6f;

        // Destellos dorados de vez en cuando
        if (Time.time >= _proximoDestello)
        {
            _proximoDestello = Time.time + intervaloDestellos;
            EfectosPolvo.Puff(transform.position + Vector3.up * 0.35f, 2, 0.12f, colorDorado);
        }
    }
}
