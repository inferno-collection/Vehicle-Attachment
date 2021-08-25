/*
 * Inferno Collection Vehicle Attachment 1.5 Alpha
 * 
 * Copyright (c) 2019-2021, Christopher M, Inferno Collection. All rights reserved.
 * 
 * This project is licensed under the following:
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to use, copy, modify, and merge the software, under the following conditions:
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 * The software may not be sold in any format.
 * Modified copies of the software may only be shared in an uncompiled format.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using CitizenFX.Core;
using System.Collections.Generic;

namespace InfernoCollection.VehicleAttachment.Client.Models
{
    public class Config
    {
        public bool EnableLine { get; set; } = true;
        public bool EnableInstructions { get; set; } = true;

        public int InstructionDisplayTime { get; set; } = 15000;

        public IReadOnlyList<Model> AttachmentBlacklist { get; set; } = new List<Model>() { new Model("blimp") };

        public bool BlacklistToWhitelist { get; set; } = false;
        public bool WhitelistForTowedVehicles { get; set; } = false;
        public int MaxNumberOfAttachedVehicles { get; set; } = -1;
        public float MaxDistanceFromTowVehicle { get; set; } = 220.0f;
        public float ChangeAmount { get; set; } = 0.2f;
        public float FasterAmount { get; set; } = 2.0f;
        public float SlowerAmount { get; set; } = -0.15f;
    }
}