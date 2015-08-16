/*
    This file is part of XTenLib source code.

    HomeGenie is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    HomeGenie is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with HomeGenie.  If not, see <http://www.gnu.org/licenses/>.  
*/

/*
 *     Author: Generoso Martello <gene@homegenie.it>
 *     Project Homepage: https://github.com/genielabs/x10-lib-dotnet
 */

using System;

namespace XTenLib.Drivers
{
    /// <summary>
    /// X10 driver interface.
    /// </summary>
    public interface XTenInterface
    {
        /// <summary>
        /// Open the hardware interface.
        /// </summary>
        bool Open();

        /// <summary>
        /// Close the hardware interface.
        /// </summary>
        void Close();

        /// <summary>
        /// Reads the data.
        /// </summary>
        /// <returns>The data.</returns>
        byte[] ReadData();

        /// <summary>
        /// Writes the data.
        /// </summary>
        /// <returns><c>true</c>, if data was written, <c>false</c> otherwise.</returns>
        /// <param name="bytesToSend">Bytes to send.</param>
        bool WriteData(byte[] bytesToSend);
    }
}

