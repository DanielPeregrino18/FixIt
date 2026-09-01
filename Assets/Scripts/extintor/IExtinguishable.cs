/// <summary>
/// Cualquier objeto que pueda incendiarse y apagarse con el extintor
/// implementa esta interfaz (Laptop, Server, etc.)
/// </summary>
public interface IExtinguishable
{
    /// <summary>True si el objeto está en una fase donde se le puede rociar.</summary>
    bool EstaEnPeligro();

    /// <summary>Se llama cada frame que el extintor lo está rociando.</summary>
    void RecibirChorroExtintor(float deltaTime);
}
