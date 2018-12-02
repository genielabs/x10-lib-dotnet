/*
  This file is part of XTenLib (https://github.com/genielabs/x10-lib-dotnet)
 
  Copyright (2012-2018) G-Labs (https://github.com/genielabs)

  Licensed under the Apache License, Version 2.0 (the "License");
  you may not use this file except in compliance with the License.
  You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

  Unless required by applicable law or agreed to in writing, software
  distributed under the License is distributed on an "AS IS" BASIS,
  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
  See the License for the specific language governing permissions and
  limitations under the License.
*/

/*
 *     Author: Generoso Martello <gene@homegenie.it>
 *     Project Homepage: https://github.com/genielabs/x10-lib-dotnet
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NLog;
using XTenLib.Drivers;

namespace XTenLib
{

    /// <summary>
    /// X10 Home Automation library for .NET / Mono. It supports CM11 (serial) and CM15 (USB) hardware.
    /// </summary>
    public class XTenManager
    {

        #region Private Fields

        internal static Logger logger = LogManager.GetCurrentClassLogger();

        // X10 objects and configuration
        private XTenInterface x10Interface;
        private string portName = "USB";
        private string monitoredHouseCode = "A";
        private readonly Dictionary<string, X10Module> modules = new Dictionary<string, X10Module>();
        // Variables for storing last addressed house codes and optimizing/speeding up X10 communication
        private readonly List<X10Module> addressedModules = new List<X10Module>();
        private bool newAddressData = true;

        // State variables
        private bool isInterfaceReady = false;
        private X10CommState communicationState = X10CommState.Ready;
        private byte expectedChecksum = 0x00;

        // Max resend attempts when a X10 command failed
        private const int CommandResendMax = 1;
        // Max wait for command acknowledge
        private const double CommandTimeoutSeconds = 5.0;
        
        // Store last X10 message (used to resend on error)
        private byte[] commandLastMessage = new byte[0];
        private int commandResendAttempts = 0;
        // I/O operation lock / monitor
        private readonly object waitAckMonitor = new object();
        private readonly object commandLock = new object();

        // Timestamps used for detecting communication timeouts
        private DateTime waitAckTimestamp = DateTime.Now;
        private DateTime lastReceivedTs = DateTime.Now;
        // Variables used for preventing duplicated messages coming from RF
        private const uint MinRfRepeatDelayMs = 500;
        private DateTime lastRfReceivedTs = DateTime.Now;
        private string lastRfMessage = "";

        // Read/Write error state variable
        private bool gotReadWriteError = true;

        // X10 interface reader Task
        private Thread reader;

        // X10 interface connection watcher
        private Thread connectionWatcher;

        private readonly object accessLock = new object();
        private bool disconnectRequested = false;

        // This is used on Linux/Mono for detecting when the link gets disconnected
        private int zeroChecksumCount = 0;

        #endregion

        #region Public Events

        /// <summary>
        /// Connected state changed event.
        /// </summary>
        public delegate void ConnectionStatusChangedEventHandler(object sender, ConnectionStatusChangedEventArgs args);

        /// <summary>
        /// Occurs when connected state changed.
        /// </summary>
        public event ConnectionStatusChangedEventHandler ConnectionStatusChanged;

        /// <summary>
        /// Occurs when an X10 module changed.
        /// </summary>
        public event PropertyChangedEventHandler ModuleChanged;

        /// <summary>
        /// Plc address received event.
        /// </summary>
        public delegate void PlcAddressReceivedEventHandler(object sender, PlcAddressReceivedEventArgs args);
        /// <summary>
        /// Occurs when plc address received.
        /// </summary>
        public event PlcAddressReceivedEventHandler PlcAddressReceived;

        /// <summary>
        /// Plc function received event.
        /// </summary>
        public delegate void PlcFunctionReceivedEventHandler(object sender, PlcFunctionReceivedEventArgs args);
        /// <summary>
        /// Occurs when plc command received.
        /// </summary>
        public event PlcFunctionReceivedEventHandler PlcFunctionReceived;

        /// <summary>
        /// RF data received event.
        /// </summary>
        public delegate void RfDataReceivedEventHandler(object sender, RfDataReceivedEventArgs args);

        /// <summary>
        /// Occurs when RF data is received.
        /// </summary>
        public event RfDataReceivedEventHandler RfDataReceived;

        /// <summary>
        /// X10 command received event.
        /// </summary>
        public delegate void X10CommandReceivedEventHandler(object sender, RfCommandReceivedEventArgs args);

        /// <summary>
        /// Occurs when x10 command received.
        /// </summary>
        public event X10CommandReceivedEventHandler RfCommandReceived;

        /// <summary>
        /// X10 security data received event.
        /// </summary>
        public delegate void X10SecurityReceivedEventHandler(object sender, RfSecurityReceivedEventArgs args);

        /// <summary>
        /// Occurs when x10 security data is received.
        /// </summary>
        public event X10SecurityReceivedEventHandler RfSecurityReceived;


        #endregion

        #region Instance Management

        /// <summary>
        /// Initializes a new instance of the <see cref="XTenLib.XTenManager"/> class.
        /// </summary>
        public XTenManager()
        {
            // Default house code is set to A
            HouseCode = "A";
            // Default interface is CM15: use "PortName" property to set a different interface driver
            x10Interface = new CM15();
        }

        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before the
        /// <see cref="XTenLib.XTenManager"/> is reclaimed by garbage collection.
        /// </summary>
        ~XTenManager()
        {
            Close();
        }

        #endregion

        #region Public Members

        #region X10 Configuration and Connection

        /// <summary>
        /// Connect to the X10 hardware.
        /// </summary>
        public bool Connect()
        {
            if (disconnectRequested)
                return false;
            lock (accessLock)
            {
                Disconnect();
                Open();
                connectionWatcher = new Thread(ConnectionWatcherTask);
                connectionWatcher.Start();
            }
            return IsConnected;
        }

        /// <summary>
        /// Connect from the X10 hardware.
        /// </summary>
        public void Disconnect()
        {
            if (disconnectRequested)
                return;
            disconnectRequested = true;
            Close();
            lock (accessLock)
            {
                if (connectionWatcher != null)
                {
                    if (!connectionWatcher.Join(5000))
                        connectionWatcher.Abort();
                    connectionWatcher = null;
                }
                disconnectRequested = false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the X10 hardware is connected.
        /// </summary>
        /// <value><c>true</c> if connected; otherwise, <c>false</c>.</value>
        public bool IsConnected
        {
            get { return (x10Interface != null && !disconnectRequested && (isInterfaceReady || (!gotReadWriteError && x10Interface.GetType().Equals(typeof(CM15))))); }
        }

        /// <summary>
        /// Gets or sets the name of the port. This can be "USB" when using CM15 hardware or the
        /// serial port address if using CM11 (eg. "COM7" on Windows or "/dev/ttyUSB1" on Linux).
        /// </summary>
        /// <value>The name of the port.</value>
        public string PortName
        {
            get { return portName; }
            set
            {
                if (portName != value)
                {
                    // set to erro so that the connection watcher will reconnect
                    // using the new port
                    Close();
                    // instantiate the requested interface
                    if (value.ToUpper() == "USB")
                    {
                        x10Interface = new CM15();
                    }
                    else
                    {
                        x10Interface = new CM11(value);
                    }
                }
                portName = value;
            }
        }

        /// <summary>
        /// Gets or sets the house codes. This string is a comma separated list of house codes (eg. "A,B,O")
        /// </summary>
        /// <value>The house code.</value>
        public string HouseCode
        {
            get { return monitoredHouseCode; }
            set
            {
                monitoredHouseCode = value;
                for (int i = 0; i < modules.Keys.Count; i++)
                {
                    modules[modules.Keys.ElementAt(i)].PropertyChanged -= Module_PropertyChanged;
                }
                modules.Clear();

                string[] hc = monitoredHouseCode.Split(',');
                for (int i = 0; i < hc.Length; i++)
                {
                    for (int uc = 1; uc <= 16; uc++)
                    {
                        var module = new X10Module(this, hc[i] + uc.ToString());
                        module.PropertyChanged += Module_PropertyChanged;
                        modules.Add(hc[i] + uc.ToString(), module);
                    }
                }

                if (!gotReadWriteError && x10Interface != null && x10Interface.GetType().Equals(typeof(CM15)))
                {
                    InitializeCm15();
                }
            }
        }

        /// <summary>
        /// Gets the list of all X10 modules or a specific module (eg. var modA5 = x10lib.Modules["A5"]).
        /// </summary>
        /// <value>The modules.</value>
        public Dictionary<string, X10Module> Modules
        {
            get { return modules; }
        }

        /// <summary>
        /// Gets the addressed modules.
        /// </summary>
        /// <value>The addressed modules list.</value>
        public List<X10Module> AddressedModules
        {
            get { return addressedModules; }
        }

        #endregion

        #region X10 Commands Implementation

        /// <summary>
        /// Dim the specified module (houseCode, unitCode) by the specified percentage.
        /// </summary>
        /// <param name="houseCode">House code.</param>
        /// <param name="unitCode">Unit code.</param>
        /// <param name="percentage">Percentage.</param>
        public void Dim(X10HouseCode houseCode, X10UnitCode unitCode, int percentage)
        {
            lock (commandLock)
            {
                string huCode = Utility.HouseUnitCodeFromEnum(houseCode, unitCode);
                string hcFunction = String.Format("{0:x1}{1:x1}", (int)houseCode, (int)X10Command.Dim);
                SendModuleAddress(houseCode, unitCode);
                if (x10Interface.GetType().Equals(typeof(CM15)))
                {
                    double normalized = (percentage / 100D);
                    SendMessage(new byte[] {
                        (int)X10CommandType.Function,
                        Byte.Parse(hcFunction, System.Globalization.NumberStyles.HexNumber),
                        (byte)(normalized * 210)
                    });
                    double newLevel = modules[huCode].Level - normalized;
                    if (newLevel < 0)
                        newLevel = 0;
                    modules[huCode].Level = newLevel;
                }
                else
                {
                    byte dimValue = Utility.GetDimValue(percentage);
                    SendMessage(new[] {
                        (byte)((int)X10CommandType.Function | dimValue | 0x04),
                        Byte.Parse(hcFunction, System.Globalization.NumberStyles.HexNumber)
                    });
                    double newLevel = modules[huCode].Level - Utility.GetPercentageValue(dimValue);
                    if (newLevel < 0)
                        newLevel = 0;
                    modules[huCode].Level = newLevel;
                }
            }
        }

        /// <summary>
        /// Brighten the specified module (houseCode, unitCode) by the specified percentage.
        /// </summary>
        /// <param name="houseCode">House code.</param>
        /// <param name="unitCode">Unit code.</param>
        /// <param name="percentage">Percentage.</param>
        public void Bright(X10HouseCode houseCode, X10UnitCode unitCode, int percentage)
        {
            lock (commandLock)
            {
                string huCode = Utility.HouseUnitCodeFromEnum(houseCode, unitCode);
                //string hcUnit = String.Format("{0:X}{1:X}", (int)houseCode, (int)unitCode);
                string hcFunction = String.Format("{0:x1}{1:x1}", (int)houseCode, (int)X10Command.Bright);
                SendModuleAddress(houseCode, unitCode);
                if (x10Interface.GetType().Equals(typeof(CM15)))
                {
                    double normalized = (percentage / 100D);
                    SendMessage(new byte[] {
                        (int)X10CommandType.Function,
                        Byte.Parse(hcFunction, System.Globalization.NumberStyles.HexNumber),
                        (byte)(normalized * 210)
                    });
                    double newLevel = modules[huCode].Level + normalized;
                    if (newLevel > 1)
                        newLevel = 1;
                    modules[huCode].Level = newLevel;
                }
                else
                {
                    byte dimValue = Utility.GetDimValue(percentage);
                    SendMessage(new byte[] {
                        (byte)((int)X10CommandType.Function | dimValue | 0x04),
                        Byte.Parse(hcFunction, System.Globalization.NumberStyles.HexNumber)
                    });
                    double newLevel = modules[huCode].Level + Utility.GetPercentageValue(dimValue);
                    if (newLevel > 1)
                        newLevel = 1;
                    modules[huCode].Level = newLevel;
                }
            }
        }

        /// <summary>
        /// Turn on the specified module (houseCode, unitCode).
        /// </summary>
        /// <param name="houseCode">House code.</param>
        /// <param name="unitCode">Unit code.</param>
        public void UnitOn(X10HouseCode houseCode, X10UnitCode unitCode)
        {
            lock (commandLock)
            {
                //string hcUnit = String.Format("{0:X}{1:X}", (int)houseCode, (int)unitCode);
                string hcFunction = String.Format("{0:x1}{1:x1}", (int)houseCode, (int)X10Command.On);
                SendModuleAddress(houseCode, unitCode);
                SendMessage(new byte[] {
                    (int)X10CommandType.Function,
                    byte.Parse(hcFunction, System.Globalization.NumberStyles.HexNumber)
                });
                string huc = Utility.HouseUnitCodeFromEnum(houseCode, unitCode);
                if (modules[huc].Level == 0.0)
                {
                    modules[huc].Level = 1.0;
                }
            }
        }

        /// <summary>
        /// Turn off the specified module (houseCode, unitCode).
        /// </summary>
        /// <param name="houseCode">House code.</param>
        /// <param name="unitCode">Unit code.</param>
        public void UnitOff(X10HouseCode houseCode, X10UnitCode unitCode)
        {
            lock (commandLock)
            {
                //string hcUnit = String.Format("{0:X}{1:X}", (int)houseCode, (int)unitCode);
                string hcFunction = String.Format("{0:x1}{1:x1}", (int)houseCode, (int)X10Command.Off);
                SendModuleAddress(houseCode, unitCode);
                SendMessage(new byte[] {
                    (int)X10CommandType.Function,
                    Byte.Parse(hcFunction, System.Globalization.NumberStyles.HexNumber)
                });
                string huc = Utility.HouseUnitCodeFromEnum(houseCode, unitCode);
                modules[huc].Level = 0.0;
            }
        }

        /// <summary>
        /// Turn on all the light modules with the given houseCode.
        /// </summary>
        /// <param name="houseCode">House code.</param>
        public void AllLightsOn(X10HouseCode houseCode)
        {
            lock (commandLock)
            {
                string hcUnit = String.Format("{0:X}{1:X}", (int)houseCode, 0);
                string hcFunction = String.Format("{0:x1}{1:x1}", (int)houseCode, (int)X10Command.All_Lights_On);
                SendMessage(new byte[] {
                    (int)X10CommandType.Address,
                    Byte.Parse(hcUnit, System.Globalization.NumberStyles.HexNumber)
                });
                SendMessage(new byte[] {
                    (int)X10CommandType.Function,
                    byte.Parse(hcFunction, System.Globalization.NumberStyles.HexNumber)
                });
                // TODO: pick only lights module
                CommandEvent_AllLightsOn(houseCode);
            }
        }

        /// <summary>
        /// Turn off all the light modules with the given houseCode.
        /// </summary>
        /// <param name="houseCode">House code.</param>
        public void AllUnitsOff(X10HouseCode houseCode)
        {
            lock (commandLock)
            {
                string hcUnit = String.Format("{0:X}{1:X}", (int)houseCode, 0);
                string hcFunction = String.Format("{0:x1}{1:x1}", (int)houseCode, (int)X10Command.All_Units_Off);
                SendMessage(new byte[] {
                    (int)X10CommandType.Address,
                    Byte.Parse(hcUnit, System.Globalization.NumberStyles.HexNumber)
                });
                SendMessage(new byte[] {
                    (int)X10CommandType.Function,
                    Byte.Parse(hcFunction, System.Globalization.NumberStyles.HexNumber)
                });
                // TODO: pick only lights module
                CommandEvent_AllUnitsOff(houseCode);
            }
        }

        /// <summary>
        /// Request module status.
        /// </summary>
        /// <param name="houseCode">House code.</param>
        /// <param name="unitCode">Unit code.</param>
        public void StatusRequest(X10HouseCode houseCode, X10UnitCode unitCode)
        {
            lock (commandLock)
            {
                //string hcUnit = String.Format("{0:X}{1:X}", (int)houseCode, (int)unitCode);
                string hcFunction = String.Format("{0:x1}{1:x1}", (int)houseCode, (int)X10Command.Status_Request);
                SendModuleAddress(houseCode, unitCode);
                SendMessage(new byte[] {
                    (int)X10CommandType.Function,
                    Byte.Parse(hcFunction, System.Globalization.NumberStyles.HexNumber)
                });
            }
        }

        #endregion

        #endregion

        #region Private Members

        #region X10 Interface Commands

        private void SendModuleAddress(X10HouseCode housecode, X10UnitCode unitcode)
        {
            // TODO: do more tests about this optimization (and comment out the "if" if tests are successfully)
            //if (!addressedModules.Contains(mod) || addressedModules.Count > 1) // optimization disabled, uncomment to enable
            {
                UnselectModules();
                SelectModule(Utility.HouseUnitCodeFromEnum(housecode, unitcode));
                string hcUnit = String.Format("{0:X}{1:X}", (int)housecode, (int)unitcode);
                SendMessage(new byte[] {
                    (int)X10CommandType.Address,
                    Byte.Parse(hcUnit, System.Globalization.NumberStyles.HexNumber)
                });
                newAddressData = true;
            }
        }

        private void UpdateInterfaceTime(bool batteryClear)
        {
            /*
            The PC must then respond with the following transmission

            Bit range	Description
            55 to 48	timer download header (0x9b)
            47 to 40	Current time (seconds)
            39 to 32	Current time (minutes ranging from 0 to 119)
            31 to 23	Current time (hours/2, ranging from 0 to 11)
            23 to 16	Current year day (bits 0 to 7)
            15	Current year day (bit 8)
            14 to 8		Day mask (SMTWTFS)
            7 to 4		Monitored house code
            3		Reserved
            2		Battery timer clear flag
            1		Monitored status clear flag
            0		Timer purge flag
            */
            var date = DateTime.Now;
            int minute = date.Minute;
            int hour = date.Hour / 2;
            if (Math.IEEERemainder(date.Hour, 2) > 0)
            { 
                // Add remaining minutes 
                minute += 60;
            }
            int wday = Convert.ToInt16(Math.Pow(2, (int)date.DayOfWeek));
            int yearDay = date.DayOfYear - 1;
            if (yearDay > 255)
            {
                yearDay = yearDay - 256;
                // Set current yearDay flag in wday's 7:th bit, since yearDay overflowed...
                wday = wday + Convert.ToInt16(Math.Pow(2, 7));
            }
            // Build message
            byte[] message = new byte[8];
            message[0] = 0x9b;   // cm11 x10 time download header
            message[1] = Convert.ToByte(date.Second);
            message[2] = Convert.ToByte(minute);
            message[3] = Convert.ToByte(hour);
            message[4] = Convert.ToByte(yearDay);
            message[5] = Convert.ToByte(wday);
            message[6] = Convert.ToByte((batteryClear ? 0x07 : 0x03) + Utility.HouseCodeFromString(this.HouseCode)); // Send timer purgeflag + Monitored status clear flag, monitored house code.

            if (x10Interface.GetType().Equals(typeof(CM15)))
            {
                // this seems to be needed only with CM15
                message[7] = 0x02;
            }

            UnselectModules();
            SendMessage(message);
        }

        private void InitializeCm15()
        {
            lock (commandLock)
            {
                // BuildTransceivedCodesMessage return byte message for setting transceive codes from given comma separated _monitoredhousecode
                UpdateInterfaceTime(false);
                byte[] trCommand = CM15.BuildTransceivedCodesMessage(monitoredHouseCode);
                SendMessage(trCommand);
                SendMessage(new byte[] { 0x8B });
            }
        }

        #endregion

        #region X10 Command Input Events and Modules status update

        private void CommandEvent_On()
        {
            for (int m = 0; m < addressedModules.Count; m++)
            {
                X10Module mod = addressedModules[m];
                mod.Level = 1.0;
            }
        }

        private void CommandEvent_Off()
        {
            for (int m = 0; m < addressedModules.Count; m++)
            {
                X10Module mod = addressedModules[m];
                mod.Level = 0.0;
            }
        }

        private void CommandEvent_Bright(byte parameter)
        {
            for (int m = 0; m < addressedModules.Count; m++)
            {
                X10Module mod = addressedModules[m];
                var brightLevel = Math.Round(mod.Level + (((double)parameter) / 210D), 2);
                if (brightLevel > 1)
                    brightLevel = 1;
                mod.Level = brightLevel;
            }
        }

        private void CommandEvent_Dim(byte parameter)
        {
            for (int m = 0; m < addressedModules.Count; m++)
            {
                X10Module mod = addressedModules[m];
                var dimLevel = Math.Round(mod.Level - (((double)parameter) / 210D), 2);
                if (dimLevel < 0)
                    dimLevel = 0;
                mod.Level = dimLevel;
            }
        }

        private void CommandEvent_AllUnitsOff(X10HouseCode houseCode)
        {
            UnselectModules();
            // TODO: select only light modules 
            foreach (KeyValuePair<string, X10Module> modkv in modules)
            {
                if (modkv.Value.Code.StartsWith(houseCode.ToString()))
                {
                    modkv.Value.Level = 0.0;
                }
            }
        }

        private void CommandEvent_AllLightsOn(X10HouseCode houseCode)
        {
            UnselectModules();
            // TODO: pick only light modules 
            foreach (KeyValuePair<string, X10Module> modkv in modules)
            {
                if (modkv.Value.Code.StartsWith(houseCode.ToString()))
                {
                    modkv.Value.Level = 1.0;
                }
            }
        }

        #endregion

        #region Modules life cycle and events

        private X10Module SelectModule(string address)
        {
            if (!modules.Keys.Contains(address))
            {
                var newModule = new X10Module(this, address);
                newModule.PropertyChanged += Module_PropertyChanged;
                modules.Add(address, newModule);
            }
            var module = modules[address];
            if (!addressedModules.Contains(module))
            {
                addressedModules.Add(module);
            }
            return module;
        }

        private void UnselectModules()
        {
            addressedModules.Clear();
        }

        private void Module_PropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            // Route event to listeners
            if (ModuleChanged == null) return;
            try
            {
                ModuleChanged(sender, args);
            }
            catch (Exception e)
            { 
                logger.Error(e);
            }
        }

        #endregion

        #region X10 Interface I/O operations

        private bool Open()
        {
            bool success;
            lock (accessLock)
            {
                Close();
                success = (x10Interface != null && x10Interface.Open());
                if (success)
                {
                    if (x10Interface.GetType().Equals(typeof(CM15)))
                    {
                        // Set transceived house codes for CM15 X10 RF-->PLC
                        InitializeCm15();
                        // For CM15 we do not need to receive ACK message to claim status as connected
                        OnConnectionStatusChanged(new ConnectionStatusChangedEventArgs(true));
                    }
                    // Start the Reader task
                    gotReadWriteError = false;
                    // Start the Reader task
                    reader = new Thread(ReaderTask);
                    reader.Start();
                }
            }
            return success;
        }

        private void Close()
        {
            UnselectModules();
            lock (accessLock)
            {
                // Dispose the X10 interface
                try
                {
                    x10Interface.Close();
                }
                catch (Exception e)
                {
                    logger.Error(e);
                }
                gotReadWriteError = true;
                // Stop the Reader task
                if (reader != null)
                {
                    if (!reader.Join(5000))
                        reader.Abort();
                    reader = null;
                }
                OnConnectionStatusChanged(new ConnectionStatusChangedEventArgs(false));
            }
        }

        private void SendMessage(byte[] message)
        {
            try
            {
                if (message.Length > 1 && IsConnected)
                {
                    // Wait for message delivery acknowledge
                    lock (waitAckMonitor)
                    {
                        // have a 500ms pause between each output message
                        while ((DateTime.Now - lastReceivedTs).TotalMilliseconds < 500)
                        {
                            Thread.Sleep(1);
                        }

                        logger.Debug(BitConverter.ToString(message));
                        if (!x10Interface.WriteData(message))
                        {
                            logger.Warn("Interface I/O error");
                        }

                        commandLastMessage = message;
                        waitAckTimestamp = DateTime.Now;

                        if (x10Interface.GetType().Equals(typeof(CM11)))
                        {
                            expectedChecksum = (byte)((message[0] + message[1]) & 0xff);
                            communicationState = X10CommState.WaitingChecksum;
                        }
                        else
                        {
                            communicationState = X10CommState.WaitingAck;
                        }

                        while (commandResendAttempts < CommandResendMax && communicationState != X10CommState.Ready)
                        {
                            var elapsedFromWaitAck = DateTime.Now - waitAckTimestamp;
                            while (elapsedFromWaitAck.TotalSeconds < CommandTimeoutSeconds && communicationState != X10CommState.Ready)
                            {
                                Thread.Sleep(1);
                                elapsedFromWaitAck = DateTime.Now - waitAckTimestamp;
                            }
                            if (elapsedFromWaitAck.TotalSeconds >= CommandTimeoutSeconds && communicationState != X10CommState.Ready)
                            {
                                // Resend last message
                                commandResendAttempts++;
                                logger.Warn("Previous command timed out, resending ({0})", commandResendAttempts);
                                if (!x10Interface.WriteData(commandLastMessage))
                                {
                                    logger.Warn("Interface I/O error");
                                }
                                waitAckTimestamp = DateTime.Now;
                            }
                        }
                        commandResendAttempts = 0;
                        commandLastMessage = new byte[0];
                    }
                }
                else
                {
                    logger.Debug(BitConverter.ToString(message));
                    if (!x10Interface.WriteData(message))
                    {
                        logger.Warn("Interface I/O error");
                    }                      
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                gotReadWriteError = true;
            }
        }

        private void ReaderTask()
        {
            while (x10Interface != null && !disconnectRequested)
            {
                try
                {
                    byte[] readData = x10Interface.ReadData();
                    if (readData.Length > 0)
                    {
                        logger.Debug(BitConverter.ToString(readData));
                        // last command ACK timeout
                        var elapsedFromWaitAck = DateTime.Now - waitAckTimestamp;
                        if (elapsedFromWaitAck.TotalSeconds >= CommandTimeoutSeconds && communicationState != X10CommState.Ready)
                        {
                            logger.Warn("Command acknowledge timeout");
                            communicationState = X10CommState.Ready;
                        }
                        // last command succesfully sent
                        if (communicationState == X10CommState.WaitingAck && readData[0] == (int)X10CommandType.PLC_Ready && readData.Length <= 2) // ack received
                        {
                            logger.Debug("Command succesfull");
                            communicationState = X10CommState.Ready;
                        }
                        else if ((readData.Length >= 13 || (readData.Length == 2 && readData[0] == 0xFF && readData[1] == 0x00)) && !isInterfaceReady)
                        {
                            OnConnectionStatusChanged(new ConnectionStatusChangedEventArgs(true));
                            UpdateInterfaceTime(false);
                            communicationState = X10CommState.Ready;
                        }
                        else if (readData.Length == 2 && communicationState == X10CommState.WaitingChecksum && readData[0] == expectedChecksum && readData[1] == 0x00)
                        {
                            // checksum is received only from CM11
                            logger.Debug("Received checksum {0}, expected {1}", BitConverter.ToString(readData), expectedChecksum.ToString("X2"));
                            //TODO: checksum verification not handled, we just reply 0x00 (OK)
                            SendMessage(new byte[] { 0x00 });
                            communicationState = X10CommState.WaitingAck;
                        }
                        else if (readData[0] == (int)X10CommandType.Macro)
                        {
                            lastReceivedTs = DateTime.Now;
                            logger.Debug("MACRO: {0}", BitConverter.ToString(readData));
                        }
                        else if (readData[0] == (int)X10CommandType.RF)
                        {
                            lastReceivedTs = DateTime.Now;

                            bool isSecurityCode = (readData.Length == 8 && readData[1] == (byte)X10Defs.RfSecurityPrefix && ((readData[3] ^ readData[2]) == 0x0F) && ((readData[5] ^ readData[4]) == 0xFF));
                            bool isCodeValid = isSecurityCode || (readData.Length == 6 && readData[1] == (byte)X10Defs.RfCommandPrefix && ((readData[3] & ~readData[2]) == readData[3] && (readData[5] & ~readData[4]) == readData[5]));

                            // Still unknown meaning of the last byte in security codes
                            if (isSecurityCode && readData[7] == 0x80)
                                readData[7] = 0x00;
                            
                            // Repeated messages check
                            if (isCodeValid)
                            {
                                if (lastRfMessage == BitConverter.ToString(readData) && (lastReceivedTs - lastRfReceivedTs).TotalMilliseconds < MinRfRepeatDelayMs)
                                {
                                    logger.Warn("Ignoring repeated message within {0}ms", MinRfRepeatDelayMs);
                                    continue;
                                }
                                lastRfMessage = BitConverter.ToString(readData);
                                lastRfReceivedTs = DateTime.Now;
                            }

                            logger.Debug("RFCOM: {0}", BitConverter.ToString(readData));
                            OnRfDataReceived(new RfDataReceivedEventArgs(readData));

                            // Decode received 32 bit message
                            // house code + 4th bit of unit code
                            // unit code (3 bits) + function code
                            if (isSecurityCode)
                            {
                                var securityEvent = X10RfSecurityEvent.NotSet;
                                Enum.TryParse<X10RfSecurityEvent>(readData[4].ToString(), out securityEvent);
                                uint securityAddress = BitConverter.ToUInt32(new byte[] { readData[2], readData[6], readData[7], 0x00 }, 0);
                                if (securityEvent != X10RfSecurityEvent.NotSet)
                                {
                                    logger.Debug("Security Event {0} Address {1}", securityEvent, securityAddress);
                                    OnRfSecurityReceived(new RfSecurityReceivedEventArgs(securityEvent, securityAddress));
                                }
                                else
                                {
                                    logger.Warn("Could not parse security event");
                                }
                            }
                            else if (isCodeValid)
                            {
                                // Parse function code
                                var hf = X10RfFunction.NotSet;
                                Enum.TryParse<X10RfFunction>(readData[4].ToString(), out hf);
                                // House code (4bit) + unit code (4bit)
                                byte hu = readData[2];
                                // Parse house code
                                var houseCode = X10HouseCode.NotSet;
                                Enum.TryParse<X10HouseCode>((Utility.ReverseByte((byte)(hu >> 4)) >> 4).ToString(), out houseCode);
                                switch (hf)
                                {
                                case X10RfFunction.Dim:
                                case X10RfFunction.Bright:
                                    logger.Debug("Command {0}", hf);
                                    if (hf == X10RfFunction.Dim)
                                        CommandEvent_Dim((byte)X10Defs.DimBrightStep);
                                    else
                                        CommandEvent_Bright((byte)X10Defs.DimBrightStep);
                                    OnRfCommandReceived(new RfCommandReceivedEventArgs(hf, X10HouseCode.NotSet, X10UnitCode.Unit_NotSet));
                                    break;
                                case X10RfFunction.AllLightsOn:
                                case X10RfFunction.AllLightsOff:
                                    if (houseCode != X10HouseCode.NotSet)
                                    {
                                        logger.Debug("Command {0} HouseCode {1}", hf, houseCode);
                                        if (hf == X10RfFunction.AllLightsOn)
                                            CommandEvent_AllLightsOn(houseCode);
                                        else
                                            CommandEvent_AllUnitsOff(houseCode);
                                        OnRfCommandReceived(new RfCommandReceivedEventArgs(hf, houseCode, X10UnitCode.Unit_NotSet));
                                    }
                                    else
                                    {
                                        logger.Warn("Unable to decode house code value");
                                    }
                                    break;
                                case X10RfFunction.NotSet:
                                    logger.Warn("Unable to decode function value");
                                    break;
                                default:
                                    // Parse unit code
                                    string houseUnit = Convert.ToString(hu, 2).PadLeft(8, '0');
                                    string unitFunction = Convert.ToString(readData[4], 2).PadLeft(8, '0');
                                    string uc = (Convert.ToInt16(houseUnit.Substring(5, 1) + unitFunction.Substring(1, 1) + unitFunction.Substring(4, 1) + unitFunction.Substring(3, 1), 2) + 1).ToString();
                                    // Parse module function
                                    var fn = X10RfFunction.NotSet;
                                    Enum.TryParse<X10RfFunction>(unitFunction[2].ToString(), out fn);
                                    switch (fn)
                                    {
                                    case X10RfFunction.On:
                                    case X10RfFunction.Off:
                                        var unitCode = X10UnitCode.Unit_NotSet;
                                        Enum.TryParse<X10UnitCode>("Unit_" + uc.ToString(), out unitCode);
                                        if (unitCode != X10UnitCode.Unit_NotSet)
                                        {
                                            logger.Debug("Command {0} HouseCode {1} UnitCode {2}", fn, houseCode, unitCode.Value());
                                            UnselectModules();
                                            SelectModule(houseCode.ToString() + unitCode.Value().ToString());
                                            if (fn == X10RfFunction.On)
                                                CommandEvent_On();
                                            else
                                                CommandEvent_Off();
                                            OnRfCommandReceived(new RfCommandReceivedEventArgs(fn, houseCode, unitCode));
                                        }
                                        else
                                        {
                                            logger.Warn("Could not parse unit code");
                                        }
                                        break;
                                    }
                                    break;
                                }
                            }
                            else
                            {
                                logger.Warn("Bad Rf message received");
                            }
                        }
                        else if ((readData[0] == (int)X10CommandType.PLC_Poll) && readData.Length <= 2)
                        {
                            OnConnectionStatusChanged(new ConnectionStatusChangedEventArgs(true));
                            SendMessage(new byte[] { (byte)X10CommandType.PLC_ReplyToPoll }); // reply to poll
                        }
                        else if ((readData[0] == (int)X10CommandType.PLC_FilterFail_Poll) && readData.Length <= 2)
                        {
                            OnConnectionStatusChanged(new ConnectionStatusChangedEventArgs(true));
                            SendMessage(new byte[] { (int)X10CommandType.PLC_FilterFail_Poll }); // reply to filter fail poll
                        }
                        else if ((readData[0] == (int)X10CommandType.PLC_Poll))
                        {
                            lastReceivedTs = DateTime.Now;
                            logger.Debug("PLCRX: {0}", BitConverter.ToString(readData));

                            if (readData.Length <= 3) continue;
                            int messageLength = readData[1];
                            if (readData.Length <= messageLength - 2) continue;
                            char[] bitmapData = Convert.ToString(readData[2], 2).PadLeft(8, '0').ToCharArray();
                            byte[] functionBitmap = new byte[messageLength - 1];
                            for (int i = 0; i < functionBitmap.Length; i++)
                            {
                                functionBitmap[i] = Byte.Parse(bitmapData[7 - i].ToString());
                            }

                            byte[] messageData = new byte[messageLength - 1];
                            Array.Copy(readData, 3, messageData, 0, messageLength - 1);

                            // CM15 Extended receive has got inverted data
                            if (messageLength > 2 && x10Interface.GetType().Equals(typeof(CM15)))
                            {
                                Array.Reverse(functionBitmap, 0, functionBitmap.Length);
                                Array.Reverse(messageData, 0, messageData.Length);
                            }

                            logger.Debug("FNMAP: {0}", BitConverter.ToString(functionBitmap));
                            logger.Debug("DATA : {0}", BitConverter.ToString(messageData));

                            for (int b = 0; b < messageData.Length; b++)
                            {
                                // read current byte data (type: 0x00 address, 0x01 function)
                                if (functionBitmap[b] == (byte)X10FunctionType.Address) // address
                                {
                                    X10HouseCode houseCode = (X10HouseCode)Convert.ToInt16(messageData[b].ToString("X2").Substring(0, 1), 16);
                                    X10UnitCode unitCode = (X10UnitCode)Convert.ToInt16(messageData[b].ToString("X2").Substring(1, 1), 16);
                                    string address = Utility.HouseUnitCodeFromEnum(houseCode, unitCode);

                                    logger.Debug("      {0}) Address = {1}", b, address);

                                    if (newAddressData)
                                    {
                                        newAddressData = false;
                                        UnselectModules();
                                    }
                                    SelectModule(address);

                                    OnPlcAddressReceived(new PlcAddressReceivedEventArgs(houseCode, unitCode));
                                }
                                else if (functionBitmap[b] == (byte)X10FunctionType.Function) // function
                                {
                                    var command = (X10Command)Convert.ToInt16(messageData[b].ToString("X2").Substring(1, 1), 16);
                                    var houseCode = X10HouseCode.NotSet;
                                    Enum.TryParse<X10HouseCode>(Convert.ToInt16(messageData[b].ToString("X2").Substring(0, 1), 16).ToString(), out houseCode);

                                    logger.Debug("      {0}) House code = {1}", b, houseCode);
                                    logger.Debug("      {0})    Command = {1}", b, command);

                                    switch (command)
                                    {
                                        case X10Command.All_Lights_Off:
                                            if (houseCode != X10HouseCode.NotSet)
                                                CommandEvent_AllUnitsOff(houseCode);
                                            break;
                                        case X10Command.All_Lights_On:
                                            if (houseCode != X10HouseCode.NotSet)
                                                CommandEvent_AllLightsOn(houseCode);
                                            break;
                                        case X10Command.On:
                                            CommandEvent_On();
                                            break;
                                        case X10Command.Off:
                                            CommandEvent_Off();
                                            break;
                                        case X10Command.Bright:
                                            CommandEvent_Bright(messageData[++b]);
                                            break;
                                        case X10Command.Dim:
                                            CommandEvent_Dim(messageData[++b]);
                                            break;
                                    }
                                    newAddressData = true;

                                    OnPlcFunctionReceived(new PlcFunctionReceivedEventArgs(command, houseCode));
                                }
                            }
                        }
                        else if ((readData[0] == (int)X10CommandType.PLC_TimeRequest)) // IS THIS A TIME REQUEST?
                        {
                            UpdateInterfaceTime(false);
                        }
                        else
                        {

                            #region This is an hack for detecting disconnection status on Linux/Mono platforms

                            if (readData[0] == 0x00)
                            {
                                zeroChecksumCount++;
                            }
                            else
                            {
                                zeroChecksumCount = 0;
                            }
                            //
                            if (zeroChecksumCount > 10)
                            {
                                zeroChecksumCount = 0;
                                gotReadWriteError = true;
                            }
                            else
                            {
                                SendMessage(new byte[] { 0x00 });
                            }

                            #endregion

                        }
                    }
                }
                catch (Exception e)
                {
                    if (!e.GetType().Equals(typeof(TimeoutException)) && !e.GetType().Equals(typeof(OverflowException)))
                    {
                        gotReadWriteError = true;
                        logger.Error(e);
                    }
                }
            }
        }

        private void ConnectionWatcherTask()
        {
            // This task takes care of automatically reconnecting the interface
            // when the connection is drop or if an I/O error occurs
            while (!disconnectRequested)
            {
                if (gotReadWriteError)
                {
                    try
                    {
                        Close();
                        // wait 3 secs before reconnecting
                        Thread.Sleep(3000);
                        if (!disconnectRequested)
                        {
                            try
                            {
                                Open();
                            }
                            catch (Exception e)
                            { 
                                logger.Error(e);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error(e);
                    }
                }
                if (!disconnectRequested)
                    Thread.Sleep(1000);
            }
        }

        #endregion

        #region Events Raising

        /// <summary>
        /// Raises the connected state changed event.
        /// </summary>
        /// <param name="args">Arguments.</param>
        protected virtual void OnConnectionStatusChanged(ConnectionStatusChangedEventArgs args)
        {
            // ensure the status is really changing
            if (isInterfaceReady != args.Connected)
            {
                isInterfaceReady = args.Connected;
                // raise the event
                ConnectionStatusChanged?.Invoke(this, args);
            }
        }

        /// <summary>
        /// Raises the plc address received event.
        /// </summary>
        /// <param name="args">Arguments.</param>
        protected virtual void OnPlcAddressReceived(PlcAddressReceivedEventArgs args)
        {
            if (PlcAddressReceived != null)
                PlcAddressReceived(this, args);
        }

        /// <summary>
        /// Raises the plc function received event.
        /// </summary>
        /// <param name="args">Arguments.</param>
        protected virtual void OnPlcFunctionReceived(PlcFunctionReceivedEventArgs args)
        {
            if (PlcFunctionReceived != null)
                PlcFunctionReceived(this, args);
        }

        /// <summary>
        /// Raises the rf data received event.
        /// </summary>
        /// <param name="args">Arguments.</param>
        protected virtual void OnRfDataReceived(RfDataReceivedEventArgs args)
        {
            if (RfDataReceived != null)
                RfDataReceived(this, args);
        }

        /// <summary>
        /// Raises the RF command received event.
        /// </summary>
        /// <param name="args">Arguments.</param>
        protected virtual void OnRfCommandReceived(RfCommandReceivedEventArgs args)
        {
            if (RfCommandReceived != null)
                RfCommandReceived(this, args);
        }

        /// <summary>
        /// Raises the RF security received event.
        /// </summary>
        /// <param name="args">Arguments.</param>
        protected virtual void OnRfSecurityReceived(RfSecurityReceivedEventArgs args)
        {
            if (RfSecurityReceived != null)
                RfSecurityReceived(this, args);
        }

        #endregion

        #endregion

    }

}

