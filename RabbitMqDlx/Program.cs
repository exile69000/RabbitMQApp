using RabbitMQ.Client;
using RabbitMQ.Client.Events;

IModel? channel = null;

TestRecovery();

Console.ReadLine();

static string GetConnectionString()
{
    var endpoint = new UriBuilder("amqp://", "localhost", 5672);
    endpoint.UserName = Uri.EscapeDataString("guest");
    endpoint.Password = Uri.EscapeDataString("guest");
    return endpoint.ToString();
}

void TestRecovery()
{
    IConnectionFactory connectionFactory = new ConnectionFactory
    {
        Uri = new Uri(GetConnectionString()),
        AutomaticRecoveryEnabled = true,
        RequestedHeartbeat = TimeSpan.FromSeconds(10),
        NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
        ContinuationTimeout = TimeSpan.FromSeconds(5),
        RequestedConnectionTimeout = TimeSpan.FromSeconds(10),
        SocketReadTimeout = TimeSpan.FromSeconds(10),
        SocketWriteTimeout = TimeSpan.FromSeconds(10),
        DispatchConsumersAsync = true
    };
    var _connection = connectionFactory.CreateConnection();
    channel = _connection.CreateModel();

    channel.ExchangeDeclare(
                    exchange: "tdi.wait.exchange",
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    arguments: null);

    channel.ExchangeDeclare(
                   exchange: "tdi.retry.exchange",
                   type: ExchangeType.Topic,
                   durable: true,
                   autoDelete: false,
                   arguments: null);

    Dictionary<string, object> arguments = new Dictionary<string, object>
                    {
                        { "x-queue-type", "quorum" },
                        { "x-max-in-memory-length", 50 },
                        { "x-dead-letter-exchange", "tdi.retry.exchange" },
                        { "x-dead-letter-routing-key", "QueueTest" }
                    };

    channel.QueueDeclare("QueueTestRetry", durable: true, exclusive: false, autoDelete: false, arguments);

    arguments["x-dead-letter-exchange"] = "tdi.wait.exchange";
    arguments["x-dead-letter-routing-key"] = "QueueTest";

    channel.QueueDeclare("QueueTest", durable: true, exclusive: false, autoDelete: false, arguments);

    arguments.Remove("x-dead-letter-exchange");
    arguments.Remove("x-dead-letter-routing-key");

    //Nous sommes sur un échangeur direct, le nom de la routing key est le nom de la file d'attente
    channel.QueueBind("QueueTestRetry", "tdi.wait.exchange", "QueueTest");

    //Bind de a file d'attente de base avec l'échangeur retry
    channel.QueueBind("QueueTest", "tdi.retry.exchange", "QueueTest");

    var consumerAsync = new AsyncEventingBasicConsumer(channel);

    channel.BasicConsume(
        queue: "QueueTest",
        autoAck: false,
        consumer: consumerAsync
    );
}

channel.ModelShutdown += (sender, args) =>
{
    
};