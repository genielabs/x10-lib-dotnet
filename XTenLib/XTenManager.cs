/*
    This file is part of XTenLib source code.

    XTenLib is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTenLib is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTenLib.  If not, see <http://www.gnu.org/licenses/>.  
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
        private XTenInterface x10interface;
        private Dictionary<string, X10Module> modules = new Dictionary<string, X10Module>();
        private string portName = "USB";
        private string monitoredHouseCode = "A";

        // Variables for storing last addressed house codes and optmizing/speeding up X10 communication
        private List<X10Module> addressedModules = new List<X10Module>();
        private bool newAddressData = true;

        // State variables
        private bool isInterfaceReady = false;
        private X10CommState communicationState = X10CommState.Ready;
        private byte expectedChecksum = 0x00;

        // Max resend attempts when a X10 command failed
        private const int commandResendMax = 1;
        // Max wait for command acknowledge
        private double commandTimeoutSeconds = 5.0;
        // Store last X10 message (used to resend on error)
        private byte[] commandLastMessage = new byte[0];
        private int commandResendAttempts = 0;
        // I/O operation lock / monitor
        private object waitAckMonitor = new object();
        private object commandLock = new object();

        // Timestamps used for detecting communication timeouts
        private DateTime waitAckTimestamp = DateTime.Now;
        private DateTime lastReceivedTs = DateTime.Now;
        // Variables used for preventing duplicated messages coming from RF
        private DateTime lastRfReceivedTs = DateTime.Now;
        private string lastRfMessage = "";

        // Read/Write error state variable
        private bool gotReadWriteError = true;

        // X10 interface reader Task
        private Task readerTask;
        private CancellationTokenSource readerTokenSource;

        // X10 interface connection watcher
        private Task connectionWatcher;
        private CancellationTokenSource watcherTokenSource;

        // This is used on Linux/Mono for detecting when the link gets disconnected
        private int zeroChecksumCount = 0;

        #endregion

        #region Public Events

        /// <summary>
        /// Occurs when an X10 module changed.
        /// </summary>
        public event PropertyChangedEventHandler ModuleChanged;
        /// <summary>
        /// Occurs when RF data is received.
        /// </summary>
        public event Action<RfDataReceivedAction> RfDataReceived;

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
            x10interface = new CM15();
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
            Disconnect();
            bool returnValue = Open();
            gotReadWriteError = !returnValue;
            watcherTokenSource = new CancellationTokenSource();
            connectionWatcher = Task.Factory.StartNew(() => ConnectionWatcherTask(watcherTokenSource.Token), watcherTokenSource.Token);
            return returnValue;
        }

        /// <summary>
        /// Connect from the X10 hardware.
        /// </summary>
        public void Disconnect()
        {
            if (connectionWatcher != null)
            {
                watcherTokenSource.Cancel();
                connectionWatcher.Wait(5000);
                connectionWatcher.Dispose();
                connectionWatcher = null;
                watcherTokenSource = null;
            }
            Close();
        }

        /// <summary>
        /// Gets a value indicating whether the X10 hardware is connected.
        /// </summary>
        /// <value><c>true</c> if connected; otherwise, <c>false</c>.</value>
        public bool IsConnected
        {
            get { return isInterfaceReady || (!gotReadWriteError && x10interface.GetType().Equals(typeof(CM15))); }
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
                    Close();

                    if (value.ToUpper() == "USB")
                    {
                        x10interface = new CM15();
                    }
                    else
                    {
                        x10interface = new CM11(value);
                    }

                    gotReadWriteError = true;
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

                if (!gotReadWriteError && x10interface != null && x10interface.GetType().Equals(typeof(CM15)))
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

        #endregion

        #region X10 Commands Implementation

        /// <summary>
        /// Dim the specified module (housecode, unitcode) by the specified percentage.
        /// </summary>
        /// <param name="housecode">Housecode.</param>
        /// <param name="unitcode">Unitcode.</param>
        /// <param name="percentage">Percentage.</param>
        public void Dim(X10HouseCode housecode, X10UnitCode unitcode, int percentage)
        {
            lock (commandLock)
            {
                string huc = Utility.HouseUnitCodeFromEnum(housecode, unitcode);
                string hcfuntion = String.Format("{0:x1}{1:x1}", (int)housecode, (int)X10Command.Dim);
                SendModuleAddress(housecode, unitcode);
                if (x10interface.GetType().Equals(typeof(CM15)))
                {
                    double normalized = ((double)percentage / 100D);
                    SendMessage(new byte[] {
                        (int)X10CommandType.Function,
                        byte.Parse(hcfuntion, System.Globalization.NumberStyles.HexNumber),
                        (byte)(normalized * 210)
                    });
                    double newLevel = modules[huc].Level - normalized;
                    if (newLevel < 0)
                        newLevel = 0;
                    modules[huc].Level = newLevel;
                }
                else
                {
                    byte dimvalue = Utility.GetDimValue(percentage);
                    SendMessage(new byte[] {
                        (byte)((int)X10CommandType.Function | dimvalue | 0x04),
                        byte.Parse(hcfuntion, System.Globalization.NumberStyles.HexNumber)
                    });
                    double newLevel = modules[huc].Level - Utility.GetPercentageValue(dimvalue);
                    if (newLevel < 0)
                        newLevel = 0;
                    modules[huc].Level = newLevel;
                }
            }
        }

        /// <summary>
        /// Brighten the specified module (housecode, unitcode) by the specified percentage.
        /// </summary>
        /// <param name="housecode">Housecode.</param>
        /// <param name="unitcode">Unitcode.</param>
        /// <param name="percentage">Percentage.</param>
        public void Bright(X10HouseCode housecode, X10UnitCode unitcode, int percentage)
        {
            lock (commandLock)
            {
                string huc = Utility.HouseUnitCodeFromEnum(housecode, unitcode);
                //string hcunit = String.Format("{0:X}{1:X}", (int)housecode, (int)unitcode);
                string hcfuntion = String.Format("{0:x1}{1:x1}", (int)housecode, (int)X10Command.Bright);
                SendModuleAddress(housecode, unitcode);
                if (x10interface.GetType().Equals(typeof(CM15)))
                {
                    double normalized = ((double)percentage / 100D);
                    SendMessage(new byte[] {
                        (int)X10CommandType.Function,
                        byte.Parse(hcfuntion, System.Globalization.NumberStyles.HexNumber),
                        (byte)(normalized * 210)
                    });
                    double newLevel = modules[huc].Level + normalized;
                    if (newLevel > 1)
                        newLevel = 1;
                    modules[huc].Level = newLevel;
                }
                else
                {
                    byte dimvalue = Utility.GetDimValue(percentage);
                    SendMessage(new byte[] {
                        (byte)((int)X10CommandType.Function | dimvalue | 0x04),
                        byte.Parse(hcfuntion, System.Globalization.NumberStyles.HexNumber)
                    });
                    double newLevel = modules[huc].Level + Utility.GetPercentageValue(dimvalue);
                    if (newLevel > 1)
                        newLevel = 1;
                    modules[huc].Level = newLevel;
                }
            }
        }

        /// <summary>
        /// Turn on the specified module (housecode, unitcode).
        /// </summary>
        /// <param name="housecode">Housecode.</param>
        /// <param name="unitcode">Unitcode.</param>
        public void UnitOn(X10HouseCode housecode, X10UnitCode unitcode)
        {
            lock (commandLock)
            {
                //string hcunit = String.Format("{0:X}{1:X}", (int)housecode, (int)unitcode);
                string hcfuntion = String.Format("{0:x1}{1:x1}", (int)housecode, (int)X10Command.On);
                SendModuleAddress(housecode, unitcode);
                SendMessage(new byte[] {
                    (int)X10CommandType.Function,
                    byte.Parse(hcfuntion, System.Globalization.NumberStyles.HexNumber)
                });
                string huc = Utility.HouseUnitCodeFromEnum(housecode, unitcode);
                if (modules[huc].Level == 0.0)
                {
                    modules[huc].Level = 1.0;
                }
            }
        }

        /// <summary>
        /// Turn off the specified module (housecode, unitcode).
        /// </summary>
        /// <param name="housecode">Housecode.</param>
        /// <param name="unitcode">Unitcode.</param>
        public void UnitOff(X10HouseCode housecode, X10UnitCode unitcode)
        {
            lock (commandLock)
            {
                //string hcunit = String.Format("{0:X}{1:X}", (int)housecode, (int)unitcode);
                string hcfuntion = String.Format("{0:x1}{1:x1}", (int)housecode, (int)X10Command.Off);
                SendModuleAddress(housecode, unitcode);
                SendMessage(new byte[] {
                    (int)X10CommandType.Function,
                    byte.Parse(hcfuntion, System.Globalization.NumberStyles.HexNumber)
                });
                string huc = Utility.HouseUnitCodeFromEnum(housecode, unitcode);
                modules[huc].Level = 0.0;
            }
        }

        /// <summary>
        /// Turn on all the light modules with the given housecode.
        /// </summary>
        /// <param name="housecode">Housecode.</param>
        public void AllLightsOn(X10HouseCode housecode)
        {
            lock (commandLock)
            {
                string hcunit = String.Format("{0:X}{1:X}", (int)housecode, 0);
                string hcfuntion = String.Format("{0:x1}{1:x1}", (int)housecode, (int)X10Command.All_Lights_On);
                SendMessage(new byte[] {
                    (int)X10CommandType.Address,
                    byte.Parse(hcunit, System.Globalization.NumberStyles.HexNumber)
                });
                SendMessage(new byte[] {
                    (int)X10CommandType.Function,
                    byte.Parse(hcfuntion, System.Globalization.NumberStyles.HexNumber)
                });
                // TODO: pick only lights module
                CommandEvent_AllLightsOn(housecode.ToString());
            }
        }

        /// <summary>
        /// Turn off all the light modules with the given housecode.
        /// </summary>
        /// <param name="housecode">Housecode.</param>
        public void AllUnitsOff(X10HouseCode housecode)
        {
            lock (commandLock)
            {
                string hcunit = String.Format("{0:X}{1:X}", (int)housecode, 0);
                string hcfuntion = String.Format("{0:x1}{1:x1}", (int)housecode, (int)X10Command.All_Units_Off);
                SendMessage(new byte[] {
                    (int)X10CommandType.Address,
                    byte.Parse(hcunit, System.Globalization.NumberStyles.HexNumber)
                });
                SendMessage(new byte[] {
                    (int)X10CommandType.Function,
                    byte.Parse(hcfuntion, System.Globalization.NumberStyles.HexNumber)
                });
                // TODO: pick only lights module
                CommandEvent_AllUnitsOff(housecode.ToString());
            }
        }

        /// <summary>
        /// Request module status.
        /// </summary>
        /// <param name="housecode">Housecode.</param>
        /// <param name="unitcode">Unitcode.</param>
        public void StatusRequest(X10HouseCode housecode, X10UnitCode unitcode)
        {
            lock (commandLock)
            {
                //string hcunit = String.Format("{0:X}{1:X}", (int)housecode, (int)unitcode);
                string hcfuntion = String.Format("{0:x1}{1:x1}", (int)housecode, (int)X10Command.Status_Request);
                SendModuleAddress(housecode, unitcode);
                SendMessage(new byte[] {
                    (int)X10CommandType.Function,
                    byte.Parse(hcfuntion, System.Globalization.NumberStyles.HexNumber)
                });
            }
        }

        #endregion

        #endregion

        #region Private Members

        #region X10 Interface Commands

        private void SendModuleAddress(X10HouseCode housecode, X10UnitCode unitcode)
        {
            // TODO: do more tests about this optimization (and comment out the "if" if tests are succesfully)
            //if (!addressedModules.Contains(mod) || addressedModules.Count > 1) // optimization disabled, uncomment to enable
            {
                UnselectModules();
                SelectModule(Utility.HouseUnitCodeFromEnum(housecode, unitcode));
                string hcUnit = String.Format("{0:X}{1:X}", (int)housecode, (int)unitcode);
                SendMessage(new byte[] {
                    (int)X10CommandType.Address,
                    byte.Parse(hcUnit, System.Globalization.NumberStyles.HexNumber)
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

            if (x10interface.GetType().Equals(typeof(CM15)))
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
                byte[] trcommand = CM15.BuildTransceivedCodesMessage(monitoredHouseCode);
                SendMessage(trcommand);
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

        private void CommandEvent_AllUnitsOff(string housecode)
        {
            UnselectModules();
            // TODO: select only light modules 
            foreach (KeyValuePair<string, X10Module> modkv in modules)
            {
                if (modkv.Value.Code.StartsWith(housecode))
                {
                    modkv.Value.Level = 0.0;
                }
            }
        }

        private void CommandEvent_AllLightsOn(string housecode)
        {
            UnselectModules();
            // TODO: pick only light modules 
            foreach (KeyValuePair<string, X10Module> modkv in modules)
            {
                if (modkv.Value.Code.StartsWith(housecode))
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
            if (ModuleChanged != null)
            {
                try
                {
                    ModuleChanged(sender, args);
                }
                catch (Exception e)
                { 
                    logger.Error(e);
                }
            }
        }

        #endregion

        #region X10 Interface I/O operations

        private bool Open()
        {
            Close();
            bool success = (x10interface != null && x10interface.Open());
            if (success)
            {
                if (x10interface.GetType().Equals(typeof(CM15)))
                {
                    // Set transceived house codes for CM15 X10 RF-->PLC
                    InitializeCm15();
                }
                // Start the Reader task
                readerTokenSource = new CancellationTokenSource();
                readerTask = Task.Factory.StartNew(() => ReaderTask(readerTokenSource.Token), readerTokenSource.Token);
            }
            return success;
        }

        private void Close()
        {
            // Stop the Reader task
            if (readerTask != null)
            {
                readerTokenSource.Cancel();
                readerTask.Wait(5000);
                readerTask.Dispose();
                readerTask = null;
                readerTokenSource = null;
            }
            // Dispose the X10 interface
            try
            {
                x10interface.Close();
            }
            catch (Exception e)
            {
                logger.Error(e);
            }
            isInterfaceReady = false;
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
                        if (!x10interface.WriteData(message))
                        {
                            logger.Warn("Interface I/O error");
                        }

                        commandLastMessage = message;
                        waitAckTimestamp = DateTime.Now;

                        if (x10interface.GetType().Equals(typeof(CM11)))
                        {
                            expectedChecksum = (byte)((message[0] + message[1]) & 0xff);
                            communicationState = X10CommState.WaitingChecksum;
                        }
                        else
                        {
                            communicationState = X10CommState.WaitingAck;
                        }

                        while (commandResendAttempts < commandResendMax && communicationState != X10CommState.Ready)
                        {
                            var elapsedFromWaitAck = DateTime.Now - waitAckTimestamp;
                            while (elapsedFromWaitAck.TotalSeconds < commandTimeoutSeconds && communicationState != X10CommState.Ready)
                            {
                                Thread.Sleep(1);
                                elapsedFromWaitAck = DateTime.Now - waitAckTimestamp;
                            }
                            if (elapsedFromWaitAck.TotalSeconds >= commandTimeoutSeconds && communicationState != X10CommState.Ready)
                            {
                                // Resend last message
                                commandResendAttempts++;
                                logger.Warn("Previous command timed out, resending ({0})", commandResendAttempts);
                                if (!x10interface.WriteData(commandLastMessage))
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
                    if (!x10interface.WriteData(message))
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

        private void ReaderTask(CancellationToken readerToken)
        {
            while (!readerToken.IsCancellationRequested)
            {
                try
                {
                    byte[] readData = x10interface.ReadData();
                    if (readData.Length > 0)
                    {
                        logger.Debug(BitConverter.ToString(readData));
                        // last command ACK timeout
                        var elapsedFromWaitAck = DateTime.Now - waitAckTimestamp;
                        if (elapsedFromWaitAck.TotalSeconds >= commandTimeoutSeconds && communicationState != X10CommState.Ready)
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
                            UpdateInterfaceTime(false);
                            isInterfaceReady = true;
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
                            string message = BitConverter.ToString(readData);
                            logger.Debug("RFCOM: {0}", message);
                            // repeated messages check
                            if (lastRfMessage == message && (lastReceivedTs - lastRfReceivedTs).TotalMilliseconds < 200)
                            {
                                logger.Warn("RFCOM: Ignoring repeated message within 200ms");
                                continue;
                            }
                            lastRfMessage = message;
                            lastRfReceivedTs = lastReceivedTs;

                            if (RfDataReceived != null)
                            {
                                Thread signal = new Thread(() =>
                                {
                                    try
                                    {
                                        RfDataReceived(new RfDataReceivedAction() { RawData = readData });
                                    }
                                    catch (Exception e)
                                    { 
                                        logger.Error(e);
                                    }
                                });
                                signal.Start();
                            }

                            // Decode X10 RF Module Command (eg. "5D 20 70 8F 48 B7")
                            if (readData.Length == 6 && readData[1] == 0x20 && ((readData[3] & ~readData[2]) == readData[3] && (readData[5] & ~readData[4]) == readData[5]))
                            {
                                byte hu = readData[2]; // house code + 4th bit of unit code
                                byte hf = readData[4]; // unit code (3 bits) + function code
                                string houseCode = ((X10HouseCode)(Utility.ReverseByte((byte)(hu >> 4)) >> 4)).ToString();
                                switch (hf)
                                {
                                case 0x98: // DIM ONE STEP
                                    CommandEvent_Dim(0x0F);
                                    break;
                                case 0x88: // BRIGHT ONE STEP
                                    CommandEvent_Bright(0x0F);
                                    break;
                                case 0x90: // ALL LIGHTS ON
                                    if (houseCode != "")
                                        CommandEvent_AllLightsOn(houseCode);
                                    break;
                                case 0x80: // ALL LIGHTS OFF
                                    if (houseCode != "")
                                        CommandEvent_AllUnitsOff(houseCode);
                                    break;
                                default:
                                    string houseUnit = Convert.ToString(hu, 2).PadLeft(8, '0');
                                    string unitFunction = Convert.ToString(hf, 2).PadLeft(8, '0');
                                    string unitCode = (Convert.ToInt16(houseUnit.Substring(5, 1) + unitFunction.Substring(1, 1) + unitFunction.Substring(4, 1) + unitFunction.Substring(3, 1), 2) + 1).ToString();

                                    UnselectModules();
                                    SelectModule(houseCode + unitCode);

                                    if (unitFunction[2] == '1') // 1 = OFF, 0 = ON
                                    {
                                        CommandEvent_Off();
                                    }
                                    else
                                    {
                                        CommandEvent_On();
                                    }
                                    break;
                                }
                            }

                        }
                        else if ((readData[0] == (int)X10CommandType.PLC_Poll) && readData.Length <= 2)
                        {
                            isInterfaceReady = true;
                            SendMessage(new byte[] { (byte)X10CommandType.PLC_ReplyToPoll }); // reply to poll
                        }
                        else if ((readData[0] == (int)X10CommandType.PLC_FilterFail_Poll) && readData.Length <= 2)
                        {
                            isInterfaceReady = true;
                            SendMessage(new byte[] { (int)X10CommandType.PLC_FilterFail_Poll }); // reply to filter fail poll
                        }
                        else if ((readData[0] == (int)X10CommandType.PLC_Poll))
                        {
                            lastReceivedTs = DateTime.Now;
                            logger.Debug("PLCRX: {0}", BitConverter.ToString(readData));

                            if (readData.Length > 3)
                            {
                                int messageLength = readData[1];
                                if (readData.Length > messageLength - 2)
                                {
                                    char[] bitmapData = Convert.ToString(readData[2], 2).PadLeft(8, '0').ToCharArray();
                                    byte[] functionBitmap = new byte[messageLength - 1];
                                    for (int i = 0; i < functionBitmap.Length; i++)
                                    {
                                        functionBitmap[i] = byte.Parse(bitmapData[7 - i].ToString());
                                    }

                                    byte[] messageData = new byte[messageLength - 1];
                                    Array.Copy(readData, 3, messageData, 0, messageLength - 1);

                                    // CM15 Extended receive has got inverted data
                                    if (messageLength > 2 && x10interface.GetType().Equals(typeof(CM15)))
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
                                        }
                                        else if (functionBitmap[b] == (byte)X10FunctionType.Function) // function
                                        {
                                            string function = ((X10Command)Convert.ToInt16(messageData[b].ToString("X2").Substring(1, 1), 16)).ToString().ToUpper();
                                            string houseCode = ((X10HouseCode)Convert.ToInt16(messageData[b].ToString("X2").Substring(0, 1), 16)).ToString();
                                            //
                                            logger.Debug("      {0}) House code = {1}", b, houseCode);
                                            logger.Debug("      {0})    Command = {1}", b, function);
                                            //
                                            switch (function)
                                            {
                                            case "ALL_UNITS_OFF":
                                                if (houseCode != "")
                                                    CommandEvent_AllUnitsOff(houseCode);
                                                break;
                                            case "ALL_LIGHTS_ON":
                                                if (houseCode != "")
                                                    CommandEvent_AllLightsOn(houseCode);
                                                break;
                                            case "ON":
                                                CommandEvent_On();
                                                break;
                                            case "OFF":
                                                CommandEvent_Off();
                                                break;
                                            case "BRIGHT":
                                                CommandEvent_Bright(messageData[++b]);
                                                break;
                                            case "DIM":
                                                CommandEvent_Dim(messageData[++b]);
                                                break;
                                            }
                                            //
                                            newAddressData = true;
                                        }
                                    }
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
                                Close();
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

        private void ConnectionWatcherTask(CancellationToken watcherToken)
        {
            // This task takes care of automatically reconnecting the interface
            // when the connection is drop or if an I/O error occurs
            while (!watcherToken.IsCancellationRequested)
            {
                if (gotReadWriteError)
                {
                    isInterfaceReady = false;
                    try
                    {
                        UnselectModules();
                        Close();
                        // wait 3 secs before reconnecting
                        Thread.Sleep(3000);
                        if (!watcherToken.IsCancellationRequested)
                        {
                            try
                            {
                                gotReadWriteError = !Open();
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
                Thread.Sleep(1000);
            }
        }

        #endregion

        #endregion

    }

    /// <summary>
    /// Rf data received action.
    /// </summary>
    public class RfDataReceivedAction
    {
        /// <summary>
        /// The raw data.
        /// </summary>
        public byte[] RawData;
    }

}

