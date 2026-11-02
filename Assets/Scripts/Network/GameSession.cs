using System;
using System.Collections.Generic;

[Serializable]
public class GameSession
{
    public string _id;
    public string levelId;
    public string worldId;
    public List<string> players = new List<string>();
    public int score;
    public int starsEarned;
    public int ticketsCompleted;
    public int ticketsFailed;
    public int durationSeconds;
    public string equipment = "Laptop";
    public string repairType = "Otros";
    public bool completed = true;
    public string playedAt;
}