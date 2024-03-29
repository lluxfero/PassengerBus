﻿using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Reflection;
using System.Reflection.Metadata;
using System.Diagnostics;
using System.Net.Http.Json;
using System.ComponentModel.DataAnnotations.Schema;


namespace PassengerBus
{
    public class FlightInfo
    {
        public bool success { get; set; } = true;
        public bool load { get; set; } = true;
        public List<string> passengers { get; set; } = [];
    }
    public class PassengersInfo
    {
        public bool success { get; set; } = true;
        public List<string> passengers { get; set; } = [];
    }

    public class CartItem
    {
        public string type { get; set; }
        public CartPosition position { get; set; }
    }
    public class CartPosition
    {
        public string row { get; set; }
        public string column { get; set; }
    }

    public class RestServer
    {
        // базовый адрес сервера
        //const string serverAddress = "http://localhost:9002/";
        const string serverAddress = "http://46.174.48.185:9002/";
        
        RabbitMqManager rabbitMqManager;
        Logger logger;

        Dictionary<string, PassengerBus> buses;
        object lockerDictionary;

        RestClient client;

        public RestServer(Dictionary<string, PassengerBus> _buses, Logger _logger, object _locker)
        {
            buses = _buses;
            logger = _logger;
            lockerDictionary = _locker;

            rabbitMqManager = new RabbitMqManager(_logger);
            client = new RestClient(_logger);
        }

        public async Task StartAsync()
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(serverAddress);
            try
            {
                listener.Start();
                logger.Log($"сервер запущен по адресу {serverAddress}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nERROR: " + ex.Message + "\n" + ex.ToString());
                Console.WriteLine("PRESS ENTER TO RETRY\n");
                Console.ReadLine();
            }

            while (true)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    var request = context.Request;      // объект запроса
                    var response = context.Response;    // объект ответа

                    if (request.Url == null) continue;

                    switch (request.HttpMethod)
                    {
                        case "GET":
                            RespondToGetMethod(request, response);
                            break;
                        case "POST":
                            logger.Log($"{DateTime.Now:HH:mm:ss.fff} | получен запрос: {request.HttpMethod} {request.Url}");
                            RespondToPostMethod(request, response);
                            break;
                        // если другой метод, то возвращаем код ошибки 405 (Method Not Allowed)
                        default:
                            response.StatusCode = 405;
                            response.Close(); // закрываем ответ
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\nERROR: " + ex.Message + "\n" + ex.ToString());
                    Console.WriteLine("PRESS ENTER TO RETRY\n");
                    Console.ReadLine();
                }
            }
            
        }

        public void RespondToGetMethod(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (request.Url == null) {
                response.StatusCode = 400;
                response.Close(); // закрываем ответ
                return;
            }
            string[] segments = request.Url.Segments;

            if (segments.Length >= 3 && segments[1] == "v1/") { } // [0] = "/", [1] = "v1/", ...
            else {
                response.StatusCode = 400;
                response.Close(); // закрываем ответ
                return;
            }

            switch (segments[2])
            {
                case "go_parking/": // запрос от УНО: выезжай к новому самолету
                    {
                        logger.Log($"{DateTime.Now:HH:mm:ss.fff} | получен запрос: {request.HttpMethod} {request.Url}");
                        ThreadPool.QueueUserWorkItem(async _ => {
                            await GetGoParking(response, segments);
                        });
                        break;
                    }
                case "visuals":
                    {
                        ThreadPool.QueueUserWorkItem(async _ => {
                            await GetVisuals(response);
                        });
                        break;
                    }
                default:
                    {
                        response.StatusCode = 400;
                        response.Close(); // закрываем ответ
                        break;
                    }
            }
        }

        public async Task GetVisuals(HttpListenerResponse response)
        {
            var items = new List<CartItem>();

            lock (lockerDictionary)
            {
                foreach (var b in buses)
                {
                    var position = new CartPosition() { row = b.Value.X.ToString(), column = b.Value.Y.ToString() };
                    var item = new CartItem() { type = "cart", position = position };
                    items.Add(item);
                }
            }
            string json = JsonSerializer.Serialize(items);

            response.StatusCode = 200; // статус код
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(json); // контент ответа
            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = System.Text.Encoding.UTF8;
            System.IO.Stream output = response.OutputStream; // запишем контент ответа
            await output.WriteAsync(buffer);
            await output.FlushAsync();
            output.Close();
            response.Close(); // закрываем ответ
        }

        public async Task GetGoParking(HttpListenerResponse response, string[] segments)
        {
            if (segments.Length >= 4 && segments[3] != null) { } // uid борта
            else {
                response.StatusCode = 400;
                response.Close(); // закрываем ответ
                return;
            }

            // выделяем новый автобус
            PassengerBus newBus = new(logger);
            string busUid = newBus.busUid;
            lock (lockerDictionary)
            {
                bool busesContainsVoyage = false;
                foreach (var b in buses)
                    if (b.Value.boardUid == segments[3]) busesContainsVoyage = true;

                if (buses.ContainsKey(busUid) || busesContainsVoyage) {
                    response.StatusCode = 400;
                    response.Close(); // закрываем ответ
                    return;
                }
                newBus.boardUid = segments[3];
                buses.Add(busUid, newBus);
            }
            // ответ на запрос go_parking
            response.StatusCode = 200; // статус код
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(busUid); // контент ответа (просто строка)
            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = System.Text.Encoding.UTF8;
            System.IO.Stream output = response.OutputStream; // запишем контент ответа
            await output.WriteAsync(buffer);
            await output.FlushAsync();
            output.Close();
            response.Close(); // закрываем ответ

            // запрос к самолету по uid: есть ли пассажиры?
            HttpResponseMessage boardResponse = await client.GetIfTherePassengersAsync(newBus);
            int statusCode = (int)boardResponse.StatusCode;
            while (statusCode != 200)
            {
                Thread.Sleep(1000);
                boardResponse = await client.GetIfTherePassengersAsync(newBus);
                statusCode = (int)boardResponse.StatusCode;
            }

            // получаем список пассажиров: пустой - едем к аэропорту, НЕ пустой - едем к самолету
            PassengerBus bus;
            FlightInfo flightInfo = new()
            {
                success = true,
                load = true, // true - принимаю пассажиров, false - отдаю пассажиров
                passengers = []
            };
            string content = await boardResponse.Content.ReadAsStringAsync();
            if (content != null && content != "")
                if (JsonSerializer.Deserialize<FlightInfo>(content) != null)
                    flightInfo = JsonSerializer.Deserialize<FlightInfo>(content);
            lock (lockerDictionary)
            {
                if (!buses.TryGetValue(busUid, out PassengerBus? value)) {
                    response.StatusCode = 400;
                    response.Close(); // закрываем ответ
                    return;
                }
                bus = value;
            }
            if (flightInfo.load == false) bus.ChangeState(BusState.GoingToAirportForPassengers);
            else if (flightInfo.load == true && flightInfo.passengers.Count == 0)
            {
                bus.ChangeState(BusState.StopPosition);
                lock (lockerDictionary)
                {
                    if (!buses.ContainsKey(bus.busUid))
                    {
                        response.StatusCode = 400;
                        response.Close(); // закрываем ответ
                        return;
                    }
                    buses.Remove(bus.busUid);
                }
                response.StatusCode = 200;
                response.Close(); // закрываем ответ
                return;
            }
            else bus.ChangeState(BusState.GoingToParkingForPassengers);

            // запрос к диспетчеру: заспавнить машинку
            HttpResponseMessage controlResponse = await client.PostMapAtRowColumnStatus(bus, bus.X, bus.Y, "1");
            statusCode = (int)controlResponse.StatusCode;
            while (statusCode != 200)
            {
                Thread.Sleep(1000);
                controlResponse = await client.PostMapAtRowColumnStatus(bus, bus.X, bus.Y, "1");
                statusCode = (int)controlResponse.StatusCode;
            }

            // едем по маршруту за пассажирами: либо до аэропорта, либо до самолета 
            await GoRoute(bus, bus.GetRouteX(), bus.GetRouteY());

            // если ехали до аэропорта, то нужно забрать пассажиров
            if (bus.GetBusState == BusState.GoingToAirportForPassengers)
            {
                bus.ChangeState(BusState.GettingPassengersFromAirport);

                // запрос к самолету по uid: какой номер рейса?
                HttpResponseMessage voyageResponse = await client.GetVoyageUidAsync(bus);
                statusCode = (int)voyageResponse.StatusCode;
                while (statusCode != 200)
                {
                    Thread.Sleep(1000);
                    voyageResponse = await client.GetVoyageUidAsync(bus);
                    statusCode = (int)voyageResponse.StatusCode;
                }
                content = await voyageResponse.Content.ReadAsStringAsync();
                bus.voyageUid = JsonDocument.Parse(content).RootElement.GetProperty("uid").ToString();

                bus.PassengersUids = rabbitMqManager.GetPassengersList(bus.voyageUid);

                bus.ChangeState(BusState.GoingToParkingToDropPassengers);
                // едем по маршруту к самолету посадить пассажиров
                await GoRoute(bus, bus.GetRouteX(), bus.GetRouteY());

                bus.ChangeState(BusState.WaitingForDropSignalFromBoard);
            }
            else bus.ChangeState(BusState.WaitingForGetSignalFromBoard);

            // говорим УНО, что машинка приехала к самолету
            HttpResponseMessage unoResponse = await client.PostCarHereAsync(bus); ;
            statusCode = (int)unoResponse.StatusCode;
            while (statusCode != 200)
            {
                Thread.Sleep(1000);
                unoResponse = await client.PostCarHereAsync(bus); ;
                statusCode = (int)unoResponse.StatusCode;
            }
        }

        public void RespondToPostMethod(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (request.Url == null) {
                response.StatusCode = 400;
                response.Close(); // закрываем ответ
                return;
            }
            string[] segments = request.Url.Segments;

            if (segments.Length >= 3 && segments[1] == "v1/") { }
            else {
                response.StatusCode = 400;
                response.Close(); // закрываем ответ
                return;
            }

            switch (segments[2])
            {
                case "do_action/":
                    {
                        ThreadPool.QueueUserWorkItem(async _ => {
                            await DoAction(response, segments);
                        });
                        break;
                    }
                default:
                    {
                        response.StatusCode = 400;
                        response.Close(); // закрываем ответ
                        break;
                    }
            }
        }
        public async Task DoAction(HttpListenerResponse response, string[] segments)
        {
            if (segments.Length >= 4 && segments[3] != null) { } // uid машинки
            else {
                response.StatusCode = 400;
                response.Close(); // закрываем ответ
                return;
            }

            // проверяем, есть ли машинка с таким uid
            PassengerBus bus;
            lock (lockerDictionary)
            {
                if (!buses.TryGetValue(segments[3], out PassengerBus? value)) return;
                bus = value;
            }

            // делаем действие: получаем пассажиров или сажаем их
            bool ifGettingPassengers;
            if (bus.GetBusState == BusState.WaitingForGetSignalFromBoard) ifGettingPassengers = true;
            else if (bus.GetBusState == BusState.WaitingForDropSignalFromBoard) ifGettingPassengers = false;
            else return;
            HttpResponseMessage boardResponse = await client.PostPassengersAsync(ifGettingPassengers, bus);
            int statusCode = (int)boardResponse.StatusCode;
            while (statusCode != 200)
            {
                Thread.Sleep(1000);
                boardResponse = await client.PostPassengersAsync(ifGettingPassengers, bus);
                statusCode = (int)boardResponse.StatusCode;
            }

            // контент ответа борта: с пассажирами или пустой
            PassengersInfo passengersInfo = new()
            {
                success = true,
                passengers = []
            };
            string jsonContent = await boardResponse.Content.ReadAsStringAsync();
            if (!ifGettingPassengers) bus.PassengersUids?.Clear();
            else if (jsonContent != null && jsonContent != "")
            {
                if (JsonSerializer.Deserialize<PassengersInfo>(jsonContent) != null)
                {
                    passengersInfo = JsonSerializer.Deserialize<PassengersInfo>(jsonContent);
                    bus.PassengersUids = passengersInfo.passengers;
                }
            }
            else bus.PassengersUids = [];

            // ответ на запрос do_action
            response.StatusCode = 200;
            response.Close();

            // говорим УНО: сделали работу (даже если вернулось 400 - ок)
            HttpResponseMessage unoResponse = await client.PostCarDoneAsync(bus);

            // едем дальше: в аэропорт высадить пассажиров или в гараж
            if (ifGettingPassengers) bus.ChangeState(BusState.GoingToAirportToDropPassengers);
            else bus.ChangeState(BusState.GoingToGarageFromParking);
            await GoRoute(bus, bus.GetRouteX(), bus.GetRouteY());

            if (ifGettingPassengers)
            {
                bus.ChangeState(BusState.DroppingPassengersToAirport);
                // запрос к самолету по uid: какой номер рейса?
                //HttpResponseMessage voyageResponse = await client.GetVoyageUidAsync(bus);
                //statusCode = (int)voyageResponse.StatusCode;
                //while (statusCode != 200)
                //{
                //    voyageResponse = await client.GetVoyageUidAsync(bus);
                //    statusCode = (int)voyageResponse.StatusCode;
                //}
                //jsonContent = await voyageResponse.Content.ReadAsStringAsync();
                //bus.voyageUid = JsonDocument.Parse(jsonContent).RootElement.GetProperty("uid").ToString();

                if (bus.PassengersUids != null) rabbitMqManager.PutPassengersToAirport("прилет", bus.PassengersUids);
                bus.PassengersUids?.Clear();
                bus.ChangeState(BusState.GoingToGarageFromAirport);

                await GoRoute(bus, bus.GetRouteX(), bus.GetRouteY());
            }

            // запрос к диспетчеру: убрать машинку
            HttpResponseMessage controlResponse = await client.PostMapAtRowColumnStatus(bus, 20, 10, "0");
            statusCode = (int)controlResponse.StatusCode;
            while (statusCode != 200)
            {
                Thread.Sleep(1000);
                controlResponse = await client.PostMapAtRowColumnStatus(bus, 20, 10, "0");
                statusCode = (int)controlResponse.StatusCode;
            }

            // машинка закончила свою работу
            bus.ChangeState(BusState.StopPosition);
            lock (lockerDictionary)
            {
                if (!buses.ContainsKey(bus.busUid)) {
                    response.StatusCode = 400;
                    response.Close(); // закрываем ответ
                    return;
                }
                buses.Remove(bus.busUid);
            }
        }

        public async Task GoRoute(PassengerBus bus, int[] X, int[] Y)
        {
            for (int i = 0; i < X.Length; i++)
            {
                HttpResponseMessage controlResponse = await client.PostMapMoveAsync(bus, X[i], Y[i]);
                int statusCode = (int)controlResponse.StatusCode;
                string content = await controlResponse.Content.ReadAsStringAsync();
                bool success = JsonDocument.Parse(content).RootElement.GetProperty("success").GetBoolean();
                while (statusCode != 200 && success != true)
                {
                    Thread.Sleep(1000);
                    controlResponse = await client.PostMapMoveAsync(bus, X[i], Y[i]);
                    statusCode = (int)controlResponse.StatusCode;
                    content = await controlResponse.Content.ReadAsStringAsync();
                    success = JsonDocument.Parse(content).RootElement.GetProperty("success").GetBoolean();
                }

                bus.X = X[i];
                bus.Y = Y[i];

                Thread.Sleep(300);
            }
        }
    }
}
