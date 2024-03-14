namespace PassengerBus
{
    class Program
    {
        // Метод, который запускает программу
        static async Task Main(string[] args)
        {
            // uid автобуса, автобус
            Dictionary<string, PassengerBus> buses = new();
            Logger logger = new();
            object lockerDictionary = new object();

            var server = new RestServer(buses, logger, lockerDictionary);
            await server.StartAsync();
        }
    }
}
