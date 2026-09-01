using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Servidor de la oficina. En niveles avanzados el JSON del nivel programa
/// su incendio (id + segundo). Si no se apaga a tiempo con el extintor,
/// explota y termina el nivel (Game Over). Evento único por nivel.
/// </summary>
public class Server : MonoBehaviour, IExtinguishable
{
    public enum EstadoServidor { Normal, Advertencia, EnLlamas, Explotado }

    [Header("Identidad")]
    [Tooltip("ID único de este servidor en la escena (1, 2, 3). El JSON lo usa para programarlo.")]
    public int servidorId = 1;

    [Header("Activación (normalmente la programa el JSON del nivel)")]
    [Tooltip("Si está activo, el incendio comenzará cuando el timer llegue a tiempoActivacion.")]
    public bool activarIncendio = false;

    [Tooltip("Segundos desde que se programó para que comience el incendio.")]
    public float tiempoActivacion = 90f;

    [Header("Fases")]
    [Tooltip("Segundos de humo (advertencia) antes de pasar a llamas si no se apaga.")]
    public float tiempoAdvertencia = 15f;

    [Tooltip("Segundos en llamas antes de explotar si no se apaga.")]
    public float tiempoEnLlamasAntesDeExplotar = 20f;

    [Tooltip("Segundos SOSTENIDOS de extintor necesarios para apagarlo.")]
    public float tiempoNecesarioParaApagar = 4f;

    [Header("Efectos Visuales")]
    public ParticleSystem humoParticles;
    public ParticleSystem fuegoParticles;
    public ParticleSystem explosionParticles; // Hijo del servidor, posicionado a gusto

    [Header("Barra de Apagado Flotante")]
    public GameObject barraUI;
    public Image barraRelleno;

    // ── Estado interno ────────────────────────────────────────────────────────
    private EstadoServidor _estado          = EstadoServidor.Normal;
    private float          _timerActivacion = 0f; // cuenta desde que se programó
    private float          _timerFase       = 0f;
    private float          _timerApagando   = 0f;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (barraUI != null)            barraUI.SetActive(false);
        if (humoParticles != null)      humoParticles.Stop();
        if (fuegoParticles != null)     fuegoParticles.Stop();
        if (explosionParticles != null) explosionParticles.gameObject.SetActive(false);
    }

    void Update()
    {
        if (_estado == EstadoServidor.Explotado) return;
        if (!activarIncendio) return;

        if (_estado == EstadoServidor.Normal)
        {
            _timerActivacion += Time.deltaTime;
            if (_timerActivacion >= tiempoActivacion)
                EntrarEnAdvertencia();
            return;
        }

        if (_estado == EstadoServidor.Advertencia)
        {
            _timerFase += Time.deltaTime;
            if (_timerFase >= tiempoAdvertencia)
                EntrarEnLlamas();
        }
        else if (_estado == EstadoServidor.EnLlamas)
        {
            _timerFase += Time.deltaTime;
            if (_timerFase >= tiempoEnLlamasAntesDeExplotar)
                Explotar();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // API PÚBLICA — programación desde el JSON del nivel
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Programa el incendio para dentro de X segundos (llamado por WaveManager).</summary>
    public void ProgramarIncendio(float segundos)
    {
        activarIncendio  = true;
        tiempoActivacion = segundos;
        _timerActivacion = 0f;
        _estado          = EstadoServidor.Normal;
        Debug.Log($"Server {servidorId}: incendio programado en {segundos}s.");
    }

    /// <summary>
    /// Desactiva cualquier incendio pendiente y resetea el servidor a su estado
    /// normal por completo (visuales incluidos). Usado al iniciar/reiniciar un nivel.
    /// </summary>
    public void Desactivar()
    {
        StopAllCoroutines(); // cancela un GameOverConDelay pendiente si explotó justo antes

        activarIncendio  = false;
        _timerActivacion = 0f;
        _timerFase       = 0f;
        _timerApagando   = 0f;
        _estado          = EstadoServidor.Normal;

        if (humoParticles != null)      humoParticles.Stop();
        if (fuegoParticles != null)     fuegoParticles.Stop();
        if (barraUI != null)            barraUI.SetActive(false);
        if (explosionParticles != null) explosionParticles.gameObject.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // IExtinguishable
    // ═══════════════════════════════════════════════════════════════════════

    public bool EstaEnPeligro() =>
        _estado == EstadoServidor.Advertencia || _estado == EstadoServidor.EnLlamas;

    public void RecibirChorroExtintor(float deltaTime)
    {
        if (!EstaEnPeligro()) return;

        if (barraUI != null) barraUI.SetActive(true);

        _timerApagando += deltaTime;
        ActualizarBarra();

        if (_timerApagando >= tiempoNecesarioParaApagar)
            Apagar();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FASES
    // ═══════════════════════════════════════════════════════════════════════

    void EntrarEnAdvertencia()
    {
        _estado        = EstadoServidor.Advertencia;
        _timerFase     = 0f;
        _timerApagando = 0f;

        if (humoParticles != null) humoParticles.Play();
        Debug.Log($"Server {servidorId}: ⚠️ humo detectado.");
    }

    void EntrarEnLlamas()
    {
        _estado    = EstadoServidor.EnLlamas;
        _timerFase = 0f;

        if (humoParticles != null)  humoParticles.Stop();
        if (fuegoParticles != null) fuegoParticles.Play();
        Debug.Log($"Server {servidorId}: 🔥 ¡en llamas!");
    }

    void Apagar()
    {
        _estado         = EstadoServidor.Normal;
        _timerFase      = 0f;
        _timerApagando  = 0f;
        activarIncendio = false; // evento único: ya se resolvió

        if (humoParticles != null)  humoParticles.Stop();
        if (fuegoParticles != null) fuegoParticles.Stop();
        if (barraUI != null)        barraUI.SetActive(false);

        Debug.Log($"Server {servidorId}: ✅ incendio apagado.");
    }

    void Explotar()
    {
        _estado = EstadoServidor.Explotado;

        // El fuego sigue — el servidor queda ardiendo tras la explosión
        if (barraUI != null) barraUI.SetActive(false);

        if (explosionParticles != null)
        {
            explosionParticles.gameObject.SetActive(true);
            foreach (ParticleSystem ps in explosionParticles.GetComponentsInChildren<ParticleSystem>(true))
                ps.Play();
        }

        Debug.Log($"Server {servidorId}: 💥 ¡EXPLOTÓ! Game Over en breve...");
        StartCoroutine(GameOverConDelay(2f));
    }

    System.Collections.IEnumerator GameOverConDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (GameManager.Instance != null)
            GameManager.Instance.ServidorExplotado();
    }

    void ActualizarBarra()
    {
        if (barraRelleno != null)
            barraRelleno.fillAmount = Mathf.Clamp01(_timerApagando / tiempoNecesarioParaApagar);
    }
}