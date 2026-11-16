using UnityEngine;

/// <summary>
/// Animación procedural de los personajes, estilo Overcooked: se suma ENCIMA de las poses
/// del Animator para que los personajes se sientan vivos.
///
/// - Caminar: rebote del cuerpo al ritmo del paso, balanceo tipo pingüino, cabeza con
///   resorte que se mece, se inclina al correr / en las curvas y cabecea al frenar.
/// - Acciones: cabeceo en cada martillazo, temblor al soplar con la lata de aire,
///   retroceso con el extintor, cabeza atrás al beber café y sacudida al terminar.
/// - Quieto: respiración sutil.
///
/// Esqueletos volteados: todos los movimientos se calculan en los ejes del JUGADOR
/// (adelante / arriba / derecha de este objeto) y se aplican como rotaciones de mundo
/// sobre el hueso. Así no importa cómo esté orientado el hueso en cada modelo.
///
/// Va en el prefab del jugador. CharacterSwapper actualiza headBone al cambiar de
/// personaje; el cuerpo se mueve con el hijo "Armature", que se busca solo.
/// </summary>
public class ProceduralHead : MonoBehaviour
{
    [Header("⚙️ Referencias")]
    [Tooltip("Hueso de la cabeza o cuello. CharacterSwapper lo asigna al elegir personaje.")]
    public Transform headBone;

    [Tooltip("Ya no se usa: los movimientos se calculan en los ejes del jugador y funcionan con cualquier esqueleto.")]
    public bool invertirDireccion = false;

    // ─────────────────────────────────────────────────────────────────────────
    [Header("🚶 Caminar")]
    public bool animarCaminar = true;
    [Tooltip("Pasos por segundo a velocidad normal (al correr se aceleran)")]
    [Range(0.5f, 5f)] public float pasosPorSegundo = 2.6f;
    [Tooltip("Altura del rebote del cuerpo en cada paso (metros)")]
    [Range(0f, 0.2f)] public float alturaRebote = 0.05f;
    [Tooltip("Grados que el cuerpo se ladea de un lado a otro al caminar (tipo pingüino)")]
    [Range(0f, 20f)] public float balanceoCuerpo = 3f;
    [Tooltip("Grados que el cuerpo se inclina hacia adelante a toda velocidad")]
    [Range(0f, 20f)] public float inclinacionCuerpo = 5f;
    [Tooltip("Grados que la cabeza se mece de lado a lado al caminar")]
    [Range(0f, 30f)] public float balanceoCabeza = 4f;
    [Tooltip("Grados que la cabeza se inclina hacia adelante al caminar")]
    [Range(0f, 30f)] public float inclinacionCabeza = 3f;
    [Tooltip("Grados que la cabeza se echa hacia ATRÁS al correr (Shift / RT). " +
             "Ojo: el cuerpo se inclina hacia adelante al correr y se lleva la cabeza, así que se nota un poco menos.")]
    [Range(0f, 50f)] public float cabezaAtrasCorrer = 32f;
    [Tooltip("Cuánto reacciona la cabeza a acelerar, frenar y girar (inercia)")]
    [Range(0f, 2f)] public float inerciaCabeza = 0.35f;

    [Header("🌀 Resorte de la cabeza")]
    [Tooltip("Rigidez: más alto = regresa más rápido")]
    [Range(20f, 400f)] public float rigidez = 130f;
    [Tooltip("Amortiguación: más bajo = rebota más (gelatina), más alto = se frena seco")]
    [Range(2f, 40f)] public float amortiguacion = 11f;
    [Tooltip("Límite de grados que puede girar la cabeza en cualquier dirección")]
    [Range(5f, 60f)] public float anguloMaximo = 40f;

    [Header("🔨 Acciones")]
    public bool animarAcciones = true;
    [Tooltip("Fuerza del cabeceo en cada martillazo")]
    [Range(0f, 800f)] public float impulsoMartillazo = 200f;
    [Tooltip("Grados del temblor de la cabeza mientras sopla con la lata de aire")]
    [Range(0f, 10f)] public float temblorLata = 2.5f;
    [Tooltip("Grados que la cabeza se echa atrás mientras rocía el extintor")]
    [Range(0f, 30f)] public float retrocesoExtintor = 8f;
    [Tooltip("Grados de vibración (cabeza y cuerpo) mientras rocía el extintor")]
    [Range(0f, 10f)] public float vibracionExtintor = 1.8f;
    [Tooltip("Grados que la cabeza se echa atrás al beber café")]
    [Range(0f, 45f)] public float inclinacionCafe = 10f;
    [Tooltip("Fuerza de la sacudida de '¡energía!' al terminar el café")]
    [Range(0f, 800f)] public float sacudidaCafe = 150f;

    [Header("😮‍💨 Quieto")]
    public bool animarRespiracion = true;
    [Tooltip("Altura del sube y baja al respirar (metros)")]
    [Range(0f, 0.05f)] public float alturaRespiracion = 0.008f;
    [Tooltip("Respiraciones por segundo")]
    [Range(0.1f, 1.5f)] public float ritmoRespiracion = 0.35f;
    [Tooltip("Estando quieto un rato, de vez en cuando gira la cabeza a un lado")]
    public bool mirarALosLados = true;
    [Tooltip("Segundos sin moverse antes de empezar a mirar a los lados")]
    [Range(0.5f, 10f)] public float tiempoQuietoParaMirar = 2.5f;
    [Tooltip("Segundos entre una mirada y la siguiente (mínimo y máximo)")]
    public Vector2 intervaloMirar = new Vector2(3f, 6f);
    [Tooltip("Grados máximos que gira la cabeza al mirar a un lado")]
    [Range(0f, 60f)] public float anguloMirar = 25f;
    [Tooltip("Segundos que se queda mirando antes de regresar")]
    [Range(0.3f, 4f)] public float duracionMirar = 1.2f;

    [Header("🎉 Reacciones")]
    public bool animarReacciones = true;
    [Tooltip("Altura del brinquito al entregar una laptop bien (metros)")]
    [Range(0f, 0.4f)] public float alturaFestejo = 0.12f;
    [Tooltip("Grados que baja la cabeza al entregar mal")]
    [Range(0f, 40f)] public float anguloDesanimo = 15f;
    [Tooltip("Segundos que dura el desánimo")]
    [Range(0.2f, 3f)] public float duracionDesanimo = 1f;
    [Tooltip("Distancia (metros) a la que una explosión asusta")]
    [Range(1f, 20f)] public float radioSusto = 6f;
    [Tooltip("Altura del saltito del susto, a quemarropa (metros)")]
    [Range(0f, 0.3f)] public float alturaSusto = 0.08f;
    [Tooltip("Fuerza con la que la cabeza se va atrás del susto")]
    [Range(0f, 800f)] public float impulsoSusto = 220f;
    [Tooltip("Grados que el cuerpo se aleja de la explosión")]
    [Range(0f, 20f)] public float inclinacionSusto = 6f;
    [Tooltip("Cuánto baja el cuerpo al agarrar un objeto (metros)")]
    [Range(0f, 0.15f)] public float sentonAgarrar = 0.04f;
    [Tooltip("Cuánto baja el cuerpo al soltar un objeto (metros)")]
    [Range(0f, 0.15f)] public float sentonSoltar = 0.02f;

    // ── Nombres en el Animator ───────────────────────────────────────────────
    private static readonly int H_GOLPE_MARTILLO = Animator.StringToHash("Golpe_Martillo");
    private const string P_LATA = "LimpiandoLata";
    private const string P_CAFE = "TomandoCafe";
    private const string P_MARTILLO = "SosteniendoMartillo";

    // Todos los personajes activos, para avisarles de las explosiones cercanas
    private static readonly System.Collections.Generic.List<ProceduralHead> _activos =
        new System.Collections.Generic.List<ProceduralHead>();

    // ── Referencias internas ─────────────────────────────────────────────────
    private Animator _anim;
    private PlayerInteraction _interaccion;
    private PlayerMovement _movimiento;
    private Transform _armature;

    private RuntimeAnimatorController _controladorRevisado;
    private bool _tieneParamLata, _tieneParamCafe, _tieneParamMartillo;
    private bool[] _enGolpePorCapa = new bool[0];
    private bool _cafeAntes;

    // ── Pose base (sin nuestro desplazamiento) ───────────────────────────────
    // Patrón: en Update se devuelve el hueso a la pose sin desplazamiento; luego el Animator
    // (si anima ese hueso) la sobreescribe; en LateUpdate se toma la pose resultante como base
    // y se suma el desplazamiento. Funciona igual con huesos animados y sin animar, sin acumular.
    private Transform _cabezaConBase;
    private Quaternion _baseCabeza;
    private Transform _armatureConBase;
    private Vector3 _basePosArmature;
    private Quaternion _baseRotArmature;

    // ── Estado del movimiento ────────────────────────────────────────────────
    private Vector3 _ultimaPos;
    private Vector3 _velSuave;
    private Vector3 _velAnterior;
    private Vector3 _acelLocal;
    private float _factorMov;   // 0 quieto … 1 velocidad normal (puede pasar de 1 al correr)
    private float _fase;        // fase de los pasos: cada 1.0 es un paso
    private float _tiempo;

    // Resorte de la cabeza: x = pitch (+ hacia adelante), y = roll (+ hacia la derecha)
    private Vector2 _cabeza;
    private Vector2 _cabezaVel;

    // Giro de la cabeza a los lados (yaw, + hacia la derecha), con su propio resorte suave
    private float _yaw, _yawVel, _yawObjetivo;
    private float _tiempoQuieto;
    private float _timerMirar;      // cuenta atrás hasta la próxima mirada
    private float _timerSostener;   // cuánto falta para regresar la cabeza al frente
    private bool _ocupado;          // reparando, limpiando, rociando o bebiendo: no mira a los lados

    // Reacciones del cuerpo
    private float _salto, _saltoVel;                 // brinquitos (arco con gravedad)
    private float _senton, _sentonVel;               // bajar y rebotar al agarrar / soltar (resorte)
    private Vector2 _cuerpoExtra, _cuerpoExtraVel;   // inclinación extra (x = adelante, y = derecha)
    private float _timerDesanimo;
    private bool _sosteniaAntes;
    private const float GRAVEDAD_SALTO = 22f;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        _anim = GetComponent<Animator>();
        _interaccion = GetComponent<PlayerInteraction>();
        _movimiento = GetComponent<PlayerMovement>();
        _ultimaPos = transform.position;
        _sosteniaAntes = _interaccion != null && _interaccion.isHoldingItem;
        _timerMirar = Random.Range(intervaloMirar.x, intervaloMirar.y);
    }

    void OnEnable()
    {
        if (!_activos.Contains(this)) _activos.Add(this);
    }

    void OnDisable()
    {
        _activos.Remove(this);

        // Dejar los huesos como estaban
        RestaurarBase();
        _cabezaConBase = null;
        _armatureConBase = null;
    }

    void Update()
    {
        RestaurarBase();
    }

    void LateUpdate()
    {
        BuscarArmature();
        TomarBase();

        float dt = Time.deltaTime;
        if (dt > 0f)
        {
            _tiempo += dt;
            ActualizarMovimiento(dt);
            ActualizarResorteCabeza(dt);
            ActualizarMirarLados(dt);
            ActualizarReacciones(dt);
        }
        // Con el juego en pausa (dt = 0) no se avanza nada, pero se mantiene la pose actual

        AplicarCuerpo();
        AplicarCabeza();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Pose base
    // ═════════════════════════════════════════════════════════════════════════

    void BuscarArmature()
    {
        // Tras cambiar de personaje, el Armature viejo se destruye y aparece uno nuevo
        if (_armature == null || _armature.parent != transform)
            _armature = transform.Find("Armature");
    }

    void RestaurarBase()
    {
        if (_cabezaConBase != null) _cabezaConBase.localRotation = _baseCabeza;
        if (_armatureConBase != null)
        {
            _armatureConBase.localPosition = _basePosArmature;
            _armatureConBase.localRotation = _baseRotArmature;
        }
    }

    void TomarBase()
    {
        _cabezaConBase = headBone;
        if (headBone != null) _baseCabeza = headBone.localRotation;

        _armatureConBase = _armature;
        if (_armature != null)
        {
            _basePosArmature = _armature.localPosition;
            _baseRotArmature = _armature.localRotation;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Movimiento y acciones
    // ═════════════════════════════════════════════════════════════════════════

    void ActualizarMovimiento(float dt)
    {
        // Velocidad horizontal, suavizada; se limita para ignorar teletransportes (spawn, reinicio)
        Vector3 vel = (transform.position - _ultimaPos) / dt;
        _ultimaPos = transform.position;
        vel.y = 0f;
        if (vel.sqrMagnitude > 20f * 20f) vel = Vector3.zero;

        _velSuave = Vector3.Lerp(_velSuave, vel, 1f - Mathf.Exp(-12f * dt));

        Vector3 acel = (_velSuave - _velAnterior) / dt;
        _velAnterior = _velSuave;
        acel = Vector3.ClampMagnitude(acel, 40f);
        _acelLocal = Vector3.Lerp(_acelLocal, transform.InverseTransformDirection(acel), 1f - Mathf.Exp(-15f * dt));

        float velRef = _movimiento != null && _movimiento.speed > 0.1f ? _movimiento.speed : 5f;
        float rapidez = _velSuave.magnitude;
        float objetivo = rapidez < 0.15f ? 0f : rapidez / velRef;
        _factorMov = Mathf.MoveTowards(_factorMov, Mathf.Min(objetivo, 1.5f), dt * 6f);

        // Los pasos se aceleran con la velocidad (al correr), pero sin exagerar
        if (_factorMov > 0.01f)
            _fase += dt * pasosPorSegundo * Mathf.Clamp(_factorMov, 0.8f, 1.35f);
        else
            _fase = Mathf.MoveTowards(_fase, Mathf.Round(_fase), dt * 2f); // termina el paso con suavidad

        if (_fase > 1000f) _fase -= 1000f; // evitar perder precisión (1000 es par: no cambia el lado)
    }

    void ActualizarResorteCabeza(float dt)
    {
        Vector2 objetivo = Vector2.zero;

        if (animarCaminar)
        {
            float mov = Mathf.Clamp01(_factorMov);

            // Inercia: al acelerar la cabeza se queda atrás, al frenar se va adelante,
            // en las curvas se va hacia afuera
            objetivo.x += -_acelLocal.z * inerciaCabeza;
            objetivo.y += -_acelLocal.x * inerciaCabeza;

            // Al caminar un poco hacia adelante; al correr pasa a ir hacia atrás.
            // Bamboleo lateral con cada paso.
            float correr = FactorCorrer();
            objetivo.x += Mathf.Lerp(inclinacionCabeza * mov, -cabezaAtrasCorrer, correr);
            objetivo.y += balanceoCabeza * mov * Mathf.Sin(_fase * Mathf.PI);
        }

        _ocupado = EstaOcupado();
        if (animarAcciones) objetivo += AccionesCabeza();

        // Desánimo tras una entrega incorrecta: cabeza abajo un momento
        if (animarReacciones && _timerDesanimo > 0f) objetivo.x += anguloDesanimo;

        objetivo.x = Mathf.Clamp(objetivo.x, -anguloMaximo, anguloMaximo);
        objetivo.y = Mathf.Clamp(objetivo.y, -anguloMaximo, anguloMaximo);

        // Resorte amortiguado: da el retraso y el rebote tipo "gelatina".
        // Se integra en pasos pequeños para que sea estable aunque el framerate baje.
        int pasos = Mathf.Clamp(Mathf.CeilToInt(dt / 0.008f), 1, 8);
        float h = dt / pasos;
        for (int i = 0; i < pasos; i++)
        {
            Vector2 acel = rigidez * (objetivo - _cabeza) - amortiguacion * _cabezaVel;
            _cabezaVel += acel * h;
            _cabeza += _cabezaVel * h;
        }

        // Límite duro (también evita NaN si algo raro pasara)
        if (float.IsNaN(_cabeza.x) || float.IsNaN(_cabeza.y)) { _cabeza = Vector2.zero; _cabezaVel = Vector2.zero; }
        _cabeza.x = Mathf.Clamp(_cabeza.x, -anguloMaximo, anguloMaximo);
        _cabeza.y = Mathf.Clamp(_cabeza.y, -anguloMaximo, anguloMaximo);
    }

    /// <summary>Objetivo extra de la cabeza según lo que esté haciendo el personaje; también dispara los impulsos.</summary>
    Vector2 AccionesCabeza()
    {
        Vector2 extra = Vector2.zero;

        // El Animator puede estar apagado un momento (p. ej. NormalTable lo apaga al instalar piezas)
        bool animListo = _anim != null && _anim.isActiveAndEnabled && _anim.runtimeAnimatorController != null;
        if (animListo) RevisarParametros();

        // Martillazo: cada vez que el Animator entra al estado del golpe, un cabeceo
        if (animListo && DetectarEntradaGolpe())
            Impulso(new Vector2(impulsoMartillazo, Random.Range(-0.25f, 0.25f) * impulsoMartillazo));

        // Lata de aire: temblor rápido y ligero
        if (animListo && _tieneParamLata && _anim.GetBool(P_LATA))
            extra += Temblor(17f, temblorLata);

        // Extintor: cabeza atrás "aguantando" y vibración
        if (Rociando())
        {
            extra.x -= retrocesoExtintor;
            extra += Temblor(24f, vibracionExtintor);
        }

        // Café: cabeza atrás mientras bebe; al terminar, sacudida de energía
        bool cafe = animListo ? (_tieneParamCafe && _anim.GetBool(P_CAFE)) : _cafeAntes;
        if (cafe) extra.x -= inclinacionCafe;
        if (_cafeAntes && !cafe) Impulso(new Vector2(-0.3f * sacudidaCafe, sacudidaCafe));
        _cafeAntes = cafe;

        return extra;
    }

    /// <summary>True mientras repara, limpia, rocía o bebe café (en esos momentos no mira a los lados).</summary>
    bool EstaOcupado()
    {
        if (Rociando()) return true;
        if (_anim == null || !_anim.isActiveAndEnabled || _anim.runtimeAnimatorController == null) return false;
        RevisarParametros();
        return (_tieneParamLata && _anim.GetBool(P_LATA)) ||
               (_tieneParamCafe && _anim.GetBool(P_CAFE)) ||
               (_tieneParamMartillo && _anim.GetBool(P_MARTILLO));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Mirar a los lados (estando quieto)
    // ═════════════════════════════════════════════════════════════════════════

    void ActualizarMirarLados(float dt)
    {
        bool quieto = _factorMov < 0.05f && !_ocupado && mirarALosLados;

        if (!quieto)
        {
            // Al moverse o ponerse a trabajar, la cabeza regresa al frente y se reinicia la espera
            _tiempoQuieto = 0f;
            _yawObjetivo = 0f;
            _timerSostener = 0f;
            _timerMirar = Random.Range(intervaloMirar.x, intervaloMirar.y);
        }
        else
        {
            _tiempoQuieto += dt;

            if (_timerSostener > 0f)
            {
                _timerSostener -= dt;
                if (_timerSostener <= 0f) _yawObjetivo = 0f; // regresar al frente
            }
            else if (_tiempoQuieto >= tiempoQuietoParaMirar)
            {
                _timerMirar -= dt;
                if (_timerMirar <= 0f)
                {
                    float lado = Random.value < 0.5f ? -1f : 1f;
                    _yawObjetivo = lado * anguloMirar * Random.Range(0.6f, 1f);
                    _timerSostener = duracionMirar;
                    _timerMirar = Random.Range(intervaloMirar.x, Mathf.Max(intervaloMirar.x, intervaloMirar.y));
                }
            }
        }

        // Resorte suave y casi sin rebote: un giro tranquilo, no un latigazo
        const float k = 40f, c = 11f;
        _yawVel += (k * (_yawObjetivo - _yaw) - c * _yawVel) * dt;
        _yaw += _yawVel * dt;
        _yaw = Mathf.Clamp(_yaw, -60f, 60f);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Reacciones: festejo, desánimo, susto, agarrar / soltar
    // ═════════════════════════════════════════════════════════════════════════

    void ActualizarReacciones(float dt)
    {
        // Agarrar / soltar: lo detecta solo, sin importar desde dónde se agarró el objeto
        bool sostiene = _interaccion != null && _interaccion.isHoldingItem;
        if (animarReacciones && sostiene != _sosteniaAntes)
            ImpulsoSenton(sostiene ? sentonAgarrar : sentonSoltar);
        _sosteniaAntes = sostiene;

        if (_timerDesanimo > 0f) _timerDesanimo -= dt;

        // Brinquitos: arco simple con gravedad, hasta tocar el piso
        if (_salto > 0f || _saltoVel > 0f)
        {
            _saltoVel -= GRAVEDAD_SALTO * dt;
            _salto += _saltoVel * dt;
            if (_salto <= 0f) { _salto = 0f; _saltoVel = 0f; }
        }

        // Sentón: resorte que baja y rebota
        const float kS = 180f, cS = 15f;
        _sentonVel += (-kS * _senton - cS * _sentonVel) * dt;
        _senton += _sentonVel * dt;

        // Inclinación extra del cuerpo (susto, desánimo), con resorte
        Vector2 objetivo = Vector2.zero;
        if (animarReacciones && _timerDesanimo > 0f) objetivo.x += anguloDesanimo * 0.35f; // encorvado
        const float kC = 120f, cC = 13f;
        _cuerpoExtraVel += (kC * (objetivo - _cuerpoExtra) - cC * _cuerpoExtraVel) * dt;
        _cuerpoExtra += _cuerpoExtraVel * dt;
        _cuerpoExtra = Vector2.ClampMagnitude(_cuerpoExtra, 25f);
    }

    void Saltar(float altura)
    {
        if (altura <= 0f) return;
        float vel = Mathf.Sqrt(2f * GRAVEDAD_SALTO * altura);
        _saltoVel = Mathf.Max(_saltoVel, vel); // si ya va en el aire, no se acumula
        if (_salto <= 0f) _salto = 0.0001f;
    }

    void ImpulsoSenton(float profundidad)
    {
        // Velocidad hacia abajo que, con el resorte, baja aproximadamente "profundidad"
        _sentonVel -= profundidad * 13.4f; // ≈ √rigidez del resorte del sentón
    }

    /// <summary>Al entregar una laptop: brinquito si estaba completa, desánimo si no.</summary>
    public void ReaccionEntrega(bool correcta)
    {
        if (!animarReacciones || !isActiveAndEnabled) return;

        if (correcta)
        {
            Saltar(alturaFestejo);
            float lado = Random.value < 0.5f ? -1f : 1f;
            Impulso(new Vector2(-60f, lado * 180f)); // meneo alegre de cabeza
            _timerDesanimo = 0f;
        }
        else
        {
            _timerDesanimo = duracionDesanimo;
        }
    }

    /// <summary>
    /// Avisa de una explosión: los personajes cercanos se asustan (más fuerte mientras más cerca).
    /// Llamar desde laptops calcinadas, servidores que explotan, etc.
    /// </summary>
    public static void Susto(Vector3 posicion, float fuerza = 1f)
    {
        for (int i = _activos.Count - 1; i >= 0; i--)
        {
            ProceduralHead p = _activos[i];
            if (p == null) { _activos.RemoveAt(i); continue; }
            p.Asustar(posicion, fuerza);
        }
    }

    void Asustar(Vector3 posicion, float fuerza)
    {
        if (!animarReacciones) return;

        Vector3 desde = transform.position - posicion; // de la explosión hacia el personaje
        desde.y = 0f;
        float distancia = desde.magnitude;
        if (distancia > radioSusto) return;

        float k = Mathf.Clamp01(1f - distancia / radioSusto) * Mathf.Max(0f, fuerza);
        if (k <= 0.01f) return;

        Saltar(alturaSusto * k);
        Impulso(new Vector2(-impulsoSusto * k, Random.Range(-0.3f, 0.3f) * impulsoSusto * k)); // cabeza atrás

        // El cuerpo se aleja de la explosión: se calcula en los ejes del personaje
        Vector3 dirLocal = distancia > 0.01f ? transform.InverseTransformDirection(desde / distancia) : Vector3.back;
        Vector2 alejarse = new Vector2(dirLocal.z, dirLocal.x) * inclinacionSusto * k;
        _cuerpoExtraVel += alejarse * 11f; // impulso al resorte de inclinación (≈ √rigidez)
    }

    /// <summary>0 caminando … 1 corriendo a toda velocidad (sprint), según la velocidad real.</summary>
    float FactorCorrer()
    {
        if (_movimiento == null || _movimiento.speed <= 0.1f) return 0f;
        float extraSprint = _movimiento.sprintSpeed / _movimiento.speed - 1f; // p. ej. 7/5 - 1 = 0.4
        if (extraSprint <= 0.01f) return 0f;
        // Llega al máximo al 70% del extra del sprint: la velocidad real casi nunca toca el tope exacto
        return Mathf.Clamp01((_factorMov - 1f) / (extraSprint * 0.7f));
    }

    /// <summary>Empujón instantáneo a la cabeza (grados/segundo en pitch y roll). Para usar desde otros scripts.</summary>
    public void Impulso(Vector2 velocidad)
    {
        _cabezaVel += velocidad;
    }

    bool Rociando() => _interaccion != null && _interaccion.RociandoExtintor;

    Vector2 Temblor(float frecuencia, float amplitud)
    {
        // Ruido suave (no aleatorio puro) para que tiemble sin verse "glitch"
        float t = _tiempo * frecuencia;
        return new Vector2((Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f * amplitud,
                           (Mathf.PerlinNoise(0.71f, t) - 0.5f) * 2f * amplitud);
    }

    /// <summary>Revisa qué parámetros tiene el Animator (cambia al aplicar el Override Controller del personaje).</summary>
    void RevisarParametros()
    {
        if (_anim.runtimeAnimatorController == _controladorRevisado) return;

        _controladorRevisado = _anim.runtimeAnimatorController;
        _tieneParamLata = _tieneParamCafe = _tieneParamMartillo = false;
        foreach (AnimatorControllerParameter p in _anim.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Bool) continue;
            if (p.name == P_LATA) _tieneParamLata = true;
            if (p.name == P_CAFE) _tieneParamCafe = true;
            if (p.name == P_MARTILLO) _tieneParamMartillo = true;
        }
        _enGolpePorCapa = new bool[_anim.layerCount];
    }

    bool DetectarEntradaGolpe()
    {
        if (_enGolpePorCapa.Length != _anim.layerCount) _enGolpePorCapa = new bool[_anim.layerCount];

        bool entro = false;
        for (int i = 0; i < _anim.layerCount; i++)
        {
            bool enGolpe = _anim.GetCurrentAnimatorStateInfo(i).shortNameHash == H_GOLPE_MARTILLO ||
                           (_anim.IsInTransition(i) && _anim.GetNextAnimatorStateInfo(i).shortNameHash == H_GOLPE_MARTILLO);
            if (enGolpe && !_enGolpePorCapa[i]) entro = true;
            _enGolpePorCapa[i] = enGolpe;
        }
        return entro;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Aplicar a los huesos (siempre en ejes del jugador → funciona con esqueletos volteados)
    // ═════════════════════════════════════════════════════════════════════════

    void AplicarCuerpo()
    {
        if (_armature == null) return;

        Vector3 arriba = transform.up;
        Vector3 derecha = transform.right;
        Vector3 adelante = transform.forward;

        float alto = 0f;
        float lean = 0f;   // + hacia adelante
        float roll = 0f;   // + hacia la derecha

        if (animarCaminar)
        {
            float mov = Mathf.Clamp01(_factorMov);
            float paso = _fase * Mathf.PI;
            alto += alturaRebote * mov * Mathf.Abs(Mathf.Sin(paso)); // un rebote por paso
            roll += balanceoCuerpo * mov * Mathf.Sin(paso);          // un lado por paso
            lean += inclinacionCuerpo * Mathf.Clamp01(_factorMov / 1.3f);
        }

        if (animarRespiracion)
        {
            float quieto = 1f - Mathf.Clamp01(_factorMov * 3f);
            alto += alturaRespiracion * quieto * (0.5f + 0.5f * Mathf.Sin(_tiempo * ritmoRespiracion * 2f * Mathf.PI));
        }

        // Reacciones: brinquitos, sentón al agarrar/soltar e inclinación extra (susto, desánimo)
        alto += _salto + _senton;
        lean += _cuerpoExtra.x;
        roll += _cuerpoExtra.y;

        if (animarAcciones && Rociando())
        {
            lean -= retrocesoExtintor * 0.4f; // el cuerpo también se echa un poco atrás
            Vector2 v = Temblor(24f, vibracionExtintor * 0.5f);
            lean += v.x;
            roll += v.y;
        }

        // Rotación alrededor del pivote del Armature (normalmente los pies)
        Quaternion giro = Quaternion.AngleAxis(lean, derecha) * Quaternion.AngleAxis(-roll, adelante);
        _armature.rotation = giro * _armature.rotation;

        // Rebote: desplazamiento hacia "arriba" del jugador, convertido al espacio del padre
        if (alto != 0f && _armature.parent != null)
            _armature.localPosition += _armature.parent.InverseTransformVector(arriba * alto);
    }

    void AplicarCabeza()
    {
        if (headBone == null) return;

        // yaw + = mirar a la derecha (alrededor de "arriba"),
        // pitch + = cabeza hacia adelante (alrededor de "derecha"),
        // roll + = cabeza hacia la derecha (alrededor de "adelante", con signo invertido en Unity)
        Quaternion giro = Quaternion.AngleAxis(_yaw, transform.up) *
                          Quaternion.AngleAxis(_cabeza.x, transform.right) *
                          Quaternion.AngleAxis(-_cabeza.y, transform.forward);
        headBone.rotation = giro * headBone.rotation;
    }
}
