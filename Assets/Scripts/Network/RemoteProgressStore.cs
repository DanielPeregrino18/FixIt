using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Implementación de IProgressStore con guardado local y sincronización con el backend de Render.
/// El juego siempre lee y escribe en local (PlayerPrefs), así funciona sin conexión; un ciclo en
/// segundo plano combina lo local con la nube conservando el mayor número de estrellas de cada nivel.
/// Con mapeo automático de niveles a mundos (1-5=Mundo1, 6-10=Mundo2, etc.)
/// </summary>
public class RemoteProgressStore : IProgressStore
{
    private const float SEGUNDOS_ENTRE_INTENTOS = 60f;

    // null = sin sesión: se guarda solo en local y no se sincroniza
    private string _userId;
    private readonly LocalProgressStore _local = new LocalProgressStore();

    // Empieza en true para comparar con la nube al abrir el juego: ahí se suben los
    // resultados que quedaron guardados sin conexión en una ejecución anterior.
    private bool _pendiente = true;
    private bool _sincronizarYa = false;

    /// <summary>Se dispara cuando la nube trajo estrellas que no estaban en este dispositivo.</summary>
    public event Action ProgresoCambiado;

    public RemoteProgressStore(string userId, MonoBehaviour coroutineRunner)
    {
        _userId = string.IsNullOrEmpty(userId) ? null : userId;
        coroutineRunner.StartCoroutine(CicloDeSincronizacion());
    }

    /// <summary>
    /// Cambia la cuenta con la que se sincroniza (null = sin sesión, solo local).
    /// Al iniciar sesión se sincroniza de inmediato: el progreso local se suma a la cuenta.
    /// </summary>
    /// <summary>Pide una sincronización inmediata (p. ej. al terminar una partida) sin esperar al siguiente ciclo.</summary>
    public void SincronizarAhora()
    {
        _sincronizarYa = true;
    }

    public void CambiarUsuario(string userId)
    {
        string nuevo = string.IsNullOrEmpty(userId) ? null : userId;
        if (nuevo == _userId) return;

        _userId = nuevo;
        _pendiente = true;
        _sincronizarYa = true;
    }

    public int ObtenerEstrellas(int nivel) => _local.ObtenerEstrellas(nivel);

    public int ObtenerNivelDesbloqueado() => _local.ObtenerNivelDesbloqueado();

    public void GuardarCambios() => _local.GuardarCambios();

    public void GuardarEstrellas(int nivel, int estrellas)
    {
        int anteriores = _local.ObtenerEstrellas(nivel);
        _local.GuardarEstrellas(nivel, estrellas);

        if (estrellas > anteriores)
        {
            Debug.Log($"💾 Progreso guardado en local: nivel {nivel} con {estrellas} estrellas");
            _pendiente = true;
            _sincronizarYa = true;
        }
    }

    // Un único ciclo secuencial: nunca hay dos sincronizaciones a la vez.
    // Usa tiempo sin escala porque el juego pone timeScale en 0 en pausa y al terminar un nivel.
    private IEnumerator CicloDeSincronizacion()
    {
        yield return null;

        while (true)
        {
            if (_userId != null && (_pendiente || SessionQueue.HayPendientes))
            {
                _pendiente = false;
                _sincronizarYa = false;
                yield return Sincronizar();
            }

            float espera = SEGUNDOS_ENTRE_INTENTOS;
            while (espera > 0f && !_sincronizarYa)
            {
                espera -= Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    private IEnumerator Sincronizar()
    {
        FixItNetworkManager red = FixItNetworkManager.Instance;
        if (red == null)
        {
            _pendiente = true;
            yield break;
        }

        // Se fija la cuenta al empezar: si la sesión cambia a medio camino, no se mezclan cuentas
        string userId = _userId;
        if (userId == null) yield break;

        bool termino = false;
        bool fallo = false;
        UserProgress remoto = null;

        red.GetProgress(
            userId: userId,
            onSuccess: (progress) =>
            {
                remoto = progress;
                termino = true;
            },
            onError: (error) =>
            {
                // 404 = el usuario aún no tiene progreso en la nube: todo lo local se sube
                if (!error.StartsWith("HTTP 404")) fallo = true;
                termino = true;
            }
        );
        yield return new WaitUntil(() => termino);

        if (fallo)
        {
            _pendiente = true;
            yield break;
        }

        bool cambioLocal = false;

        for (int nivel = 1; nivel <= ProgressManager.TOTAL_NIVELES; nivel++)
        {
            int estrellasRemotas = ObtenerEstrellasRemotas(remoto, nivel);
            int estrellasLocales = _local.ObtenerEstrellas(nivel);

            if (estrellasRemotas > estrellasLocales)
            {
                _local.GuardarEstrellas(nivel, estrellasRemotas);
                cambioLocal = true;
            }
            else if (estrellasLocales > estrellasRemotas)
            {
                termino = false;
                fallo = false;

                red.UpdateProgress(
                    userId: userId,
                    worldId: ObtenerWorldIdParaNivel(nivel),
                    levelId: ObtenerLevelIdParaNivel(nivel),
                    score: 0,
                    starsEarned: estrellasLocales,
                    onSuccess: (_) => termino = true,
                    onError: (error) =>
                    {
                        fallo = true;
                        termino = true;
                    }
                );
                yield return new WaitUntil(() => termino);

                if (fallo)
                {
                    _pendiente = true;
                    break;
                }

                Debug.Log($"✅ Nivel {nivel} sincronizado con la nube: {estrellasLocales} estrellas");
            }
        }

        if (cambioLocal)
        {
            Debug.Log("✅ Progreso actualizado desde la nube");
            ProgresoCambiado?.Invoke();
        }

        if (!_pendiente)
            yield return SessionQueue.Enviar(red);
    }

    private int ObtenerEstrellasRemotas(UserProgress progreso, int nivel)
    {
        WorldProgress world = progreso?.GetWorld(ObtenerWorldIdParaNivel(nivel));
        LevelProgress level = world?.GetLevel(ObtenerLevelIdParaNivel(nivel));
        return level != null ? level.bestStars : 0;
    }

    /// <summary>
    /// Calcula automáticamente el mundo basado en el nivel.
    /// Nivel 1-5 = world_1, Nivel 6-10 = world_2, Nivel 11-15 = world_3, etc.
    /// </summary>
    public static string ObtenerWorldIdParaNivel(int nivel)
    {
        int mundoNumero = ((nivel - 1) / 5) + 1;
        return $"world_{mundoNumero}";
    }

    /// <summary>
    /// Obtiene el ID del nivel (nivel_1, nivel_2, etc.)
    /// </summary>
    public static string ObtenerLevelIdParaNivel(int nivel)
    {
        return $"nivel_{nivel}";
    }
}
