using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;
using UserService.Data;
using UserService.Models;

namespace UserService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly UserServiceContext _context;

        public UserController(UserServiceContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUser()
        {
            return await _context.Users.ToListAsync();
        }

        private void PublishToMessageQueue(string integrationEvent, string eventData)
        {
            // Todo: Reuse and close connections and channel, etc,
            var factory = new ConnectionFactory()
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest",
                VirtualHost = "/"
            };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            channel.QueueDeclare(queue: "user.postservice", durable: true, exclusive: false, autoDelete: false, arguments: null);
            var body = Encoding.UTF8.GetBytes(eventData);
            channel.ExchangeDeclare(exchange: "User", type: "direct", durable: true, autoDelete: false);
            channel.QueueBind(queue: "user.postservice", exchange: "User", routingKey: integrationEvent);
            channel.BasicPublish(exchange: "User", routingKey: integrationEvent, basicProperties: null, body: body);
            Console.WriteLine(" [X] Sent {0}", body);
        }

        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var integrationEventData = JsonConvert.SerializeObject(new
            {
                id = user.ID,
                name = user.Name
            });
            PublishToMessageQueue("user.add", integrationEventData);

            return CreatedAtAction("GetUser", new { id = user.ID }, user);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, User user)
        {
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            var integrationEventData = JsonConvert.SerializeObject(new
            {
                id = user.ID,
                name = user.Name
            });
            PublishToMessageQueue("user.update", integrationEventData);

            return NoContent();
        }
    }
}
