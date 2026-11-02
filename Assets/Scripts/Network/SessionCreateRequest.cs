using System;
using System.Collections.Generic;

/// <summary>
/// Payload limpio para crear una sesión, sin el campo _id para que MongoDB lo genere.
/// </summary>
[Serializable]
public class SessionCreateRequest
{
    public string levelId;
    public string worldId;
    public List<string> players;
    public int score;
    public int starsEarned;
    public int ticketsCompleted;
    public int ticketsFailed;
    public int durationSeconds;
    public string equipment;
    public string repairType;
    public bool completed;
}