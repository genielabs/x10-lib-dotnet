using System;
using System.Threading;

using XTenLib;
using NLog;

namespace Test.X10
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            // NOTE: To disable debug output uncomment the following two lines
            //LogManager.Configuration.LoggingRules.RemoveAt(0);
            //LogManager.Configuration.Reload();

            Console.WriteLine("XTenLib test program, waiting for connection.");
            var x10 = new XTenManager();
            // Listen to XTenManager events
            x10.ConnectionStatusChanged += X10_ConnectionStatusChanged;
            x10.ModuleChanged += X10_ModuleChanged;
            x10.PlcAddressReceived += X10_PlcAddressReceived;
            x10.PlcFunctionReceived += X10_PlcFunctionReceived;
            // These RF events are only used for CM15
            x10.RfDataReceived += X10_RfDataReceived;
            x10.RfCommandReceived += X10_RfCommandReceived;
            x10.RfSecurityReceived += X10_RfSecurityReceived;
            // Setup X10 interface. For CM15 set PortName = "USB"; for CM11 use serial port path intead (eg. "COM7" or "/dev/ttyUSB0")
            x10.PortName = "USB";
            x10.HouseCode = "A,C";
            // Connect to the interface
            x10.Connect();

            // Prevent the program from quitting with a noop loop
            while (true)
            {
                Thread.Sleep(1000);
            }

        }

        static void X10_ConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs args)
        {
            Console.WriteLine("Interface connection status {0}", args.Connected);
        }

        static void X10_ModuleChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var module = sender as X10Module;
            Console.WriteLine("Module property changed: {0} {1} = {2}", module.Code, e.PropertyName, module.Level);
        }

        static void X10_PlcAddressReceived (object sender, PlcAddressReceivedEventArgs args)
        {
            Console.WriteLine("PLC address received: HouseCode {0} Unit {1}", args.HouseCode, args.UnitCode);
        }

        static void X10_PlcFunctionReceived (object sender, PlcFunctionReceivedEventArgs args)
        {
            Console.WriteLine("PLC function received: Command {0} HouseCode {1}", args.Command, args.HouseCode);
        }

        static void X10_RfDataReceived(object sender, RfDataReceivedEventArgs args)
        {
            Console.WriteLine("RF data received: {0}", BitConverter.ToString(args.Data));
        }

        static void X10_RfCommandReceived(object sender, RfCommandReceivedEventArgs args)
        {
            Console.WriteLine("Received RF command {0} House Code {1} Unit {2}", args.Command, args.HouseCode, args.UnitCode);
        }

        static void X10_RfSecurityReceived(object sender, RfSecurityReceivedEventArgs args)
        {
            Console.WriteLine("Received RF Security event {0} from address {1}", args.Event, args.Address.ToString("X3"));
        }
    }
}
