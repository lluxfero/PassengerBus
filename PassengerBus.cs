using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PassengerBus
{
    public enum BusState 
    {
        StartPosition,


        GoingToAirportForPassengers,
        GoingToParkingForPassengers,


        GettingPassengersFromAirport,

        WaitingForGetSignalFromBoard,
        GettingPassengersFromBoard,


        GoingToParkingToDropPassengers,
        GoingToAirportToDropPassengers,


        WaitingForDropSignalFromBoard,
        DroppingPassengersToBoard,

        DroppingPassengersToAirport,


        GoingToGarageFromParking,
        GoingToGarageFromAirport,


        StopPosition
    }

    public class PassengerBus
    {
        Logger logger;

        BusState busState = BusState.StartPosition;
        public string busUid = "";
        List<string> passengersUids = [];
        public List<string> PassengersUids
        {
            get { lock (locker) { return passengersUids; } }
            set { lock (locker) { passengersUids = value; } }
        }

        public string boardUid = "";
        public string voyageUid = "";

        object locker = new();

        static int lastUid = 0;
        object lockerLastUid = new();

        int x = 20;
        int y = 7;

        readonly int[] RouteToAirportFromGarage_X = [20, 20, 20, 19, 18, 17, 16, 15, 14, 14, 14, 14, 14, 13];
        readonly int[] RouteToAirportFromGarage_Y = [6,  5,  4,  4,  4,  4,  4,  4,  4,  5,  6,  7,  8,  8];

        readonly int[] RouteToParkingFromAirport_X = [13, 13, 13, 14, 15, 16, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17];
        readonly int[] RouteToParkingFromAirport_Y = [9,  10, 11, 11, 11, 11, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36];

        readonly int[] RouteToGarageFromParking_X = [20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20];
        readonly int[] RouteToGarageFromParking_Y = [36, 35, 34, 33, 32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10];

        readonly int[] RouteToParkingFromGarage_X = [20, 20, 20, 19, 18, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17];
        readonly int[] RouteToParkingFromGarage_Y = [6,  5,  4,  4,  4,  4,  5,  6,  7,  8,  9,  10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36];

        readonly int[] RouteToAirportFromParking_X = [22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 21, 20, 19, 18, 17, 16, 15, 14, 14, 14, 14, 14, 13];
        readonly int[] RouteToAirportFromParking_Y = [36, 35, 34, 33, 32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9,  8,  7,  6,  5,  4,  4,  4,  4,  4,  4,  4,  4,  4,  5,  6,  7,  8,  8];

        readonly int[] RouteToGarageFromAirport_X = [13, 13, 13, 14, 15, 16, 17, 18, 19, 20, 20];
        readonly int[] RouteToGarageFromAirport_Y = [9,  10, 11, 11, 11, 11, 11, 11, 11, 11, 10];

        public PassengerBus(Logger _logger)
        {
            logger = _logger;

            busUid = lastUid.ToString();
            logger.Log(DateTime.Now.ToString("HH:mm:ss.fff") + " | bus #" + busUid + ": новый автобус начинает работу");

            lock(lockerLastUid) { lastUid++; }
        }

        public int X
        {
            get { lock (locker) { return x; } }
            set { lock (locker) { x = value; } }
        }
        public int Y
        {
            get { lock (locker) { return y; } }
            set { lock (locker) { y = value; } }
        }

        public int[] GetRouteX()
        {
            switch(GetBusState)
            {
                case BusState.GoingToAirportForPassengers:
                    return RouteToAirportFromGarage_X;
                case BusState.GoingToParkingForPassengers:
                    return RouteToParkingFromGarage_X;

                case BusState.GoingToParkingToDropPassengers:
                    return RouteToParkingFromAirport_X;
                case BusState.GoingToAirportToDropPassengers:
                    return RouteToAirportFromParking_X;

                case BusState.GoingToGarageFromParking:
                    return RouteToGarageFromParking_X;
                case BusState.GoingToGarageFromAirport:
                    return RouteToGarageFromAirport_X;
            }
            return [];
        }
        public int[] GetRouteY()
        {
            switch (GetBusState)
            {
                case BusState.GoingToAirportForPassengers:
                    return RouteToAirportFromGarage_Y;
                case BusState.GoingToParkingForPassengers:
                    return RouteToParkingFromGarage_Y;

                case BusState.GoingToParkingToDropPassengers:
                    return RouteToParkingFromAirport_Y;
                case BusState.GoingToAirportToDropPassengers:
                    return RouteToAirportFromParking_Y;

                case BusState.GoingToGarageFromParking:
                    return RouteToGarageFromParking_Y;
                case BusState.GoingToGarageFromAirport:
                    return RouteToGarageFromAirport_Y;
            }
            return [];
        }

        public BusState GetBusState { get { lock (locker) { return busState; } } }
        public void ChangeState(BusState newState)
        {
            lock (locker)
            {
                busState = newState;
            }
            switch (GetBusState)
            {
                case BusState.GoingToAirportForPassengers:
                    logger.Log(DateTime.Now.ToString("HH:mm:ss.fff") + " | bus #" + busUid + ": еду к аэропорту за пассажирами");
                    break;
                case BusState.GoingToParkingForPassengers:
                    logger.Log(DateTime.Now.ToString("HH:mm:ss.fff") + " | bus #" + busUid + ": еду к борту за пассажирами");
                    break;
                case BusState.GoingToParkingToDropPassengers:
                    logger.Log(DateTime.Now.ToString("HH:mm:ss.fff") + " | bus #" + busUid + ": еду к борту высадить пассажиров");
                    break;
                case BusState.GoingToAirportToDropPassengers:
                    logger.Log(DateTime.Now.ToString("HH:mm:ss.fff") + " | bus #" + busUid + ": еду к аэропорту высадить пассажиров");
                    break;
                case BusState.GoingToGarageFromParking:
                    logger.Log(DateTime.Now.ToString("HH:mm:ss.fff") + " | bus #" + busUid + ": еду в гараж от борта");
                    break;
                case BusState.GoingToGarageFromAirport:
                    logger.Log(DateTime.Now.ToString("HH:mm:ss.fff") + " | bus #" + busUid + ": еду в гараж от аэропорта");
                    break;
                case BusState.GettingPassengersFromAirport:
                    logger.Log(DateTime.Now.ToString("HH:mm:ss.fff") + " | bus #" + busUid + ": забираю пассажиров у аэропорта");
                    break;
                case BusState.GettingPassengersFromBoard:
                    logger.Log(DateTime.Now.ToString("HH:mm:ss.fff") + " | bus #" + busUid + ": забираю пассажиров у борта");
                    break;
                case BusState.WaitingForDropSignalFromBoard:
                    logger.Log(DateTime.Now.ToString("HH:mm:ss.fff") + " | bus #" + busUid + ": жду сигнал высадить пассажиров на борт");
                    break;
                case BusState.WaitingForGetSignalFromBoard:
                    logger.Log(DateTime.Now.ToString("HH:mm:ss.fff") + " | bus #" + busUid + ": жду сигнал забрать пассажиров у борта");
                    break;
                case BusState.DroppingPassengersToBoard:
                    logger.Log(DateTime.Now.ToString("HH:mm:ss.fff") + " | bus #" + busUid + ": высаживаю пассажиров на борт");
                    break;
                case BusState.DroppingPassengersToAirport:
                    logger.Log(DateTime.Now.ToString("HH:mm:ss.fff") + " | bus #" + busUid + ": высаживаю пассажиров в аэропорт");
                    break;
                case BusState.StopPosition:
                    logger.Log(DateTime.Now.ToString("HH:mm:ss.fff") + " | bus #" + busUid + ": приехал в гараж, закончил работу");
                    break;
            }
        }
    }
}
