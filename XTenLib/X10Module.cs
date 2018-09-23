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

#pragma warning disable 1591

using System;
using System.ComponentModel;

namespace XTenLib
{
    public class X10Module : INotifyPropertyChanged
    {
        
        #region Private fields

        private XTenManager x10;
        private X10HouseCode houseCode;
        private X10UnitCode unitCode;
        private double statusLevel;

        #endregion

        #region Public events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Instance management

        public X10Module(XTenManager x10manager, string code)
        {
            x10 = x10manager;
            Code = code;
            houseCode = Utility.HouseCodeFromString(code);
            unitCode = Utility.UnitCodeFromString(code);
            Level = 0.0;
            Description = "";
        }

        #endregion

        #region Public members

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        /// <value>The description.</value>
        public string Description { get; set; }

        /// <summary>
        /// Gets the House+Unit code of this module.
        /// </summary>
        /// <value>The code.</value>
        public string Code { get; }

        /// <summary>
        /// Gets the house code.
        /// </summary>
        /// <value>The house code.</value>
        public X10HouseCode HouseCode
        {
            get { return houseCode; }
        }

        /// <summary>
        /// Gets the unit code.
        /// </summary>
        /// <value>The unit code.</value>
        public X10UnitCode UnitCode
        {
            get { return unitCode; }
        }

        /// <summary>
        /// Turn On this module.
        /// </summary>
        public void On()
        {
            if (x10 != null)
            {
                x10.UnitOn(houseCode, unitCode);
            }
        }

        /// <summary>
        /// Turn Off this module.
        /// </summary>
        public void Off()
        {
            if (x10 != null)
            {
                x10.UnitOff(houseCode, unitCode);
            }
        }

        /// <summary>
        /// Dim the module by the specified percentage.
        /// </summary>
        /// <param name="percentage">Percentage.</param>
        public void Dim(int percentage = 5)
        {
            if (x10 != null)
            {
                x10.Dim(houseCode, unitCode, percentage);
            }
        }

        /// <summary>
        /// Brighten the module by the specified percentage.
        /// </summary>
        /// <param name="percentage">Percentage.</param>
        public void Bright(int percentage = 5)
        {
            if (x10 != null)
            {
                x10.Bright(houseCode, unitCode, percentage);
            }
        }

        /// <summary>
        /// Request the status of the module.
        /// </summary>
        public void GetStatus()
        {
            if (x10 != null)
            {
                x10.StatusRequest(houseCode, unitCode);
            }
        }

        /// <summary>
        /// Gets a value indicating whether this module is on.
        /// </summary>
        /// <value><c>true</c> if this module is on; otherwise, <c>false</c>.</value>
        public bool IsOn
        {
            get { return statusLevel != 0; }
        }

        /// <summary>
        /// Gets a value indicating whether this module is off.
        /// </summary>
        /// <value><c>true</c> if this module is off; otherwise, <c>false</c>.</value>
        public bool IsOff
        {
            get { return statusLevel == 0; }
        }

        /// <summary>
        /// Gets the dimmer level. This value ranges from 0.0 (0%) to 1.0 (100%).
        /// </summary>
        public double Level
        {
            get
            {
                return statusLevel; 
            }
            internal set
            {
                // This is used for the ComponentModel event implementation
                // Sets the level (0.0 to 1.0) and fire the PropertyChanged event.
                statusLevel = value;
                OnPropertyChanged("Level");
            }
        }

        #endregion

        #region Private members

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        #endregion

    }
}

#pragma warning restore 1591

