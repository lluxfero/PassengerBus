using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassengerBus
{
    public class Logger
    {
        const string LogCommonFileName = "common.log";
        object lockerCommon = new();
        const string LogPassangersFileName = "passengers.log";
        object lockerPassangers = new();


        public Logger()
        {
            File.Delete(LogCommonFileName);
            File.Delete(LogPassangersFileName);
        }

        public void Log(string str)
        {
            lock (lockerCommon) {
                using var writer = File.AppendText(LogCommonFileName);
                writer.WriteLine(str);
                Console.WriteLine(str);
            }
        }

        public void LogPassengers(bool ifGet, string time, string voyage, List<string> list)
        {
            string action = ifGet ? "GET" : "PUT";
            string str = $" [x] {time} | {action} voyage uid - {voyage}:\n";
            for (int i = 0; i < list.Count; i++)
                str += $"{action} | passenger #{list[i]}\n";

            lock (lockerPassangers)
            {
                using var writer = File.AppendText(LogCommonFileName);
                writer.WriteLine(str);
                Console.WriteLine(str);
            }
        }
    }
}
