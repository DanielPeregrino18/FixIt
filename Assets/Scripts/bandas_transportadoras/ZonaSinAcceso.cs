using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zona invisible donde los jugadores no pueden entrar (por ejemplo, encima de las bandas
/// transportadoras). Hace dos cosas:
///  1. Al lanzar una pieza o el extintor, su punto de caída nunca puede quedar aquí
///     (PiezaLanzada la trata como lugar inválido y la pieza cae antes).
///  2. Red de seguridad: si aun así algo queda adentro, la pieza se destruye (cuenta como
///     desperdiciada) y el extintor regresa a su estación.
///
/// Las bandas (ConveyorBelt) crean la suya solas al iniciar. También se puede poner a mano:
/// un objeto con BoxCollider (se marca como trigger solo) y este script.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ZonaSinAcceso : MonoBehaviour
{
    static readonly List<ZonaSinAcceso> _todas = new List<ZonaSinAcceso>();

    [Tooltip("Si algo queda adentro: la pieza se pierde y el extintor vuelve a su estación. " +
             "Desactivado = solo funciona como pared para lo lanzado (útil si la zona toca piso donde caminan)")]
    public bool destruirLoQueCaiga = true;

    private BoxCollider _caja;

    void Awake()
    {
        _caja = GetComponent<BoxCollider>();
        _caja.isTrigger = true;
        gameObject.layer = 2; // Ignore Raycast: que no estorbe a los rayos del juego
    }

    void OnEnable() { if (!_todas.Contains(this)) _todas.Add(this); }
    void OnDisable() { _todas.Remove(this); }

    /// <summary>¿Ese punto cae dentro de alguna zona sin acceso (con un margen alrededor)?</summary>
    public static bool Contiene(Vector3 punto, float margen = 0f)
    {
        foreach (ZonaSinAcceso z in _todas)
        {
            if (z == null || z._caja == null) continue;
            Bounds b = z._caja.bounds;
            b.Expand(margen * 2f);
            if (b.Contains(punto)) return true;
        }
        return false;
    }

    /// <summary>Crea una zona que cubre el modelo del objeto y se extiende hacia arriba.</summary>
    public static ZonaSinAcceso CrearSobre(GameObject objeto, float alturaExtra, float margen)
    {
        Renderer[] renderers = objeto.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return null;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        GameObject go = new GameObject("ZonaSinAcceso_" + objeto.name);
        BoxCollider caja = go.AddComponent<BoxCollider>();
        caja.isTrigger = true;

        float alto = b.size.y + alturaExtra + 0.5f; // un poco por debajo del modelo y mucho por encima
        go.transform.position = new Vector3(b.center.x, b.min.y - 0.5f + alto * 0.5f, b.center.z);
        caja.size = new Vector3(b.size.x + margen * 2f, alto, b.size.z + margen * 2f);

        return go.AddComponent<ZonaSinAcceso>();
    }

    // ── Red de seguridad ─────────────────────────────────────────────────────

    void OnTriggerEnter(Collider other) => Revisar(other);
    void OnTriggerStay(Collider other) => Revisar(other);

    void Revisar(Collider other)
    {
        if (!destruirLoQueCaiga) return;
        HardwarePiece pieza = other.GetComponentInParent<HardwarePiece>();
        Extinguisher extintor = pieza == null ? other.GetComponentInParent<Extinguisher>() : null;
        if (pieza == null && extintor == null) return;

        Transform obj = pieza != null ? pieza.transform : extintor.transform;

        // No tocar lo que está en una mano, en una mesa/estación o todavía volando
        if (obj.GetComponentInParent<PlayerInteraction>() != null) return;
        if (obj.GetComponentInParent<InteractableTable>() != null) return;
        PiezaLanzada vuelo = obj.GetComponent<PiezaLanzada>();
        if (vuelo != null && vuelo.EnVuelo) return;

        if (pieza != null)
        {
            EfectosPolvo.Puff(obj.position, 10, 0.3f);
            if (GameManager.Instance != null) GameManager.Instance.RegistrarPiezaDesperdiciada();
            Debug.Log($"ZonaSinAcceso: {pieza.tipoPieza} cayó donde nadie la alcanza — se pierde (desperdiciada).");
            Destroy(pieza.gameObject);
        }
        else
        {
            foreach (ExtinguisherStation st in FindObjectsByType<ExtinguisherStation>())
            {
                if (st != null && st.EsSuExtintor(extintor.gameObject))
                {
                    st.ResetearExtintor();
                    Debug.Log("ZonaSinAcceso: el extintor cayó donde nadie lo alcanza — regresa a su estación.");
                    return;
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        BoxCollider c = _caja != null ? _caja : GetComponent<BoxCollider>();
        if (c == null) return;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.DrawCube(c.bounds.center, c.bounds.size);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}
