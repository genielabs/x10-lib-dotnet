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

#pragma warning disable 1591

using System;

namespace XTenLib
{
    
    public static class Utility
    {
        private const int X10_MAX_DIM = 22;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="percentage"></param>
        /// <returns></returns>
        public static byte GetDimValue(int percentage)
        {
            if (percentage > 100)
                percentage = 100;
            else if (percentage < 0)
                percentage = 0;
            percentage = (int)Math.Floor(((double)percentage / 100D) * X10_MAX_DIM);
            byte dimvalue = (byte)(percentage << 3);
            return dimvalue;
        }

        /// <summary>
        /// returns a value between 0.0 and 1.0 representing the percentage of dim
        /// </summary>
        /// <param name="dimvalue"></param>
        /// <returns></returns>
        public static double GetPercentageValue(byte dimvalue)
        {
            return Math.Round(((double)((dimvalue) >> 3) / (double)X10_MAX_DIM), 2);
        }

        public static string HouseUnitCodeFromEnum(X10HouseCode housecode, X10UnitCode unitcodes)
        {
            string unit = unitcodes.ToString();
            unit = unit.Substring(unit.LastIndexOf("_") + 1);
            //
            return housecode.ToString() + unit;
        }

        public static X10HouseCode HouseCodeFromString(string s)
        {
            var houseCode = X10HouseCode.A;
            s = s.Substring(0, 1).ToUpper();
            switch (s)
            {
            case "A":
                houseCode = X10HouseCode.A;
                break;
            case "B":
                houseCode = X10HouseCode.B;
                break;
            case "C":
                houseCode = X10HouseCode.C;
                break;
            case "D":
                houseCode = X10HouseCode.D;
                break;
            case "E":
                houseCode = X10HouseCode.E;
                break;
            case "F":
                houseCode = X10HouseCode.F;
                break;
            case "G":
                houseCode = X10HouseCode.G;
                break;
            case "H":
                houseCode = X10HouseCode.H;
                break;
            case "I":
                houseCode = X10HouseCode.I;
                break;
            case "J":
                houseCode = X10HouseCode.J;
                break;
            case "K":
                houseCode = X10HouseCode.K;
                break;
            case "L":
                houseCode = X10HouseCode.L;
                break;
            case "M":
                houseCode = X10HouseCode.M;
                break;
            case "N":
                houseCode = X10HouseCode.N;
                break;
            case "O":
                houseCode = X10HouseCode.O;
                break;
            case "P":
                houseCode = X10HouseCode.P;
                break;
            }
            return houseCode;
        }

        public static X10UnitCode UnitCodeFromString(string s)
        {
            var unitCode = X10UnitCode.Unit_1;
            s = s.Substring(1);
            switch (s)
            {
            case "1":
                unitCode = X10UnitCode.Unit_1;
                break;
            case "2":
                unitCode = X10UnitCode.Unit_2;
                break;
            case "3":
                unitCode = X10UnitCode.Unit_3;
                break;
            case "4":
                unitCode = X10UnitCode.Unit_4;
                break;
            case "5":
                unitCode = X10UnitCode.Unit_5;
                break;
            case "6":
                unitCode = X10UnitCode.Unit_6;
                break;
            case "7":
                unitCode = X10UnitCode.Unit_7;
                break;
            case "8":
                unitCode = X10UnitCode.Unit_8;
                break;
            case "9":
                unitCode = X10UnitCode.Unit_9;
                break;
            case "10":
                unitCode = X10UnitCode.Unit_10;
                break;
            case "11":
                unitCode = X10UnitCode.Unit_11;
                break;
            case "12":
                unitCode = X10UnitCode.Unit_12;
                break;
            case "13":
                unitCode = X10UnitCode.Unit_13;
                break;
            case "14":
                unitCode = X10UnitCode.Unit_14;
                break;
            case "15":
                unitCode = X10UnitCode.Unit_15;
                break;
            case "16":
                unitCode = X10UnitCode.Unit_16;
                break;
            }
            return unitCode;
        }

        public static byte ReverseByte(byte originalByte)
        {
            int result = 0;
            for (int i = 0; i < 8; i++)
            {
                result = result << 1;
                result += originalByte & 1;
                originalByte = (byte)(originalByte >> 1);
            }
            return (byte)result;
        }

    }
}

#pragma warning restore 1591
