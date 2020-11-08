/*
 * Inferno Collection Vehicle Attachment 1.21 Alpha
 * 
 * Copyright (c) 2019-2020, Christopher M, Inferno Collection. All rights reserved.
 * 
 * This project is licensed under the following:
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to use, copy, modify, and merge the software, under the following conditions:
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 * The software may not be sold in any format.
 * Modified copies of the software may only be shared in an uncompiled format.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using System;
using CitizenFX.Core;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace InfernoCollection.VehicleCollection.Server
{
    public class Main : BaseScript
    {
        #region General Variables
        internal static List<int> _vehiclesInUse = new List<int>();
        #endregion

        #region Event Handlers
        [EventHandler("Inferno-Collection:Vehicle-Attachment:AddInUseVehicle")]
        internal void OnAddVehicle(int networkId)
        {
            if (!_vehiclesInUse.Contains(networkId)) _vehiclesInUse.Add(networkId);

            TriggerClientEvent("Inferno-Collection:Vehicle-Attachment:Sync", JsonConvert.SerializeObject(_vehiclesInUse));
        }

        [EventHandler("Inferno-Collection:Vehicle-Attachment:RemoveInUseVehicle")]
        internal void OnRemoveVehicle(int networkId)
        {
            if (_vehiclesInUse.Contains(networkId)) _vehiclesInUse.Remove(networkId);

            TriggerClientEvent("Inferno-Collection:Vehicle-Attachment:Sync", JsonConvert.SerializeObject(_vehiclesInUse));
        }
        #endregion
    }
}