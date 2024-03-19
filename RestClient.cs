using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Text.Json;

namespace PassengerBus
{
    // Класс, который реализует клиентскую часть REST API
    public class RestClient
    {
        readonly string UNOAddress = "http://46.174.48.185:9004/";      // УНО
        readonly string ControlAddress = "http://46.174.48.185:9007/";  // диспетчер
        readonly string BoardAddress = "http://46.174.48.185:9008/";    // борт

        Logger logger;

        public RestClient(Logger _logger)
        {
            logger = _logger;
        }

        // запрос к самолету по uid: есть ли пассажиры?
        public async Task<HttpResponseMessage> GetIfTherePassengersAsync(PassengerBus bus)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(BoardAddress);

            HttpResponseMessage response = new(System.Net.HttpStatusCode.BadRequest);
            try { response = await client.GetAsync($"/v1/airplanes/{bus.boardUid}/passengers"); }
            catch (Exception) { }

            logger.Log($"{DateTime.Now:HH:mm:ss.fff} | bus #{bus.busUid} | to board | GET /v1/airplanes/{bus.boardUid}/passengers | {response.StatusCode}");
            return response;
        }

        // запрос к самолету по uid: какой номер рейса?
        public async Task<HttpResponseMessage> GetVoyageUidAsync(PassengerBus bus)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(BoardAddress);

            HttpResponseMessage response = new(System.Net.HttpStatusCode.BadRequest);
            try { response = await client.GetAsync($"/v1/{bus.boardUid}/voyage/uid"); }
            catch (Exception) { }

            logger.Log($"{DateTime.Now:HH:mm:ss.fff} | bus #{bus.busUid} | to board | GET /v1/{bus.boardUid}/voyage/uid | {response.StatusCode}");
            return response;
        }

        // запрос к самолету по uid: принять или посадить пассажиров
        public async Task<HttpResponseMessage> PostPassengersAsync(bool ifGettingPassengers, PassengerBus bus)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(BoardAddress);

            if (ifGettingPassengers)
            {
                StringContent content = new StringContent("");

                HttpResponseMessage response = new(System.Net.HttpStatusCode.BadRequest);
                try { response = await client.PostAsync($"/v1/{bus.boardUid}/passengers/unload", content); }
                catch (Exception) { }

                logger.Log($"{DateTime.Now:HH:mm:ss.fff} | bus #{bus.busUid} | to board | POST /v1/{bus.boardUid}/passengers/unload | {response.StatusCode}");
                return response;
            }
            else
            {
                string json = JsonSerializer.Serialize(bus.PassengersUids);
                StringContent jsonContent = new StringContent(json, System.Text.Encoding.UTF8);

                HttpResponseMessage response = new(System.Net.HttpStatusCode.BadRequest);
                try { response = await client.PostAsync($"/v1/{bus.boardUid}/passengers", jsonContent); }
                catch (Exception) { }

                string time = DateTime.Now.ToString("HH:mm:ss.fff");
                logger.Log($"{time} | bus #{bus.busUid} | to board | POST /v1/{bus.boardUid}/passengers | {response.StatusCode}\n{json}");
                return response;
            }
        }

        // запрос к УНО с uid машинки: машинка приехала
        public async Task<HttpResponseMessage> PostCarHereAsync(PassengerBus bus)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(UNOAddress);

            StringContent content = new StringContent("");

            HttpResponseMessage response = new(System.Net.HttpStatusCode.BadRequest);
            try { response = await client.PostAsync($"/v1/car/here/{bus.busUid}", content); }
            catch (Exception) { }

            logger.Log($"{DateTime.Now:HH:mm:ss.fff} | bus #{bus.busUid} | to UNO | POST /v1/car/here/{bus.busUid} | {response.StatusCode}");
            return response;
        }

        // запрос к УНО с uid машинки: работа сделана
        public async Task<HttpResponseMessage> PostCarDoneAsync(PassengerBus bus)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(UNOAddress);

            StringContent content = new StringContent("");

            HttpResponseMessage response = new(System.Net.HttpStatusCode.BadRequest);
            try { response = await client.PostAsync($"/v1/car/done/{bus.busUid}", content); }
            catch (Exception) { }

            logger.Log($"{DateTime.Now:HH:mm:ss.fff} | bus #{bus.busUid} | to UNO | POST /v1/car/done/{bus.busUid} | {response.StatusCode}");
            return response;
        }

        // запрос к Диспетчеру: заспавнить машинку на карте
        public async Task<HttpResponseMessage> PostMapAtRowColumnStatus(PassengerBus bus, int X, int Y, string action)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(ControlAddress);

            StringContent content = new StringContent("");

            HttpResponseMessage response = new(System.Net.HttpStatusCode.BadRequest);
            try { response = await client.PostAsync($"/v1/map/at/{Y}/{X}/{action}", content); }
            catch (Exception) { }

            logger.Log($"{DateTime.Now:HH:mm:ss.fff} | bus #{bus.busUid} | to Control | POST /v1/map/at/{Y}/{X}/{action} | {response.StatusCode}");

            return response;
        }

        // запрос к Диспетчеру: переместить машинку на карте
        public async Task<HttpResponseMessage> PostMapMoveAsync(PassengerBus bus, int newX, int newY)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(ControlAddress);

            StringContent content = new StringContent("");
            int oldX = bus.X;
            int oldY = bus.Y;

            HttpResponseMessage response = new(System.Net.HttpStatusCode.BadRequest);
            try { response = await client.PostAsync($"/v1/map/move/{oldY}/{oldX}/{newY}/{newX}", content); }
            catch (Exception) { }

            logger.Log($"{DateTime.Now:HH:mm:ss.fff} | bus #{bus.busUid} | to Control | POST /v1/map/move/{oldY}/{oldX}/{newY}/{newX} | {response.StatusCode}");

            return response;
        }
    }
}
