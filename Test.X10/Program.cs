using System;

using XTenLib;
using System.Threading;

namespace Test.X10
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("XTenLib test program, waiting for connection.");
            var x10 = new XTenManager();
            // Listen to XTenManager events
            x10.ModuleChanged += X10_ModuleChanged;
            // This event is only used for CM15
            x10.RfDataReceived += X10_RfDataReceived;
            // Setup X10 interface. For CM15 set PortName = "USB"; for CM11 use serial port path intead (eg. "COM7" or "/dev/ttyUSB0")
            x10.PortName = "USB";
            x10.HouseCode = "A,C";
            // Connect to the interface
            x10.Connect();
            // Enter the main loop, the interface can be "hot" plugged at any time
            while (true)
            {
                // Wait for the connection
                while (!x10.IsConnected)
                {
                    Console.Write(".");
                    Thread.Sleep(500);
                }
                Console.WriteLine("\nConnected!");
                // Send On/Off to module C7 repeatedly
                while (x10.IsConnected)
                {
                    var modC7 = x10.Modules["C7"];
                    modC7.On();
                    modC7.Off();
                }
            }
        }

        static void X10_RfDataReceived(RfDataReceivedAction obj)
        {
            Console.WriteLine("RF data received: {0}", BitConverter.ToString(obj.RawData));
        }

        static void X10_ModuleChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var module = sender as X10Module;
            Console.WriteLine("Module property changed: {0} {1}", module.Code, e.PropertyName);
        }
    }
}
