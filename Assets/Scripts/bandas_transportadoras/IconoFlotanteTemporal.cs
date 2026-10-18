using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Utilidad reutilizable: muestra un ícono flotando brevemente en cualquier
/// punto del mundo 3D, mirando hacia la cámara, y se destruye solo después
/// de un tiempo. No requiere agregarlo a ningún objeto — se llama directo.
/// </summary>
public static class IconoFlotanteTemporal
{
    /// <param name="posicionMundo">Dónde debe aparecer (normalmente la posición de lo que se está evaluando)</param>
    /// <param name="sprite">Qué ícono mostrar</param>
    /// <param name="duracion">Segundos que permanece visible antes de destruirse solo</param>
    /// <param name="alturaSobrePosicion">Qué tanto más arriba de esa posición aparece</param>
    public static void Mostrar(Vector3 posicionMundo, Sprite sprite, float duracion = 1.5f, float alturaSobrePosicion = 1f, float tamano = 60f, Vector3 rotacionExtra = default)
    {
        if (sprite == null) return;

        GameObject canvasObj = new GameObject("IconoFlotanteTemporal");
        canvasObj.transform.position = posicionMundo + Vector3.up * alturaSobrePosicion;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasScaler>();

        RectTransform canvasRT = canvasObj.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(100f, 100f);
        canvasRT.localScale = Vector3.one * 0.01f;

        GameObject imgObj = new GameObject("Icono", typeof(RectTransform), typeof(Image));
        imgObj.transform.SetParent(canvasObj.transform, false);

        Image img = imgObj.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;

        RectTransform imgRT = imgObj.GetComponent<RectTransform>();
        imgRT.sizeDelta = new Vector2(tamano, tamano);

        // Orientarlo hacia la cámara al aparecer (con la cámara fija de este
        // juego, no hace falta actualizarlo cada frame para algo que dura tan poco),
        // más una rotación extra ajustable para corregir cualquier inclinación.
        if (Camera.main != null)
            canvasObj.transform.rotation = Quaternion.LookRotation(
                canvasObj.transform.position - Camera.main.transform.position) * Quaternion.Euler(rotacionExtra);

        Object.Destroy(canvasObj, duracion);
    }
}