/// <summary>
/// Contrato de guardado de progreso. Cualquier fuente de datos
/// (local, base de datos remota, etc.) implementa esta interfaz.
/// El resto del juego solo conoce IProgressStore, nunca la implementación
/// concreta — así cambiar de local a remoto no toca ningún otro script.
/// </summary>
public interface IProgressStore
{
    /// <summary>Estrellas guardadas para un nivel (0 si nunca se jugó).</summary>
    int ObtenerEstrellas(int nivel);

    /// <summary>
    /// Guarda el resultado de un nivel. Si ya había un resultado mejor,
    /// la implementación decide si lo conserva (normalmente sí).
    /// </summary>
    void GuardarEstrellas(int nivel, int estrellas);

    /// <summary>Número del nivel más alto desbloqueado (empieza en 1).</summary>
    int ObtenerNivelDesbloqueado();

    /// <summary>Persiste los cambios inmediatamente (flush a disco/red).</summary>
    void GuardarCambios();
}
