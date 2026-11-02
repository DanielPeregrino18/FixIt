using System;

[Serializable]
public class NewsItem
{
    public string _id;
    public string title;
    public string content;
    public string author = "Equipo Fix IT!";
    public string publishedAt;
    public string createdAt;
    public string updatedAt;
}