using System;
using System.Threading;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            const string AuthKey = "6b59e9e17f3641bbcbfdc0f8f5979dd2";

            // Párosított eszközök keresése
            BMananger mananger = new BMananger();
            int selected = int.MinValue;
            Console.CursorVisible = false;
            Console.WriteLine("Kérem adja meg az eszköz számát.");
            while (selected < 0)
            {
                Console.SetCursorPosition(0, 0);
                for (int i = 1; i <= mananger.Devices.Count; i++)
                {
                    Console.WriteLine(i + ": " + mananger.Devices[i - 1].Name + "ID: " + mananger.Devices[i - 1].Id);
                }
                if (Console.KeyAvailable)
                {
                    char tmp = Console.ReadKey(true).KeyChar;
                    if (Char.IsDigit(tmp))
                        selected = (int)char.GetNumericValue(tmp);
                }
            }
            Console.WriteLine("Kiválasztott eszköz: " + mananger.Devices[selected-1].Name);

            // Csatlakozás
            Device device = new Device(mananger.Devices[selected - 1], AuthKey);
            device.Connect();
            Console.WriteLine("Megjegyzés: A sikeres autentikációhoz az eszköz egyidőben csak egy eszközhöz csatlakozhat!");
            device.Authenticate();
            Thread.Sleep(5000);
            Console.WriteLine(device.Status);

            // Eszközkezelés

            // Heartrate
            /*device.StartHeartrate(true);
            while(!Console.KeyAvailable)
            {
                Console.WriteLine(device.Heartrate);
                Thread.Sleep(100);
                Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 1);
            }
            device.StopHeartrate();*/

            device.GetDataFromAccelero();
            Thread.Sleep(3000);
            Console.WriteLine(device.Heartrate);

            Console.ReadLine();
        }
    }
}
