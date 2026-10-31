using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Maneja la música del juego. Por ahora: la canción del nivel.
///
/// - IniciarMusicaNivel(): arranca la canción desde el principio (inicio, reintento,
///   reinicio desde la pausa y siguiente nivel).
/// - PausarMusica() / ReanudarMusica(): para la pausa del juego.
/// - DetenerMusicaConFundido(): baja el volumen poco a poco y se detiene (fin del nivel).
/// - DetenerMusicaInmediato(): corta al instante (salir al menú).
///
/// Todo el tiempo del fundido es "real" (unscaled), porque el juego puede estar en
/// timeScale 0 al terminar el nivel.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Música del nivel")]
    [Tooltip("Canción que suena mientras dura el nivel")]
    public AudioClip musicaNivel;
    [Range(0f, 1f)] public float volumenMusica = 0.7f;
    [Tooltip("Segundos que tarda en bajar el volumen al terminar el nivel")]
    public float duracionFundido = 1f;
    [Tooltip("Si la canción termina antes que el nivel, ¿se repite?")]
    public bool repetir = false;

    [Header("Sincronía con el final del nivel")]
    [Tooltip("Si la canción dura MÁS que el nivel, se retrasa su inicio para que termine justo cuando acaba el nivel")]
    public bool sincronizarFinal = true;
    [Tooltip("Ajuste fino en segundos: positivo = la canción termina un poco DESPUÉS del nivel, negativo = un poco antes")]
    public float ajusteFinal = 0f;

    [Header("Música de menús (principal, selección de personaje y de nivel)")]
    [Tooltip("Una sola canción que suena en loop en todos los menús")]
    public AudioClip musicaMenu;
    [Range(0f, 1f)] public float volumenMenu = 0.7f;
    [Tooltip("Segundos de mezcla entre el final de la canción y su inicio al repetirse. 0 = loop normal")]
    public float duracionCruceLoop = 3f;
    [Tooltip("Segundos que tarda en bajar la música de menú al entrar a un nivel")]
    public float duracionFundidoMenu = 0.8f;

    [Header("Sonidos de la pantalla de resultados")]
    public AudioClip sonidoVictoria;
    public AudioClip sonidoDerrota;
    [Range(0f, 1f)] public float volumenSonidoResultado = 1f;
    [Tooltip("Espera antes del sonido, para que no se empalme con el fundido de la música del nivel")]
    public float retrasoSonidoResultado = 0.4f;

    [Header("Sonidos de interfaz (menús)")]
    [Tooltip("Al cambiar de botón o de opción")]
    public AudioClip sonidoNavegar;
    [Tooltip("Al confirmar: elegir personaje, nivel u opción de un menú o de los resultados")]
    public AudioClip sonidoConfirmar;
    [Tooltip("Al pasar de un personaje a otro en la selección de personaje (izquierda / derecha)")]
    public AudioClip sonidoCambioPersonaje;
    [Range(0f, 1f)] public float volumenInterfaz = 1f;

    [Header("Efectos del juego")]
    [Tooltip("Golpe del martillo en la mesa de reparación (uno por pulsación)")]
    public AudioClip sonidoGolpe;
    [Header("Aire comprimido (mesa de limpieza)")]
    [Tooltip("Suena una vez al empezar a limpiar; justo al terminar entra el loop")]
    public AudioClip sonidoAireInicio;
    [Tooltip("Se repite mientras dura la limpieza")]
    [FormerlySerializedAs("sonidoAireComprimido")]
    public AudioClip sonidoAireLoop;
    [Tooltip("Suena una vez al terminar o cancelar la limpieza")]
    public AudioClip sonidoAireFinal;

    [Header("Volumen de efectos")]
    [Range(0f, 1f)] public float volumenEfectos = 1f;

    private AudioSource _fuenteAire;
    private AudioSource _fuenteAireExtremos; // inicio y final del aire
    private int _limpiezasActivas = 0; // cuántas mesas de limpieza están en uso a la vez

    private AudioSource _fuenteSfx;
    private AudioSource _fuenteUI;
    private AudioSource _fuenteEfectos;
    private Coroutine _sonidoResultadoPendiente;

    private AudioSource _fuenteMusica;
    private Coroutine _fundido;
    private Coroutine _inicioPendiente;
    private bool _pausada = false;

    // Música de menú: dos fuentes que se alternan para mezclar el final con el inicio
    private AudioSource _menuA;
    private AudioSource _menuB;
    private Coroutine _bucleMenu;
    private Coroutine _fundidoMenu;
    private bool _menuActivo = false;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _fuenteMusica = GetComponent<AudioSource>();
        if (_fuenteMusica == null) _fuenteMusica = gameObject.AddComponent<AudioSource>();

        _fuenteMusica.playOnAwake = false;
        _fuenteMusica.spatialBlend = 0f; // 2D: suena igual sin importar dónde esté la cámara

        _menuA = CrearFuenteMenu();
        _menuB = CrearFuenteMenu();
        _fuenteSfx = CrearFuenteMenu(); // fuente aparte para efectos: no la cortan los fundidos de la música
        _fuenteUI = CrearFuenteMenu(); // y otra para los sonidos de menús, que nunca se cortan entre sí con otros
        _fuenteEfectos = CrearFuenteMenu(); // efectos del juego (golpes, etc.)

        _fuenteAire = CrearFuenteMenu();
        _fuenteAire.loop = true;
        _fuenteAireExtremos = CrearFuenteMenu();
    }

    // ── Aire comprimido (loop mientras alguien limpia) ────────────────────────
    // Si dos jugadores limpian a la vez, el sonido sigue hasta que termine el último.

    public static void IniciarAireComprimido() { if (Instance != null) Instance.AireMasUno(); }
    public static void DetenerAireComprimido() { if (Instance != null) Instance.AireMenosUno(); }

    void AireMasUno()
    {
        _limpiezasActivas++;
        if (_limpiezasActivas != 1 || _fuenteAire == null) return;

        // 1. Sonido de inicio
        if (sonidoAireInicio != null)
        {
            _fuenteAireExtremos.Stop();
            _fuenteAireExtremos.clip = sonidoAireInicio;
            _fuenteAireExtremos.volume = volumenEfectos;
            _fuenteAireExtremos.Play();
        }

        // 2. Loop: entra justo cuando termina el inicio (programado, sin hueco)
        if (sonidoAireLoop != null)
        {
            _fuenteAire.Stop();
            _fuenteAire.clip = sonidoAireLoop;
            _fuenteAire.volume = volumenEfectos;
            if (sonidoAireInicio != null)
                _fuenteAire.PlayScheduled(AudioSettings.dspTime + sonidoAireInicio.length);
            else
                _fuenteAire.Play();
        }
    }

    void AireMenosUno()
    {
        if (_limpiezasActivas <= 0) return;
        _limpiezasActivas--;
        if (_limpiezasActivas > 0 || _fuenteAire == null) return;

        // Se corta el loop (o su arranque programado) y suena el final
        _fuenteAire.Stop();
        if (sonidoAireFinal != null)
        {
            _fuenteAireExtremos.Stop(); // por si el inicio aún no había terminado
            _fuenteAireExtremos.clip = sonidoAireFinal;
            _fuenteAireExtremos.volume = volumenEfectos;
            _fuenteAireExtremos.Play();
        }
    }

    /// <summary>Apaga todo el aire en seco, sin sonido final (fin de nivel, reinicio o salir al menú).</summary>
    void ReiniciarAire()
    {
        _limpiezasActivas = 0;
        if (_fuenteAire != null) _fuenteAire.Stop();
        if (_fuenteAireExtremos != null) _fuenteAireExtremos.Stop();
    }

    // ── Efectos del juego ─────────────────────────────────────────────────────

    public static void Golpe() { if (Instance != null) Instance.ReproducirEfecto(Instance.sonidoGolpe); }

    void ReproducirEfecto(AudioClip clip)
    {
        if (clip == null || _fuenteEfectos == null) return;
        _fuenteEfectos.PlayOneShot(clip, volumenEfectos); // PlayOneShot permite que varios golpes seguidos se solapen
    }

    // ── Sonidos de interfaz ───────────────────────────────────────────────────
    // Métodos estáticos para llamarlos desde cualquier script sin comprobar si existe el AudioManager.

    public static void Navegar() { if (Instance != null) Instance.ReproducirUI(Instance.sonidoNavegar); }
    public static void Confirmar() { if (Instance != null) Instance.ReproducirUI(Instance.sonidoConfirmar); }
    public static void CambioPersonaje() { if (Instance != null) Instance.ReproducirUI(Instance.sonidoCambioPersonaje); }

    void ReproducirUI(AudioClip clip)
    {
        if (clip == null || _fuenteUI == null) return;
        _fuenteUI.PlayOneShot(clip, volumenInterfaz);
    }

    // ── Sonidos de resultados ─────────────────────────────────────────────────

    /// <summary>Suena el sonido de victoria o de derrota al mostrarse la pantalla de resultados.</summary>
    public void ReproducirSonidoResultado(bool victoria)
    {
        AudioClip clip = victoria ? sonidoVictoria : sonidoDerrota;
        if (clip == null || _fuenteSfx == null) return;

        DetenerSonidoResultado();
        _sonidoResultadoPendiente = StartCoroutine(SonidoResultadoConRetraso(clip));
    }

    // Tiempo real: al mostrar los resultados el juego está en timeScale 0
    IEnumerator SonidoResultadoConRetraso(AudioClip clip)
    {
        if (retrasoSonidoResultado > 0f)
            yield return new WaitForSecondsRealtime(retrasoSonidoResultado);

        _sonidoResultadoPendiente = null;
        _fuenteSfx.PlayOneShot(clip, volumenSonidoResultado);
    }

    /// <summary>Corta el sonido de resultados (al reintentar, pasar de nivel o salir).</summary>
    public void DetenerSonidoResultado()
    {
        if (_sonidoResultadoPendiente != null)
        {
            StopCoroutine(_sonidoResultadoPendiente);
            _sonidoResultadoPendiente = null;
        }
        if (_fuenteSfx != null) _fuenteSfx.Stop();
    }

    AudioSource CrearFuenteMenu()
    {
        AudioSource f = gameObject.AddComponent<AudioSource>();
        f.playOnAwake = false;
        f.loop = false;
        f.spatialBlend = 0f;
        return f;
    }

    // ── Música de menús ───────────────────────────────────────────────────────

    /// <summary>
    /// Arranca la música de menú en loop. Si ya está sonando no la reinicia, así que
    /// pasar de la pantalla principal al lobby y al selector de nivel no la corta.
    /// </summary>
    public void IniciarMusicaMenu()
    {
        if (musicaMenu == null) return;

        // Si estaba bajando el volumen, se cancela y se empieza de nuevo
        if (_fundidoMenu != null) { StopCoroutine(_fundidoMenu); _fundidoMenu = null; }
        else if (_menuActivo) return;

        PararFuentesMenu();
        _menuActivo = true;
        _bucleMenu = StartCoroutine(BucleMenu());
    }

    /// <summary>Baja el volumen de la música de menú y la detiene (al entrar a un nivel).</summary>
    public void DetenerMusicaMenu(bool conFundido = true)
    {
        if (!_menuActivo) return;

        if (_bucleMenu != null) { StopCoroutine(_bucleMenu); _bucleMenu = null; }
        if (_fundidoMenu != null) return;

        if (conFundido && duracionFundidoMenu > 0f)
        {
            _fundidoMenu = StartCoroutine(FundidoMenu());
        }
        else
        {
            PararFuentesMenu();
            _menuActivo = false;
        }
    }

    IEnumerator BucleMenu()
    {
        AudioSource activa = _menuA;
        AudioSource otra = _menuB;

        float cruce = Mathf.Clamp(duracionCruceLoop, 0f, musicaMenu.length / 3f);

        // Primera reproducción
        activa.clip = musicaMenu;
        activa.volume = volumenMenu;
        activa.time = 0f;
        activa.Play();

        while (true)
        {
            // Esperar a que falte "cruce" segundos para terminar
            while (activa.isPlaying && activa.time < musicaMenu.length - cruce)
                yield return null;

            // Mezclar: la actual baja mientras la otra arranca desde el principio
            otra.clip = musicaMenu;
            otra.volume = 0f;
            otra.time = 0f;
            otra.Play();

            float t = 0f;
            while (t < cruce)
            {
                t += Time.unscaledDeltaTime;
                float k = cruce > 0f ? Mathf.Clamp01(t / cruce) : 1f;
                // Curva de potencia constante: el volumen total no se hunde a la mitad de la mezcla
                activa.volume = volumenMenu * Mathf.Cos(k * Mathf.PI * 0.5f);
                otra.volume = volumenMenu * Mathf.Sin(k * Mathf.PI * 0.5f);
                yield return null;
            }

            activa.Stop();
            otra.volume = volumenMenu;

            AudioSource aux = activa; activa = otra; otra = aux;
        }
    }

    IEnumerator FundidoMenu()
    {
        float vA = _menuA.volume;
        float vB = _menuB.volume;
        float t = 0f;

        while (t < duracionFundidoMenu)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duracionFundidoMenu);
            _menuA.volume = Mathf.Lerp(vA, 0f, k);
            _menuB.volume = Mathf.Lerp(vB, 0f, k);
            yield return null;
        }

        PararFuentesMenu();
        _menuActivo = false;
        _fundidoMenu = null;
    }

    void PararFuentesMenu()
    {
        if (_menuA != null) { _menuA.Stop(); _menuA.volume = volumenMenu; }
        if (_menuB != null) { _menuB.Stop(); _menuB.volume = volumenMenu; }
    }

    // ── Música del nivel ──────────────────────────────────────────────────────

    /// <summary>Arranca la canción desde el principio. Llamar al empezar o reiniciar un nivel.</summary>
    /// <param name="duracionNivel">Duración del nivel en segundos, para que la canción termine a la vez.</param>
    public void IniciarMusicaNivel(float duracionNivel = 0f)
    {
        if (musicaNivel == null || _fuenteMusica == null) return;

        ReiniciarAire();
        DetenerSonidoResultado(); // si venías de unos resultados, que su sonido no se solape
        DetenerMusicaMenu(true); // por si el nivel arranca con la música de menú aún sonando
        CancelarFundido();
        CancelarInicioPendiente();
        _pausada = false;

        _fuenteMusica.Stop();
        _fuenteMusica.clip = musicaNivel;
        _fuenteMusica.loop = repetir;
        _fuenteMusica.volume = volumenMusica;
        _fuenteMusica.time = 0f;

        // Si la canción dura más que el nivel, se espera ese sobrante antes de arrancarla
        float espera = 0f;
        if (sincronizarFinal && duracionNivel > 0f)
            espera = Mathf.Max(0f, musicaNivel.length - duracionNivel + ajusteFinal);

        if (espera > 0.01f)
            _inicioPendiente = StartCoroutine(IniciarConRetraso(espera));
        else
            _fuenteMusica.Play();
    }

    // Usa tiempo con escala: si el juego se pausa durante la espera, la espera también se pausa
    IEnumerator IniciarConRetraso(float espera)
    {
        yield return new WaitForSeconds(espera);
        _inicioPendiente = null;
        _fuenteMusica.Play();
    }

    void CancelarInicioPendiente()
    {
        if (_inicioPendiente != null)
        {
            StopCoroutine(_inicioPendiente);
            _inicioPendiente = null;
        }
    }

    public void PausarMusica()
    {
        // El aire también se calla en pausa
        if (_fuenteAire != null && _fuenteAire.isPlaying) _fuenteAire.Pause();
        if (_fuenteAireExtremos != null && _fuenteAireExtremos.isPlaying) _fuenteAireExtremos.Pause();
        if (_fuenteMusica == null || !_fuenteMusica.isPlaying) return;
        _fuenteMusica.Pause();
        _pausada = true;
    }

    public void ReanudarMusica()
    {
        if (_fuenteAire != null && _limpiezasActivas > 0) _fuenteAire.UnPause();
        if (_fuenteAireExtremos != null) _fuenteAireExtremos.UnPause();
        if (_fuenteMusica == null || !_pausada) return;
        _pausada = false;
        _fuenteMusica.UnPause();
    }

    /// <summary>Baja el volumen poco a poco y detiene la canción (fin del nivel).</summary>
    public void DetenerMusicaConFundido()
    {
        if (_fuenteMusica == null) return;
        CancelarInicioPendiente();                                      // si aún no había arrancado, que no arranque
        ReiniciarAire();                                                // el nivel terminó: el aire se apaga
        if (_fundido != null) return;                                   // ya se está apagando
        if (!_fuenteMusica.isPlaying && !_pausada) return;              // ya está detenida

        _fundido = StartCoroutine(Fundido());
    }

    /// <summary>Corta la música al instante (salir al menú).</summary>
    public void DetenerMusicaInmediato()
    {
        if (_fuenteMusica == null) return;

        CancelarFundido();
        CancelarInicioPendiente();
        DetenerSonidoResultado();
        ReiniciarAire();
        _pausada = false;
        _fuenteMusica.Stop();
        _fuenteMusica.volume = volumenMusica;
    }

    // ── Internos ──────────────────────────────────────────────────────────────

    IEnumerator Fundido()
    {
        float volumenInicial = _fuenteMusica.volume;
        float t = 0f;

        while (t < duracionFundido)
        {
            t += Time.unscaledDeltaTime;
            _fuenteMusica.volume = Mathf.Lerp(volumenInicial, 0f, duracionFundido > 0f ? t / duracionFundido : 1f);
            yield return null;
        }

        _fuenteMusica.Stop();
        _fuenteMusica.volume = volumenMusica;
        _pausada = false;
        _fundido = null;
    }

    void CancelarFundido()
    {
        if (_fundido != null)
        {
            StopCoroutine(_fundido);
            _fundido = null;
        }
    }
}