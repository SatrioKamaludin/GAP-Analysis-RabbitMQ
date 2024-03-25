using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;
using UserService.Data;
using UserService.Entities;

namespace UserService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly UserServiceContext _context;
        private readonly IntegrationEventService _integrationEventService;

        public UserController(UserServiceContext context, IntegrationEventService integrationEventService)
        {
            _context = context;
            _integrationEventService = integrationEventService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUser()
        {
            return await _context.Users.ToListAsync();
        }

        private void PublishToMessageQueue(string integrationEvent, string eventData)
        {
            // TOOO: Reuse and close connections and channel, etc, 
            var factory = new ConnectionFactory();
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            var body = Encoding.UTF8.GetBytes(eventData);
            channel.BasicPublish(exchange: "User",
                                             routingKey: integrationEvent,
                                             basicProperties: null,
                                             body: body);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, User user)
        {
            using var transaction = _context.Database.BeginTransaction();

            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            var integrationEventData = JsonConvert.SerializeObject(new
            {
                id = user.Id,
                newname = user.Name,
            });
            _context.IntegrationEventOutbox.Add(
                new IntegrationEvent()
                {
                    Event = "user.update",
                    Data = integrationEventData
                });

            _context.SaveChanges();
            transaction.Commit();

            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            using var transaction = _context.Database.BeginTransaction();
            _context.Users.Add(user);
            _context.SaveChanges();

            var integrationEventData = JsonConvert.SerializeObject(new
            {
                id = user.Id,
                name = user.Name
            });

            _context.IntegrationEventOutbox.Add(
                new IntegrationEvent()
                {
                    Event = "user.add",
                    Data = integrationEventData
                });

            _context.SaveChanges();
            transaction.Commit();

            return CreatedAtAction("GetUser", new { id = user.Id }, user);
        }
    }
}
