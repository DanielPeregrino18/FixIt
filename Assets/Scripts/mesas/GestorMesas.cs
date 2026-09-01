using System.Collections.Generic;
using UnityEngine;

public class GestorMesas : MonoBehaviour
{
    [Header("Sistema de Mesas")]
    public SlotTable[] mesasDeLlegada;

    [Header("Bote de Desechos")]
    public WasteBin boteDDesechos;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Laptop"))
        {
            LaptopController controlador = other.GetComponent<LaptopController>();

            if (controlador != null && controlador.yaLlego == false)
            {
                controlador.yaLlego = true;
                AcomodarLaptop(other.gameObject, controlador);
            }
        }
    }

    void AcomodarLaptop(GameObject nuevaLaptop, LaptopController controladorNueva)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RegistrarLaptopLlegada();

        // 1. Verificar si hay espacio
        bool hayEspacio = false;
        for (int i = 0; i < mesasDeLlegada.Length; i++)
        {
            if (mesasDeLlegada[i].currentItemOnTable == null)
            {
                hayEspacio = true;
                break;
            }
        }

        // 2. Sin espacio — nueva laptop va directo al bote
        if (!hayEspacio)
        {
            Debug.Log("GestorMesas: Slots llenos, laptop nueva al bote.");
            if (boteDDesechos != null)
                boteDDesechos.RecibirLaptop(nuevaLaptop);
            else
            {
                Destroy(nuevaLaptop);
                if (GameManager.Instance != null)
                    GameManager.Instance.RegistrarLaptopPerdida();
            }
            return;
        }

        // 3. Hay espacio — recoger laptops existentes
        List<GameObject> laptopsActuales = new List<GameObject>();
        for (int i = 0; i < mesasDeLlegada.Length; i++)
        {
            if (mesasDeLlegada[i].currentItemOnTable != null)
                laptopsActuales.Add(mesasDeLlegada[i].currentItemOnTable);
        }

        // 4. Nueva lista: nueva primero, luego las existentes
        List<GameObject> nuevaLista = new List<GameObject>();
        nuevaLista.Add(nuevaLaptop);
        nuevaLista.AddRange(laptopsActuales);

        // 5. Limpiar slots y reasignar compactado
        for (int i = 0; i < mesasDeLlegada.Length; i++)
            mesasDeLlegada[i].currentItemOnTable = null;

        for (int i = 0; i < nuevaLista.Count; i++)
        {
            SlotTable mesa    = mesasDeLlegada[i];
            GameObject laptop = nuevaLista[i];

            mesa.currentItemOnTable = laptop;
            laptop.transform.SetParent(mesa.tableSocket);

            LaptopController lc = laptop.GetComponent<LaptopController>();
            if (lc != null) lc.MoverASlot(mesa.tableSocket.position);
        }
    }
}