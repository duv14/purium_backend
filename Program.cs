using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PuriumBackend
{
    // Record types
    public record RegisterRequest(string Username);
    public record SendRequest(string From, string To, string Subject, string Body);
    public record Email(string From, string To, string Subject, string Body, DateTime SentAt);

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Enable CORS for your Netlify frontend
            var allowedOrigins = new string[] { "https://purium.xyz" };
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            var app = builder.Build();
            app.UseCors();

            var usersFile = "users.json";
            var mailsFile = "emails.json";

            List<string> users = File.Exists(usersFile)
                ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(usersFile)) ?? []
                : [];

            List<Email> emails = File.Exists(mailsFile)
                ? JsonSerializer.Deserialize<List<Email>>(File.ReadAllText(mailsFile)) ?? []
                : [];

            // Optional homepage so / doesn’t 404
            app.MapGet("/", () => Results.Content("Purium Backend API is running.", "text/plain"));

            // Register endpoint
            app.MapPost("/api/register", async (HttpContext ctx) =>
            {
                var req = await ctx.Request.ReadFromJsonAsync<RegisterRequest>();
                if (req == null) return Results.BadRequest("Invalid request");

                var user = req.Username.Trim().ToLower();
                if (users.Contains(user)) return Results.BadRequest("User exists");

                users.Add(user);
                File.WriteAllText(usersFile, JsonSerializer.Serialize(users));
                return Results.Ok(new { address = $"{user}@purium.xyz" });
            });

            // Send email endpoint
            app.MapPost("/api/send", async (HttpContext ctx) =>
            {
                var req = await ctx.Request.ReadFromJsonAsync<SendRequest>();
                if (req == null) return Results.BadRequest("Invalid request");

                var from = req.From.ToLower();
                var to = req.To.ToLower();

                if (!users.Contains(from) || !users.Contains(to))
                    return Results.BadRequest("Invalid sender or recipient");

                var msg = new Email(from, to, req.Subject, req.Body, DateTime.UtcNow);
                emails.Add(msg);
                File.WriteAllText(mailsFile, JsonSerializer.Serialize(emails));
                return Results.Ok("sent");
            });

            // Inbox endpoint
            app.MapGet("/api/inbox/{user}", (string user) =>
            {
                user = user.ToLower();
                var inbox = emails.Where(e => e.To == user).OrderByDescending(e => e.SentAt);
                return Results.Ok(inbox);
            });

            // Sent emails endpoint
            app.MapGet("/api/sent/{user}", (string user) =>
            {
                user = user.ToLower();
                var sent = emails.Where(e => e.From == user).OrderByDescending(e => e.SentAt);
                return Results.Ok(sent);
            });

            app.Run();
        }
    }
}
