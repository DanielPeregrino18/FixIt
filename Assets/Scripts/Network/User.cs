using System;

[Serializable]
public class User
{
    public string _id;
    public string username;
    public string email;
    public string createdAt;
    public UserStats stats;
}