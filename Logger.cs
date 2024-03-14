using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassengerBus
{
    public class Logger
    {
        const string LogFileName = "log.txt";
        object locker = new();

        public Logger()
        {
            File.Delete(LogFileName);
        }

        public void Log(string str)
        {
            lock (locker) {
                using var writer = File.AppendText(LogFileName);
                writer.WriteLine(str);
                Console.WriteLine(str);
            }
        }
    }
}
