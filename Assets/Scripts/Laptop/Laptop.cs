using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Laptop : MonoBehaviour, IExtinguishable
{
    // ── Tipos de problema ─────────────────────────────────────────────────────
    public enum Problema
    {
        Limpieza,
        Reparacion,
        Actualizacion,
        NecesitaRAM,
        NecesitaHDD,
        NecesitaProcesador
    }

    // ── Problemas activos ─────────────────────────────────────────────────────
    [Header("Estado de la Laptop")]
    public List<Problema> problemasActivos = new List<Problema>();

    // ── Referencias del Canvas flotante ───────────────────────────────────────
    [Header("Canvas del Ticket")]
    public Transform canvasTicket;
    public Transform iconosContainer;

    // ── Sprites de los iconos ─────────────────────────────────────────────────
    [Header("Iconos (arrastra desde el Project)")]
    public Sprite icono_limpieza;
    public Sprite icono_reparacion;
    public Sprite icono_actualizacion;
    public Sprite icono_ram;
    public Sprite icono_hdd;
    public Sprite icono_procesador;

    [Header("Tamaño de cada ícono en el canvas")]
    public float iconSize = 40f;

    // ═══════════════════════════════════════════════════════════════════════
    // SISTEMA DE INCENDIOS
    // ═══════════════════════════════════════════════════════════════════════

    public enum EstadoFuego { Normal, Advertencia, EnLlamas, Cenizas }

    [Header("Incendios — Tiempos por tipo de problema (segundos)")]
    [Tooltip("Limpieza y Procesador — riesgo alto")]
    public float tiempoLimpiezaProcesador = 45f;
    [Tooltip("RAM y HDD — riesgo medio")]
    public float tiempoRamHdd = 75f;
    [Tooltip("Actualización — riesgo bajo")]
    public float tiempoActualizacion = 120f;

    [Header("Incendios — Configuración de fases")]
    [Range(0f, 1f)]
    [Tooltip("Porcentaje del tiempo límite donde empieza la fase de advertencia (humo)")]
    public float porcentajeAdvertencia = 0.7f;

    [Tooltip("Segundos en llamas antes de calcinarse permanentemente")]
    public float tiempoEnLlamasAntesDeCalcinar = 20f;
    [Tooltip("Color al que se tiñe la laptop al calcinarse (el rato que queda en pantalla antes de desaparecer)")]
    public Color colorCalcinada = new Color(0.06f, 0.06f, 0.06f);

    [Tooltip("Segundos que el jugador queda bloqueado al recoger en fase de advertencia")]
    public float penalizacionAlRecoger = 2f;

    [Tooltip("Segundos que le quedarán antes de incendiarse tras la penalización")]
    public float tiempoRestanteTrasPenalizacion = 30f;

    // Funciona como los servidores: el rocío se ACUMULA aunque se suelte y se vuelva a rociar.
    // (Los nombres "segundos…" son nuevos a propósito: Unity conserva en memoria el valor viejo de
    // un campo ya existente aunque cambie el valor por defecto en el código.)
    [Header("Incendios — Extintor")]
    [Tooltip("Segundos de rocío (acumulados) para quitar el HUMO. Soplarle tarda Penalizacion Al Recoger")]
    public float segundosExtintorHumo = 0.5f;
    [Tooltip("Segundos de rocío (acumulados) para apagar el FUEGO")]
    public float segundosExtintorFuego = 1f;

    [Header("Incendios — Efectos (se conectan después)")]
    public ParticleSystem humoParticles;
    public ParticleSystem fuegoParticles;

    // ── Estado interno del fuego ─────────────────────────────────────────────
    private EstadoFuego _estadoFuego = EstadoFuego.Normal;
    private float _timerActual = 0f;
    private float _timerLimite = 999f;
    private float _timerEnLlamas = 0f;
    private bool _calentando = false; // true = está en mesa/suelo, sumando tiempo
    private bool _bloqueadaTemp = false; // bloqueada por la animación de penalización

    // ── Apagado con extintor (se acumula mientras se rocía) ──────────────────
    private float _progresoExtintor = 0f;      // segundos de rocío acumulados en la fase actual
    private int _frameUltimoRocio = -1;        // para contar una sola vez por frame aunque tenga varios colliders
    private float _emisionHumoBase = -1f, _emisionFuegoBase = -1f;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        RefrescarIconos();
        RecalcularLimiteFuego();
    }

    void LateUpdate()
    {
        // Billboard: el canvas siempre mira hacia la cámara
        if (canvasTicket != null && Camera.main != null)
        {
            canvasTicket.rotation = Camera.main.transform.rotation;
        }
    }

    void Update()
    {
        ActualizarFuego();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // API PÚBLICA — PROBLEMAS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Resuelve un problema, refresca iconos y resetea el timer de fuego.</summary>
    public void ResolverProblema(Problema problema)
    {
        if (problemasActivos.Contains(problema))
        {
            problemasActivos.Remove(problema);
            RefrescarIconos();

            // Cada reparación resetea el riesgo de incendio
            RecalcularLimiteFuego();
            _timerActual = 0f;
            if (_estadoFuego == EstadoFuego.Advertencia)
                VolverANormal();

            Debug.Log($"Laptop: problema '{problema}' resuelto. Quedan {problemasActivos.Count}.");
        }
    }

    public bool EstaLista() => problemasActivos.Count == 0;

    /// <summary>true cuando ya se registró su entrega (sigue en la banda de salida hasta destruirse).</summary>
    [System.NonSerialized] public bool yaEntregada = false;

    // ═══════════════════════════════════════════════════════════════════════
    // API PÚBLICA — INCENDIOS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Llamar cuando la laptop queda en una mesa de trabajo o en el suelo.</summary>
    public void OnDejada()
    {
        _calentando = true;
    }

    /// <summary>
    /// Llamar SIEMPRE antes de mover la laptop a la mano del jugador.
    /// Devuelve false si está en llamas y NO se debe permitir recogerla.
    /// </summary>
    public bool PuedeRecogerse()
    {
        return _estadoFuego != EstadoFuego.EnLlamas;
    }

    /// <summary>Llamar justo después de mover la laptop a la mano del jugador.</summary>
    public void OnRecogida(PlayerInteraction player)
    {
        _calentando = false; // se pausa el calentamiento mientras se carga

        if (_estadoFuego == EstadoFuego.Advertencia)
            StartCoroutine(PenalizarRecogidaEnAdvertencia(player));
    }

    /// <summary>
    /// Pone la laptop en llamas desde ya (laptop de práctica del extintor al iniciar un nivel).
    /// segundosAntesDeCalcinar reemplaza el tiempo normal que aguanta en llamas.
    /// </summary>
    public void EmpezarEnLlamas(float segundosAntesDeCalcinar)
    {
        if (segundosAntesDeCalcinar > 0f) tiempoEnLlamasAntesDeCalcinar = segundosAntesDeCalcinar;
        _calentando = true;
        EntrarEnLlamas();
    }

    /// <summary>
    /// Apaga de inmediato el fuego o el humo (se llama al completar el rocío con el extintor).
    /// (El humo también se puede quitar soplándole, al recogerla con la mano.)
    /// </summary>
    public void ApagarConExtintor()
    {
        if (_estadoFuego != EstadoFuego.EnLlamas && _estadoFuego != EstadoFuego.Advertencia) return;

        bool eraFuego = _estadoFuego == EstadoFuego.EnLlamas;
        VolverANormal();
        _timerActual = 0f;
        _timerEnLlamas = 0f;
        Debug.Log(eraFuego ? "Laptop: incendio apagado con el extintor." : "Laptop: humo apagado con el extintor.");
    }

    public bool EstaEnLlamas() => _estadoFuego == EstadoFuego.EnLlamas;
    public bool EstaEnAdvertencia() => _estadoFuego == EstadoFuego.Advertencia;

    // ── Implementación de IExtinguishable ───────────────────────────────────
    // Igual que los servidores: los segundos de rocío se acumulan (aunque se suelte y se vuelva
    // a rociar) hasta llegar al tiempo necesario. Al cambiar de fase, el avance vuelve a cero.
    public bool EstaEnPeligro() => EstaEnLlamas() || EstaEnAdvertencia();

    /// <summary>True mientras el jugador le sopla (no puede soltarla ni moverse).</summary>
    public bool EstaSoplandose() => _bloqueadaTemp;

    public void RecibirChorroExtintor(float deltaTime)
    {
        if (!EstaEnPeligro()) return;

        // Si la laptop tiene varios colliders, el extintor la "toca" varias veces por frame
        if (_frameUltimoRocio == Time.frameCount) return;
        _frameUltimoRocio = Time.frameCount;

        _progresoExtintor += deltaTime;

        float necesario = EstaEnLlamas() ? segundosExtintorFuego : segundosExtintorHumo;
        if (_progresoExtintor >= necesario)
            ApagarConExtintor();
        else
            MostrarAvanceExtintor(_progresoExtintor / Mathf.Max(0.01f, necesario));
    }

    /// <summary>El humo o el fuego se van achicando mientras se rocía (0 = completo … 1 = casi apagado).</summary>
    void MostrarAvanceExtintor(float avance)
    {
        float factor = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(avance));
        AjustarEmision(humoParticles, ref _emisionHumoBase, factor);
        AjustarEmision(fuegoParticles, ref _emisionFuegoBase, factor);
    }

    static void AjustarEmision(ParticleSystem ps, ref float baseEmision, float factor)
    {
        if (ps == null) return;
        ParticleSystem.EmissionModule em = ps.emission;
        if (baseEmision < 0f) baseEmision = em.rateOverTimeMultiplier; // se guarda la original la primera vez
        em.rateOverTimeMultiplier = baseEmision * factor;
    }

    /// <summary>Al cambiar de fase (humo → fuego, o apagada), el avance del extintor empieza de cero.</summary>
    void ReiniciarAvanceExtintor()
    {
        _progresoExtintor = 0f;
        MostrarAvanceExtintor(0f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LÓGICA INTERNA — FUEGO
    // ═══════════════════════════════════════════════════════════════════════

    void ActualizarFuego()
    {
        if (GameManager.Instance != null && !GameManager.Instance.incendiosActivos) return;
        if (PoderesSupervivencia.Activo(Poder.FuegoCongelado)) return; // poder: nada se calienta ni avanza
        if (_estadoFuego == EstadoFuego.Cenizas) return;
        if (_bloqueadaTemp) return;

        // Fase: En llamas — cuenta hacia la calcinación
        if (_estadoFuego == EstadoFuego.EnLlamas)
        {
            _timerEnLlamas += Time.deltaTime;
            if (_timerEnLlamas >= tiempoEnLlamasAntesDeCalcinar)
                Calcinar();
            return;
        }

        // Solo sube temperatura si está "calentando" (mesa o suelo)
        if (!_calentando) return;

        _timerActual += Time.deltaTime;
        float progreso = _timerActual / _timerLimite;

        if (_estadoFuego == EstadoFuego.Normal && progreso >= porcentajeAdvertencia)
        {
            EntrarEnAdvertencia();
        }

        if (_estadoFuego == EstadoFuego.Advertencia && _timerActual >= _timerLimite)
        {
            EntrarEnLlamas();
        }
    }

    void RecalcularLimiteFuego()
    {
        if (problemasActivos.Count == 0)
        {
            _timerLimite = 999f; // sin problemas, no hay riesgo
            return;
        }

        float menor = float.MaxValue;
        foreach (Problema p in problemasActivos)
        {
            float t = TiempoParaProblema(p);
            if (t < menor) menor = t;
        }
        _timerLimite = menor;
    }

    float TiempoParaProblema(Problema p)
    {
        switch (p)
        {
            case Problema.Limpieza:
            case Problema.NecesitaProcesador:
                return tiempoLimpiezaProcesador;
            case Problema.NecesitaRAM:
            case Problema.NecesitaHDD:
                return tiempoRamHdd;
            case Problema.Actualizacion:
            default:
                return tiempoActualizacion;
        }
    }

    void EntrarEnAdvertencia()
    {
        _estadoFuego = EstadoFuego.Advertencia;
        ReiniciarAvanceExtintor();
        if (humoParticles != null) humoParticles.Play();
        Debug.Log("Laptop: ⚠️ Advertencia — humo detectado.");
    }

    void EntrarEnLlamas()
    {
        _estadoFuego = EstadoFuego.EnLlamas;
        _timerEnLlamas = 0f;
        ReiniciarAvanceExtintor(); // lo rociado en humo no cuenta para el fuego
        if (humoParticles != null) humoParticles.Stop();
        if (fuegoParticles != null) fuegoParticles.Play();
        // GestureController.MostrarGestoATodos(TipoGesto.Panico); // pendiente: integrar sistema de gestos
        Debug.Log("Laptop: 🔥 ¡En llamas!");
    }

    void VolverANormal()
    {
        _estadoFuego = EstadoFuego.Normal;
        ReiniciarAvanceExtintor();
        if (humoParticles != null) humoParticles.Stop();
        if (fuegoParticles != null) fuegoParticles.Stop();
    }

    void Calcinar()
    {
        _estadoFuego = EstadoFuego.Cenizas;
        if (fuegoParticles != null) fuegoParticles.Stop();

        Debug.Log("Laptop: 💀 Calcinada, se pierde.");
        PintarCalcinada();

        AudioManager.ExplosionLaptop();
        ProceduralHead.Susto(transform.position); // los jugadores cercanos se asustan

        if (GameManager.Instance != null)
            GameManager.Instance.RegistrarLaptopPerdida();

        Destroy(gameObject, 1f);
    }

    /// <summary>
    /// Tiñe la laptop (y lo que tenga encima) del color de calcinada. Solo modelos 3D:
    /// partículas, sprites y UI se quedan como están.
    /// </summary>
    void PintarCalcinada()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            foreach (Material m in r.materials) // copias propias de esta laptop
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colorCalcinada); // URP
                if (m.HasProperty("_Color")) m.SetColor("_Color", colorCalcinada);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    IEnumerator PenalizarRecogidaEnAdvertencia(PlayerInteraction player)
    {
        _bloqueadaTemp = true;

        PlayerMovement pm = player != null ? player.GetComponent<PlayerMovement>() : null;
        if (pm != null) pm.movimientoBloqueado = true;

        AudioManager.IniciarSoplido();

        Debug.Log("Laptop: 💨 El jugador le sopla para enfriarla...");
        yield return new WaitForSeconds(penalizacionAlRecoger);

        AudioManager.DetenerSoplido(); // el clip dura más que el soplido
        if (pm != null) pm.movimientoBloqueado = false;

        VolverANormal();
        _timerActual = Mathf.Max(0f, _timerLimite - tiempoRestanteTrasPenalizacion);

        _bloqueadaTemp = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ÍCONOS DEL TICKET
    // ═══════════════════════════════════════════════════════════════════════

    void RefrescarIconos()
    {
        if (iconosContainer == null) return;

        foreach (Transform hijo in iconosContainer)
            Destroy(hijo.gameObject);

        if (canvasTicket != null)
            canvasTicket.gameObject.SetActive(problemasActivos.Count > 0);

        foreach (Problema p in problemasActivos)
        {
            Sprite sprite = ObtenerSprite(p);
            if (sprite == null) continue;

            GameObject iconObj = new GameObject(p.ToString(), typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(iconosContainer, false);

            RectTransform rt = iconObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(iconSize, iconSize);

            Image img = iconObj.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
        }
    }

    Sprite ObtenerSprite(Problema problema)
    {
        switch (problema)
        {
            case Problema.Limpieza: return icono_limpieza;
            case Problema.Reparacion: return icono_reparacion;
            case Problema.Actualizacion: return icono_actualizacion;
            case Problema.NecesitaRAM: return icono_ram;
            case Problema.NecesitaHDD: return icono_hdd;
            case Problema.NecesitaProcesador: return icono_procesador;
            default: return null;
        }
    }
}