using JwtBaseApiNetCore.Controllers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;


namespace JwtBaseApiNetCore.Models;

public class User
{

    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Username { get; set; } 

    [BsonElement("email")]
    public string Email { get; set; } 

    [BsonElement("contact_phone")]
    public string Phone { get; set; } 

    [BsonElement("password")]
    public string Password { get; set; }

    [BsonElement("salt")]
    public string KeyWordHash { get; set; }

}