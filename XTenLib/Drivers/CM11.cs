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
using System.IO.Ports;

namespace XTenLib.Drivers
{
    /// <summary>
    /// CM11 driver.
    /// </summary>
    public class CM11 : XTenInterface
    {
        const int BufferLength = 32;

        private SerialPort serialPort;
        private readonly string portName;

        /// <summary>
        /// Initializes a new instance of the <see cref="XTenLib.Drivers.CM11"/> class.
        /// </summary>
        /// <param name="port">Serial port path.</param>
        public CM11(string port)
        {
            portName = port;
        }

        /// <summary>
        /// Open the hardware interface.
        /// </summary>
        public bool Open()
        {
            bool success = false;
            try
            {
                bool tryOpen = (serialPort == null);
                if (Environment.OSVersion.Platform.ToString().StartsWith("Win") == false)
                {
                    tryOpen = (tryOpen && System.IO.File.Exists(portName));
                }
                if (tryOpen)
                {
                    serialPort = new SerialPort();
                    serialPort.PortName = portName;
                    serialPort.BaudRate = 4800;
                    serialPort.Parity = Parity.None;
                    serialPort.DataBits = 8;
                    serialPort.StopBits = StopBits.One;
                    serialPort.ReadTimeout = 150;
                    serialPort.WriteTimeout = 150;
                    //serialPort.RtsEnable = true;

                    // DataReceived event won't work under Linux / Mono
                    //serialPort.DataReceived += HandleDataReceived;
                    //serialPort.ErrorReceived += HandleErrorReceived;
                }
                if (serialPort.IsOpen == false)
                {
                    serialPort.Open();
                }
                // Send staus request on connection
                WriteData(new byte[] { 0x8B });
                success = true;
            }
            catch (Exception e)
            {
                XTenManager.logger.Error(e);
            }
            return success;
        }

        /// <summary>
        /// Close the hardware interface.
        /// </summary>
        public void Close()
        {
            if (serialPort != null)
            {
                //serialPort.DataReceived -= HandleDataReceived
                //serialPort.ErrorReceived -= HandleErrorReceived;
                try
                {
                    //serialPort.Dispose();
                    serialPort.Close();
                }
                catch (Exception e)
                {
                    XTenManager.logger.Error(e);
                }
                serialPort = null;
            }
        }

        /// <summary>
        /// Reads the data.
        /// </summary>
        /// <returns>The data.</returns>
        public byte[] ReadData()
        {
            int length = 0;
            int readBytes = 0;
            byte[] buffer = new byte[BufferLength];
            do
            {
                readBytes = serialPort.Read(buffer, length, BufferLength - length);
                length += readBytes;
                if (length > 1 && buffer[0] < length)
                    break;
                else if (buffer[0] > 0x10 && serialPort.BytesToRead == 0)
                    break;
            } while (readBytes > 0 && (BufferLength - length > 0));

            byte[] readData = new byte[length + 1];
            if (length > 1 && length < 13)
            {
                readData[0] = (int)X10CommandType.PLC_Poll;
                Array.Copy(buffer, 0, readData, 1, length);
            }
            else
            {
                Array.Copy(buffer, readData, length);
            }

            return readData;
        }

        /// <summary>
        /// Writes the data.
        /// </summary>
        /// <returns>true</returns>
        /// <c>false</c>
        /// <param name="bytesToSend">Bytes to send.</param>
        public bool WriteData(byte[] bytesToSend)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Write(bytesToSend, 0, bytesToSend.Length);
                return true;
            }
            return false;
        }

    }
}

