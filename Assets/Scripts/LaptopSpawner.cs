using System.Collections.Generic;
using UnityEngine;
 
public class LaptopSpawner : MonoBehaviour
{
    [Header("Configuración de Spawn")]
    public GameObject laptopPrefab;
    public float tiempoEntreSpawns = 4f;
    public int limiteLaptops = 4;
 
    [Header("Materiales de Color (arrastra 4 o 5 desde el Project)")]
    public Material[] materialesColor;
 
    // El renderer de la laptop que recibirá el color
    // Debe ser el MeshRenderer del modelo 3D de la laptop
    [Tooltip("Nombre exacto del objeto hijo que tiene el MeshRenderer de la laptop")]
    public string nombreRendererLaptop = "laptop_modelo"; 
 
    private int _laptopsCreadas = 0;
 
    void Start()
    {
        InvokeRepeating(nameof(SpawnLaptop), 1f, tiempoEntreSpawns);
    }
 
    void SpawnLaptop()
    {
        // 1. Instanciar
        GameObject nuevaLaptop = Instantiate(laptopPrefab, transform.position, laptopPrefab.transform.rotation);
 
        // 2. Asignar problemas aleatorios
        Laptop laptopScript = nuevaLaptop.GetComponent<Laptop>();
        if (laptopScript != null)
            AsignarProblemasAleatorios(laptopScript);
 
        // 3. Asignar color aleatorio
        if (materialesColor != null && materialesColor.Length > 0)
            AsignarColorAleatorio(nuevaLaptop);
 
        // 4. Límite de spawns
        _laptopsCreadas++;
        if (_laptopsCreadas >= limiteLaptops)
            CancelInvoke(nameof(SpawnLaptop));
    }
 
    void AsignarProblemasAleatorios(Laptop laptop)
    {
        // Todos los problemas posibles
        List<Laptop.Problema> todos = new List<Laptop.Problema>
        {
            Laptop.Problema.Limpieza,
            Laptop.Problema.Reparacion,
            Laptop.Problema.Actualizacion,
            Laptop.Problema.NecesitaRAM,
            Laptop.Problema.NecesitaHDD,
            Laptop.Problema.NecesitaProcesador
        };
 
        // Cantidad aleatoria entre 1 y 4
        int cantidad = Random.Range(1, 5);
 
        // Mezclar la lista y tomar los primeros 'cantidad'
        for (int i = 0; i < todos.Count; i++)
        {
            int j = Random.Range(i, todos.Count);
            Laptop.Problema temp = todos[i];
            todos[i] = todos[j];
            todos[j] = temp;
        }
 
        laptop.problemasActivos.Clear();
        for (int i = 0; i < cantidad; i++)
            laptop.problemasActivos.Add(todos[i]);
 
        Debug.Log($"Laptop spawneada con {cantidad} problema(s): {string.Join(", ", laptop.problemasActivos)}");
    }
 
    void AsignarColorAleatorio(GameObject laptop)
    {
        // Buscar el MeshRenderer en los hijos
        MeshRenderer renderer = laptop.GetComponentInChildren<MeshRenderer>();
        if (renderer != null)
        {
            Material colorElegido = materialesColor[Random.Range(0, materialesColor.Length)];
            renderer.material = colorElegido;
        }
        else
        {
            Debug.LogWarning("LaptopSpawner: No se encontró MeshRenderer en la laptop.");
        }
    }
}