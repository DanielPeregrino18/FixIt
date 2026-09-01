using UnityEngine;

/// <summary>
/// Identifica qué tipo de pieza es este objeto y a qué problema
/// de laptop corresponde. Agrégalo a los prefabs de RAM, HDD y CPU.
/// </summary>
public class HardwarePiece : MonoBehaviour
{
    public enum TipoPieza { RAM, HDD, CPU }

    [Header("Tipo de pieza")]
    public TipoPieza tipoPieza;

    /// <summary>Devuelve el problema de laptop que esta pieza resuelve.</summary>
    public Laptop.Problema ProblemaCorrespondiente()
    {
        switch (tipoPieza)
        {
            case TipoPieza.RAM: return Laptop.Problema.NecesitaRAM;
            case TipoPieza.HDD: return Laptop.Problema.NecesitaHDD;
            case TipoPieza.CPU: return Laptop.Problema.NecesitaProcesador;
            default:            return Laptop.Problema.NecesitaRAM;
        }
    }
}
