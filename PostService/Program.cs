using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using PostService.Data;
using PostService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<PostServiceContext>(o =>
    o.UseSqlite(@"Data Source=user.db"));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<PostServiceContext>();
        dbContext.Database.EnsureCreated();
    }
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

ListenForIntegrationEvent(app);

app.Run();

static void ListenForIntegrationEvent(IHost host)
{
    var factory = new ConnectionFactory()
    {
        HostName = "localhost",
        UserName = "guest",
        Password = "guest",
        VirtualHost = "/"
    };
    using var connection = factory.CreateConnection();
    using var channel = connection.CreateModel();
    channel.ExchangeDeclare(exchange: "User", type: "direct", durable: true, autoDelete: false);
    channel.QueueDeclare(queue: "user.postservice", durable: true, exclusive: false, autoDelete: false, arguments: null);
    channel.QueueBind(queue: "user.postservice", exchange: "User", routingKey: "user.add");
    channel.QueueBind(queue: "user.postservice", exchange: "User", routingKey: "user.update");
    var consumer = new EventingBasicConsumer(channel);

    consumer.Received += (model, ea) =>
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine(" [X] Received {0}", message);
        try
        {
            var data = JObject.Parse(message);
            var type = ea.RoutingKey;

            Console.WriteLine($" [X] RoutingKey: {type}");

            using var localScope = host.Services.CreateScope();
            var localDbContext = localScope.ServiceProvider.GetRequiredService<PostServiceContext>();

            if (type == "user.add")
            {
                if (data["id"] != null && data["name"] != null)
                {
                    localDbContext.Users.Add(new User()
                    {
                        ID = data["id"].Value<int>(),
                        Name = data["name"].Value<string>()
                    });
                    localDbContext.SaveChanges();
                    Console.WriteLine(" [X] User added to database");
                } else
                {
                    Console.WriteLine(" [X] User data not found in the message");
                }
            }
            else if (type == "user.update")
            {
                if (data["id"] != null && data["name"] != null)
                {
                    var user = localDbContext.Users.FirstOrDefault(a => a.ID == data["id"].Value<int>());
                    if (user != null)
                    {
                        user.Name = data["name"].Value<string>();
                        localDbContext.SaveChanges();
                        Console.WriteLine(" [X] User updated in database");
                    } else
                    {
                        Console.WriteLine(" [X] User not found for update");
                    }
                } else
                {
                    Console.WriteLine(" [X] User data not found in the message");
                }
            } else
            {
                Console.WriteLine($" [X] Unknown routing key: {type}");
            }
        } catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex.Message}");
        }
    };

    channel.BasicConsume(queue: "user.postservice", autoAck: true, consumer: consumer);
}
