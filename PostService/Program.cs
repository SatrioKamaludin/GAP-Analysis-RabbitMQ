using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using PostService.Data;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using Newtonsoft.Json.Linq;
using PostService.Entities;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddDbContext<PostServiceContext>(o =>
    o.UseSqlite(@"Data Source=user.db"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

ListenForIntegrationEvents(app);

app.Run();

static void ListenForIntegrationEvents(IHost host)
{
    var factory = new ConnectionFactory();
    var connection = factory.CreateConnection();
    var channel = connection.CreateModel();
    var consumer = new EventingBasicConsumer(channel);

    consumer.Received += (model, ea) =>
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine(" [x] Received {0}", message);
        var data = JObject.Parse(message);
        var type = ea.RoutingKey;

        using var localScope = host.Services.CreateScope();
        var dbContext = localScope.ServiceProvider.GetRequiredService<PostServiceContext>();

        if (type == "user.add")
        {
            dbContext.Users.Add(new User()
            {
                Id = data["id"].Value<int>(),
                Name = data["name"].Value<string>()
            });
            dbContext.SaveChanges();
        }
        else if (type == "user.update")
        {
            var user = dbContext.Users.First(a => a.Id == data["id"].Value<int>());
            user.Name = data["newname"].Value<string>();
            dbContext.SaveChanges();
        }
    };

    channel.BasicConsume(queue: "user.postservice",
                         autoAck: true,
                         consumer: consumer);
}
