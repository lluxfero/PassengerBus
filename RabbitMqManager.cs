using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices.JavaScript;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading.Channels;
using System.Data.Common;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Collections;

namespace PassengerBus
{
    public class RabbitMqManager
    {
        Dictionary<string, List<string>> voyageListDictionary = []; // voyage uid - список uid пассажиров
        object lockerDictionary = new();

        Logger logger;

        ConnectionFactory factory = new ConnectionFactory
        {
            VirtualHost = "itojxdln",
            HostName = "hawk-01.rmq.cloudamqp.com",
            Password = "DEL8js4Cg76jY_2lAt19CjfY2saZT0yW",
            UserName = "itojxdln",
            ClientProvidedName = "Passenger Generator"
        };
        private IConnection connection;
        private IModel channel;
        const string queueName = "testQueue";

        bool isConnected;
        object lockerIsConnected = new();

        public RabbitMqManager(Logger _logger)
        {
            logger = _logger;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                Connect();
            });
        }

        void Connect()
        {
            while (true)
            {
                try
                {
                    lock (lockerIsConnected)
                    {
                        if (isConnected == true) continue;
                    }

                    connection = factory.CreateConnection();
                    logger.Log("\n [x] connected to RabbitMQ host");

                    channel = connection.CreateModel();
                    channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

                    logger.Log(" [x] waiting for messages\n");

                    var consumer = new EventingBasicConsumer(channel);
                    consumer.Received += (model, ea) =>
                    {
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            var body = ea.Body.ToArray();
                            string message = Encoding.UTF8.GetString(body);

                            string passengerUid = JsonDocument.Parse(message).RootElement.GetProperty("Passenger").ToString();
                            string voyageUid = JsonDocument.Parse(message).RootElement.GetProperty("Voyage").ToString();
                            AddUidToDictionary(voyageUid, passengerUid);

                            logger.Log($" [x] received from queue: voyage uid - {voyageUid}, passenger uid - {passengerUid}");
                            channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                        });
                    };

                    channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
                    lock (lockerIsConnected) { isConnected = true; }
                }
                catch (Exception ex)
                {
                    int retrySeconds = 3;
                    logger.Log($" [x] ERROR: {ex.Message}\n{ex.ToString}");
                    logger.Log($" [x] trying to connect to RabbitMQ host again in {retrySeconds} sec");

                    lock (lockerIsConnected) { isConnected = false; }
                    Thread.Sleep(retrySeconds * 1000);
                }
            }
        }

        void AddUidToDictionary(string key, string value)
        {
            lock (lockerDictionary)
            {
                if (!voyageListDictionary.ContainsKey(key))
                    voyageListDictionary[key] = new List<string>();

                voyageListDictionary[key].Add(value);
            }
        }
        
        public List<string> GetPassengersList(string Voyage)
        {
            List<string> list = [];
            lock (lockerDictionary)
            {
                if (voyageListDictionary.ContainsKey(Voyage))
                {
                    list = voyageListDictionary[Voyage].ToList();
                    voyageListDictionary.Remove(Voyage);
                    logger.Log($" [x] get passengers from airport: voyage uid - {Voyage}");
                }
            }
            return list;
        }

        public void PutPassengersToAirport(string Voyage, List<string> passengersUids)
        {
            logger.Log($" [x] sent passenger to airport: voyage uid - {Voyage}");
        }

        public void PutUidToQueue(string Voyage, string Passenger)
        {
            try
            {
                string json = JsonSerializer.Serialize(new { Voyage, Passenger });
                var body = Encoding.UTF8.GetBytes(json);
                channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);
                logger.Log($" [x] sent to queue: voyage uid - {Voyage}, passenger uid - {Passenger}");
            }
            catch (Exception ex)
            {
                int retrySeconds = 3;
                logger.Log($" [x] ERROR: {ex.Message}\n{ex.ToString}");
                logger.Log($" [x] trying to connect to RabbitMQ host again in {retrySeconds} sec");

                lock (lockerIsConnected) { isConnected = false; }
                Thread.Sleep(retrySeconds * 1000);
                Connect();
            }
        }
    }
}
