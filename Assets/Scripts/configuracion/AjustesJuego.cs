using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ajustes del jugador (volumen, brillo, contraste), guardados en PlayerPrefs.
/// Se aplican solos al arrancar el juego; el menú de Configuración solo cambia los valores.
///
/// Brillo y contraste usan un Volume global propio (URP, Color Adjustments) con prioridad alta,
/// así no se toca el perfil de post-proceso de la escena.
/// </summary>
public static class AjustesJuego
{
    private const string KEY_MUSICA = "FixIT_Ajuste_Musica";
    private const string KEY_EFECTOS = "FixIT_Ajuste_Efectos";
    private const string KEY_BRILLO = "FixIT_Ajuste_Brillo";
    private const string KEY_CONTRASTE = "FixIT_Ajuste_Contraste";

    // Todos van de 0 a 1. Brillo y contraste: 0.5 = sin cambio.
    public static float VolumenMusica => PlayerPrefs.GetFloat(KEY_MUSICA, 1f);
    public static float VolumenEfectos => PlayerPrefs.GetFloat(KEY_EFECTOS, 1f);
    public static float Brillo => PlayerPrefs.GetFloat(KEY_BRILLO, 0.5f);
    public static float Contraste => PlayerPrefs.GetFloat(KEY_CONTRASTE, 0.5f);

    private const float RANGO_EXPOSICION = 1.5f; // ± pasos de exposición en los extremos del brillo
    private const float RANGO_CONTRASTE = 40f;   // ± contraste (URP admite -100 a 100)

    private static ColorAdjustments _ajusteColor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AplicarAlIniciar()
    {
        AplicarImagen();
        // El volumen lo aplica AudioManager en su Awake (ya ocurrió a estas alturas)
    }

    public static void GuardarVolumen(float musica, float efectos)
    {
        PlayerPrefs.SetFloat(KEY_MUSICA, Mathf.Clamp01(musica));
        PlayerPrefs.SetFloat(KEY_EFECTOS, Mathf.Clamp01(efectos));
        if (AudioManager.Instance != null)
            AudioManager.Instance.AplicarVolumenUsuario(musica, efectos);
    }

    public static void GuardarImagen(float brillo, float contraste)
    {
        PlayerPrefs.SetFloat(KEY_BRILLO, Mathf.Clamp01(brillo));
        PlayerPrefs.SetFloat(KEY_CONTRASTE, Mathf.Clamp01(contraste));
        AplicarImagen();
    }

    /// <summary>Escribe a disco (se llama al cerrar el menú, no en cada movimiento de la barra).</summary>
    public static void Guardar() => PlayerPrefs.Save();

    private static void AplicarImagen()
    {
        if (_ajusteColor == null) CrearVolumen();

        _ajusteColor.postExposure.Override(_exposicionBase + (Brillo - 0.5f) * 2f * RANGO_EXPOSICION);
        _ajusteColor.contrast.Override(Mathf.Clamp(_contrasteBase + (Contraste - 0.5f) * 2f * RANGO_CONTRASTE, -100f, 100f));
    }

    // Valores que ya trae el Volume de la escena: el ajuste del usuario se suma encima
    private static float _exposicionBase;
    private static float _contrasteBase;

    private static void LeerValoresDeLaEscena()
    {
        foreach (Volume v in Object.FindObjectsByType<Volume>())
        {
            if (!v.isGlobal || v.sharedProfile == null) continue;
            if (!v.sharedProfile.TryGet(out ColorAdjustments ca)) continue;
            if (ca.postExposure.overrideState) _exposicionBase = ca.postExposure.value;
            if (ca.contrast.overrideState) _contrasteBase = ca.contrast.value;
        }
    }

    private static void CrearVolumen()
    {
        LeerValoresDeLaEscena();

        GameObject go = new GameObject("AjustesJuego_Volume");
        Object.DontDestroyOnLoad(go);

        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f; // por encima del Volume de la escena: solo pisa lo que sobreescribe

        VolumeProfile perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        // false: solo se sobreescriben exposición y contraste (vía Override); saturación,
        // filtro de color, etc. siguen saliendo del Volume de la escena
        _ajusteColor = perfil.Add<ColorAdjustments>(false);
        volume.profile = perfil;
    }
}
