using UnityEngine;
using UnityEngine.UI;

public class NewsDisplayManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Transform newsContainer;
    [SerializeField] private GameObject newsItemPrefab;
    [SerializeField] private Text titleText;
    [SerializeField] private Text contentText;
    [SerializeField] private Text authorText;
    [SerializeField] private Text dateText;

    [Header("Settings")]
    [SerializeField] private int maxNewsToShow = 5;

    private void Start()
    {
        LoadNews();
    }

    public void LoadNews()
    {
        if (newsContainer != null)
        {
            // Limpiar noticias anteriores
            foreach (Transform child in newsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        FixItNetworkManager.Instance.GetNews(
            onSuccess: (newsList) =>
            {
                Debug.Log($"📰 {newsList.Count} noticias cargadas");

                int count = Mathf.Min(newsList.Count, maxNewsToShow);

                for (int i = 0; i < count; i++)
                {
                    DisplayNewsItem(newsList[i]);
                }
            },
            onError: (error) =>
            {
                Debug.LogError($"❌ Error cargando noticias: {error}");
            }
        );
    }

    private void DisplayNewsItem(NewsItem news)
    {
        if (newsItemPrefab != null && newsContainer != null)
        {
            GameObject newsObj = Instantiate(newsItemPrefab, newsContainer);

            // Configurar textos si el prefab tiene estos componentes
            Text[] texts = newsObj.GetComponentsInChildren<Text>();
            foreach (Text text in texts)
            {
                if (text.name.Contains("Title") || text.name == "Title")
                    text.text = news.title;
                else if (text.name.Contains("Content") || text.name == "Content")
                    text.text = news.content;
                else if (text.name.Contains("Author") || text.name == "Author")
                    text.text = $"Por: {news.author}";
                else if (text.name.Contains("Date") || text.name == "Date")
                    text.text = news.publishedAt;
            }
        }
        else if (titleText != null && contentText != null)
        {
            // Modo simple: mostrar en textos fijos
            titleText.text = news.title;
            contentText.text = news.content;
            if (authorText != null) authorText.text = $"Por: {news.author}";
            if (dateText != null) dateText.text = news.publishedAt;
        }
    }
}