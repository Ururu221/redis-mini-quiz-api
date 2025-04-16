using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

var connection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(connection));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", (IConnectionMultiplexer redis) =>
{
    IDatabase db = redis.GetDatabase();

    db.HashSet("question:1", new HashEntry[]
    {
        new HashEntry("text", "Is 10 bigger than 5?"),
        new HashEntry("answer", "yes"),
    });
    db.KeyExpire("question:1", TimeSpan.FromMinutes(1));

    db.HashSet("question:2", new HashEntry[]
    {
        new HashEntry("text", "Does Fedia bench press 100 kg at the gym?"),
        new HashEntry("answer", "yes")
    });
    db.KeyExpire("question:2", TimeSpan.FromMinutes(2));

    db.HashSet("question:3", new HashEntry[]
    {
        new HashEntry("text", "Are you smart?"),
        new HashEntry("answer", "no")
    });
    db.KeyExpire("question:3", TimeSpan.FromMinutes(3));

    return "Hello World. Pre-setup was successfully completed";
});

app.MapPost("/add-member/{name}", (IConnectionMultiplexer redis, string name) =>
{
    IDatabase db = redis.GetDatabase();
    var member = new Member { Name = name, Score = 0 };
    var json = JsonConvert.SerializeObject(member);
    db.ListRightPush("members", json);

    ISubscriber pub = redis.GetSubscriber();
    pub.Publish("quiz:updates", $"A new member whose name is {name}");

    return Results.Ok($"Member {name} added");
});

app.MapGet("/members", (IConnectionMultiplexer redis) =>
{
    IDatabase db = redis.GetDatabase();
    
    var membersJson = db.ListRange("members");
    var members = new List<Member>();

    foreach (var memberJson in membersJson)
    {
        members.Add(JsonConvert.DeserializeObject<Member>(memberJson));
    }

    return Results.Ok(members);
});

app.MapGet("/question/{id}", (IConnectionMultiplexer redis, int id) =>
{
    IDatabase db = redis.GetDatabase();
    var question = db.HashGet($"question:{id}", "text");
    return Results.Ok(question.ToString());
});

app.MapPatch("/answer/{question}/{name}/{answer}",
    (IConnectionMultiplexer redis, string question, string name, string answer) =>
    {
        IDatabase db = redis.GetDatabase();

        if (db.HashGet($"question:{question}", "answer") == answer)
        {
            var membersJson = db.ListRange("members");

            foreach (var memberJson in membersJson)
            {
                var member = JsonConvert.DeserializeObject<Member>(memberJson);
                if (member?.Name == name)
                {
                    if (db.SetContains($"answered:{question}", name))
                    {
                        return Results.BadRequest($"{name} has already answered this question.");
                    }

                    member.Score += 1;
                    var updatedJson = JsonConvert.SerializeObject(member);
                    db.ListSetByIndex("members", Array.IndexOf(membersJson, memberJson), updatedJson);

                    db.SetAdd($"answered:{question}", name);

                    ISubscriber pub = redis.GetSubscriber();
                    pub.Publish("quiz:updates", $"{name} gains 1 point for question the {question}.");

                    return Results.Ok($"Correct! {name} now has {member.Score} points.");
                }
            }

            return Results.NotFound($"Player {name} not found.");
        }

        return Results.BadRequest($"Incorrect answer for {name}.");
    });

Task.Run(static () =>
{
    ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost:6379");
    ISubscriber sub = redis.GetSubscriber();

    sub.Subscribe("quiz:updates", (channel, message) =>
    {
        Console.WriteLine($"[Pub/Sub] {message}");
    });

    Console.WriteLine("[Pub/Sub] Подписан на quiz:updates");
});

app.Run();

public class Member
{
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; } = 0;
}