using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var usersFile = "users.json";
var mailsFile = "emails.json";

List<string> users = File.Exists(usersFile)
    ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(usersFile)) ?? []
    : [];

List<Email> emails = File.Exists(mailsFile)
    ? JsonSerializer.Deserialize<List<Email>>(File.ReadAllText(mailsFile)) ?? []
    : [];

record RegisterRequest(string Username);
record SendRequest(string From, string To, string Subject, string Body);
record Email(string From, string To, string Subject, string Body, DateTime SentAt);

app.MapPost("/api/register", ([FromBody] RegisterRequest req) =>
{
    var user = req.Username.Trim().ToLower();
    if (users.Contains(user)) return Results.BadRequest("User exists");
    users.Add(user);
    File.WriteAllText(usersFile, JsonSerializer.Serialize(users));
    return Results.Ok(new { address = $"{user}@purium.xyz" });
});

app.MapPost("/api/send", ([FromBody] SendRequest req) =>
{
    var from = req.From.ToLower();
    var to = req.To.ToLower();

    if (!users.Contains(from) || !users.Contains(to))
        return Results.BadRequest("Invalid sender or recipient");

    var msg = new Email(from, to, req.Subject, req.Body, DateTime.UtcNow);
    emails.Add(msg);
    File.WriteAllText(mailsFile, JsonSerializer.Serialize(emails));
    return Results.Ok("sent");
});

app.MapGet("/api/inbox/{user}", (string user) =>
{
    user = user.ToLower();
    var inbox = emails.Where(e => e.To == user).OrderByDescending(e => e.SentAt);
    return Results.Ok(inbox);
});

app.MapGet("/api/sent/{user}", (string user) =>
{
    user = user.ToLower();
    var sent = emails.Where(e => e.From == user).OrderByDescending(e => e.SentAt);
    return Results.Ok(sent);
});

app.Run();