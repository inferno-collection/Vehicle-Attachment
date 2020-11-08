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
using System.Linq;
using CitizenFX.Core;
using Newtonsoft.Json;
using CitizenFX.Core.UI;
using CitizenFX.Core.Native;
using System.Threading.Tasks;
using System.Collections.Generic;
using InfernoCollection.VehicleAttachment.Client.Models;

namespace InfernoCollection.VehicleCollection.Client
{
    public class Main : BaseScript
    {
        #region Configuration Variables
        internal readonly static Vector3
            POSITION_VECTOR = new Vector3(0.0f, -2.0f, 1.5f),
            ROTATION_VECTOR = new Vector3(0.0f, 0.0f, 0.0f),
            RAYCAST_VECTOR = new Vector3(0.0f, 2.0f, 0.0f);

        internal const string
            CONFIG_FILE = "config.json",
            TOW_CONTROLS = "\nNUMPAD 8/5 (or Left Stick) = Forward/Backwards\n NUMPAD 4/6 (or Left Stick) = Left/Right\nNUMPAD +/- (or Left Stick) = Up/Down\nNUMPAD 7/9 (or Left Stick) = Rotate Left/Right\nHold Left Shift (or X)/Left Control (or A) = Speed Up/Slow Down\nEnter (or A) = Confirm Position";
        #endregion

        #region General Variables
        internal static CrossFrameControl
            _goFaster,
            _goSlower;

        internal static Config CONFIG = new Config();

        internal static List<int> _vehiclesInUse = new List<int>();

        internal static List<Towing> _attachments = new List<Towing>();

        internal static AttachmentStage _attachmentStage = AttachmentStage.None;
        #endregion

        #region Load configuration file and add chat suggestion
        public Main()
        {
            string ConfigFile = null;

            TriggerEvent("chat:addSuggestion", "/attach [help]", "Starts the process of attaching one vehicle to another.");
            TriggerEvent("chat:addSuggestion", "/detach [help]", "Starts the process of detaching one vehicle from another.");

            try
            {
                ConfigFile = API.LoadResourceFile("inferno-vehicle-attachment", CONFIG_FILE);
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Error loading configuration from file, could not load file contents. Reverting to default configuration values.");
                Debug.WriteLine(exception.ToString());
            }

            if (ConfigFile != null && ConfigFile != "")
            {
                try
                {
                    CONFIG = JsonConvert.DeserializeObject<Config>(ConfigFile);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine("Error loading configuration from file, contents are invalid. Reverting to default configuration values.");
                    Debug.WriteLine(exception.ToString());
                }
            }
            else
            {
                Debug.WriteLine("Loaded configuration file is empty, reverting to defaults.");
            }
        }
        #endregion

        #region Command Handlers
        /// <summary>
        /// Triggers event that starts the attaching process.
        /// Also handles the triggering of the canceling process, and showing the help information.
        /// </summary>
        /// <param name="args">Command arguments</param>
        [Command("attach")]
        internal void OnAttach(string[] args)
        {
            if (args?[0] == "help")
            {
                ShowTowControls();
                return;
            }
            else if (args?[0] == "cancel")
            {
                if (_attachmentStage != AttachmentStage.None)
                {
                    _attachmentStage = AttachmentStage.Cancel;

                    Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                }
                else
                {
                    Screen.ShowNotification("~r~You are not interacting with a vehicle right now!");
                }

                return;
            }

            OnNewAttachment();
        }

        /// <summary>
        /// Triggers event that starts the detaching process.
        /// Also handles the triggering of the canceling process, and showing the help information.
        /// </summary>
        /// <param name="args">Command arguments</param>
        [Command("detach")]
        internal void OnDetach(string[] args)
        {
            if (args?[0] == "help")
            {
                ShowTowControls();
                return;
            }
            else if (args?[0] == "cancel")
            {
                if (_attachmentStage != AttachmentStage.None)
                {
                    _attachmentStage = AttachmentStage.Cancel;

                    Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                }
                else
                {
                    Screen.ShowNotification("~r~You are not interacting with a vehicle right now!");
                }

                return;
            }

            OnRemoveLastAttachment();
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Updates the synced list of vehicles that are currently being interacted with.
        /// </summary>
        /// <param name="json">JSON to parse into list</param>
        [EventHandler("Inferno-Collection:Vehicle-Attachment:Sync")]
        internal void OnSync(string json) => _vehiclesInUse = new List<int>(JsonConvert.DeserializeObject<List<int>>(json));

        /// <summary>
        /// Starts the process of attaching a vehicle to another vehicle
        /// </summary>
        [EventHandler("Inferno-Collection:Vehicle-Attachment:NewAttachment")]
        internal void OnNewAttachment()
        {
            if (_attachmentStage != AttachmentStage.None)
            {
                Screen.ShowNotification("~r~You are already interacting with another vehicle!");
                return;
            }

            _attachmentStage = AttachmentStage.TowTruck;

            Game.PlaySound("TOGGLE_ON", "HUD_FRONTEND_DEFAULT_SOUNDSET");
            Screen.ShowNotification("~g~Select a towing vehicle to get started!");
        }

        /// <summary>
        /// Starts the process of detaching one vehicle from another vehicle
        /// </summary>
        [EventHandler("Inferno-Collection:Vehicle-Attachment:RemoveLastAttachment")]
        internal void OnRemoveLastAttachment()
        {

            if (_attachments.Count == 0)
            {
                Screen.ShowNotification("~r~You do not have any vehicle to detach!");
                return;
            }

            if (_attachmentStage != AttachmentStage.None)
            {
                Screen.ShowNotification("~r~You are already interacting with another vehicle!");
                return;
            }

            _attachmentStage = AttachmentStage.Predetach;

            Game.PlaySound("TOGGLE_ON", "HUD_FRONTEND_DEFAULT_SOUNDSET");
            Screen.ShowNotification("~g~Follow the instructions to deatch the vehicle.");
        }
        #endregion

        #region Tick Handlers
        /// <summary>
        /// Handles vehicle selection, attaching, detaching, and canceling
        /// </summary>
        [Tick]
        internal async Task OnTick()
        {
            switch (_attachmentStage)
            {
                #region No attachment
                case AttachmentStage.None:
                    await Delay(1000);
                    break;
                #endregion

                #region Selecting tow truck
                case AttachmentStage.TowTruck:
                    {
                        Vehicle towTruck = FindVehicle();

                        if (towTruck == null)
                        {
                            Screen.DisplayHelpTextThisFrame("No vehicle found!");
                            return;
                        }

                        if (_vehiclesInUse.Contains(towTruck.NetworkId))
                        {
                            Screen.DisplayHelpTextThisFrame($"Someone else is using the {towTruck.LocalizedName} right now.");
                            return;
                        }

                        if (
                            (!CONFIG.BlacklistToWhitelist && CONFIG.AttachmentBlacklist.Contains(towTruck.Model)) ||
                            (CONFIG.BlacklistToWhitelist && !CONFIG.AttachmentBlacklist.Contains(towTruck.Model))
                        )
                        {
                            Screen.DisplayHelpTextThisFrame($"The {towTruck.LocalizedName} cannot be used as a tow vehicle!");
                            return;
                        }

                        if (CONFIG.EnableLine)
                        {
                            World.DrawLine(
                                Game.PlayerPed.Position, towTruck.Position,
                                System.Drawing.Color.FromArgb(255, 0, 255, 0)
                            );
                        }

                        Screen.DisplayHelpTextThisFrame($"Press ~INPUT_FRONTEND_ACCEPT~ to use the {towTruck.LocalizedName} as the towing vehicle.");

                        if (!Game.IsControlJustPressed(0, Control.FrontendAccept)) return;

                        Game.PlaySound("OK", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                        Screen.ShowNotification($"~g~{towTruck.LocalizedName} confirmed as towing vehicle! Now select a vehicle to be towed.");

                        _attachmentStage = AttachmentStage.VehicleToBeTowed;
                        _attachments.Add(new Towing(){ TowVehicle = towTruck.NetworkId });

                        TriggerServerEvent("Inferno-Collection:Vehicle-Attachment:AddInUseVehicle", towTruck.NetworkId);

                        await Delay(1000);
                    }
                    break;
                #endregion

                #region Selecting vehicle to be towed
                case AttachmentStage.VehicleToBeTowed:
                    {
                        Vehicle
                            vehicleToBeTowed = FindVehicle(),
                            towVehicle = (Vehicle)Entity.FromNetworkId(_attachments.Last().TowVehicle);

                        if (vehicleToBeTowed == null)
                        {
                            Screen.DisplayHelpTextThisFrame("No vehicle that be can towed found!");
                            return;
                        }

                        if (
                            _vehiclesInUse.Contains(vehicleToBeTowed.NetworkId) ||
                            // If vehicle is already attached to another vehicle
                            _attachments.Any(i => i.VehicleBeingTowed == vehicleToBeTowed.NetworkId)
                        )
                        {
                            Screen.DisplayHelpTextThisFrame($"The {vehicleToBeTowed.LocalizedName} is already in use.");
                            break;
                        }

                        if (
                            (!CONFIG.BlacklistToWhitelist && CONFIG.AttachmentBlacklist.Contains(vehicleToBeTowed.Model))
                            ||
                            (
                                CONFIG.BlacklistToWhitelist && CONFIG.WhitelistForTowedVehicles &&
                                !CONFIG.AttachmentBlacklist.Contains(vehicleToBeTowed.Model)
                            )
                        )
                        {
                            Screen.DisplayHelpTextThisFrame($"The {vehicleToBeTowed.LocalizedName} cannot be towed!");
                            return;
                        }

                        if (vehicleToBeTowed.Occupants.Length > 0)
                        {
                            Screen.DisplayHelpTextThisFrame($"The {vehicleToBeTowed.LocalizedName} is occupied!");
                            return;
                        }

                        if (
                            Entity.Exists(towVehicle) &&
                            vehicleToBeTowed.Position.DistanceToSquared2D(towVehicle.Position) > CONFIG.MaxDistanceFromTowVehicle
                        )
                        {
                            Screen.DisplayHelpTextThisFrame($"The {vehicleToBeTowed.LocalizedName} is too far from the {towVehicle.LocalizedName}!");
                            return;
                        }

                        if (CONFIG.EnableLine)
                        {
                            World.DrawLine(
                                Game.PlayerPed.Position, vehicleToBeTowed.Position,
                                System.Drawing.Color.FromArgb(255, 0, 255, 0)
                            );
                        }

                        Screen.DisplayHelpTextThisFrame($"Press ~INPUT_FRONTEND_ACCEPT~ to tow the {vehicleToBeTowed.LocalizedName}.");

                        if (!Game.IsControlJustPressed(0, Control.FrontendAccept)) return;

                        if (!Entity.Exists(towVehicle))
                        {
                            _attachmentStage = AttachmentStage.Cancel;

                            Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                            Screen.ShowNotification("~r~Tow vehicle deleted, cannot attach to nothing!");
                            break;
                        }

                        _attachmentStage = AttachmentStage.Position;
                        _attachments.Last().VehicleBeingTowed = vehicleToBeTowed.NetworkId;

                        TriggerServerEvent("Inferno-Collection:Vehicle-Attachment:AddInUseVehicle", vehicleToBeTowed.NetworkId);

                        int timeout = 4;
                        API.NetworkRequestControlOfNetworkId(vehicleToBeTowed.NetworkId);
                        while (
                            timeout > 0 &&
                            !API.NetworkHasControlOfNetworkId(vehicleToBeTowed.NetworkId)
                        )
                        {
                            await Delay(250);
                            API.NetworkRequestControlOfNetworkId(vehicleToBeTowed.NetworkId);

                            timeout--;
                        }

                        if (!API.NetworkHasControlOfNetworkId(vehicleToBeTowed.NetworkId))
                        {
                            Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                            Screen.ShowNotification($"~r~Could not tow the {vehicleToBeTowed.LocalizedName}.", true);

                            Debug.WriteLine($"Unable to tow {vehicleToBeTowed.LocalizedName} ({vehicleToBeTowed.NetworkId}); ownership of the vehicle could not be requested!");

                            _attachmentStage = AttachmentStage.Cancel;

                            break;
                        }

                        Game.PlaySound("OK", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                        Screen.ShowNotification($"~g~{vehicleToBeTowed.LocalizedName} confirmed as vehicle to be towed! Follow instructions to position vehicle.");

                        ShowTowControls();

                        vehicleToBeTowed.Opacity = 225;
                        vehicleToBeTowed.IsPersistent = true;
                        vehicleToBeTowed.IsPositionFrozen = true;
                        vehicleToBeTowed.IsCollisionEnabled = false;
                        vehicleToBeTowed.LockStatus = VehicleLockStatus.CannotBeTriedToEnter;
                        vehicleToBeTowed.AttachTo(towVehicle, POSITION_VECTOR, ROTATION_VECTOR);

                        _attachments.Last().AttachmentPosition = POSITION_VECTOR;
                        _attachments.Last().AttachmentRotation = ROTATION_VECTOR;

                        await Delay(1000);
                    }
                    break;
                #endregion

                #region Remove attached vehicle
                case AttachmentStage.Predetach:
                    {
                        ShowTowControls();

                        Entity vehicleBeingTowed = Entity.FromNetworkId(_attachments.Last().VehicleBeingTowed);

                        if (!Entity.Exists(vehicleBeingTowed))
                        {
                            _attachmentStage = AttachmentStage.Cancel;

                            Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                            Screen.ShowNotification("~r~Vehicle being towed deleted!");                           
                            break;
                        }

                        vehicleBeingTowed.Opacity = 225;
                        _attachmentStage = AttachmentStage.Detach;
                    }
                    break;
                #endregion

                #region Cancel current attachments
                case AttachmentStage.Cancel:
                    {
                        Entity vehicleBeingTowed = Entity.FromNetworkId(_attachments.Last().VehicleBeingTowed);

                        if (Entity.Exists(vehicleBeingTowed))
                        {
                            ResetTowedVehicle(vehicleBeingTowed);
                        }

                        _attachmentStage = AttachmentStage.None;

                        // Even though the vehicles may no longer exist, we still need to clear the Network IDs in case they get reused
                        TriggerServerEvent("Inferno-Collection:Vehicle-Attachment:RemoveInUseVehicle", _attachments.Last().TowVehicle);
                        TriggerServerEvent("Inferno-Collection:Vehicle-Attachment:RemoveInUseVehicle", _attachments.Last().VehicleBeingTowed);

                        _attachments.RemoveAll(
                            i =>
                                i.TowVehicle == _attachments.Last().TowVehicle &&
                                i.VehicleBeingTowed == _attachments.Last().VehicleBeingTowed
                        );                       
                    }
                    break;
                #endregion

                #region Adjust vehicle position
                default:
                    {
                        float changeAmount = CONFIG.ChangeAmount;

                        Vector3
                            position = _attachments.Last().AttachmentPosition,
                            rotation = _attachments.Last().AttachmentRotation;

                        bool
                            fast = Game.IsControlPressed(0, Control.Sprint),
                            slow = Game.IsControlPressed(0, Control.VehicleSubDescend);

                        Vehicle
                            towVehicle = (Vehicle)Entity.FromNetworkId(_attachments.Last().TowVehicle),
                            vehicleBeingTowed = (Vehicle)Entity.FromNetworkId(_attachments.Last().VehicleBeingTowed);

                        // Because FiveM only takes one input at a time, this is how we
                        // check if Shift or Ctrl are being held as well as another key
                        if (fast)
                        {
                            _goFaster = CrossFrameControl.True;
                        }
                        else if (!fast && _goFaster == CrossFrameControl.True)
                        {
                            _goFaster = CrossFrameControl.FalseNextFrame;
                        }
                        else if (_goFaster == CrossFrameControl.FalseNextFrame)
                        {
                            _goFaster = CrossFrameControl.False;
                        }

                        if (!fast && slow)
                        {
                            _goSlower = CrossFrameControl.True;
                        }
                        else if (!slow && _goSlower == CrossFrameControl.True)
                        {
                            _goSlower = CrossFrameControl.FalseNextFrame;
                        }
                        else if (_goSlower == CrossFrameControl.FalseNextFrame)
                        {
                            _goSlower = CrossFrameControl.False;
                        }

                        if (_goFaster != CrossFrameControl.False)
                        {
                            changeAmount += CONFIG.FasterAmount;
                        }

                        if (_goSlower != CrossFrameControl.False)
                        {
                            changeAmount += CONFIG.SlowerAmount;
                        }

                        // Gets new position based of old position +/- increase amount
                        // NUMPAD 8
                        if (Game.IsControlJustPressed(0, Control.VehicleFlyPitchUpOnly))
                        {
                            position.Y += changeAmount;
                        }
                        // NUMPAD 5
                        else if (Game.IsControlJustPressed(0, Control.VehicleFlyPitchDownOnly))
                        {
                            position.Y -= changeAmount;
                        }
                        // NUMPAD 4
                        else if (Game.IsControlJustPressed(0, Control.VehicleFlyRollLeftOnly))
                        {
                            position.X -= changeAmount;
                        }
                        // NUMPAD 6
                        else if (Game.IsControlJustPressed(0, Control.VehicleFlyRollRightOnly))
                        {
                            position.X += changeAmount;
                        }
                        // NUMPAD +
                        else if (Game.IsControlJustPressed(0, Control.ReplayFOVIncrease))
                        {
                            position.Z += changeAmount;
                        }
                        // NUMPAD -
                        else if (Game.IsControlJustPressed(0, Control.ReplayFOVDecrease))
                        {
                            position.Z -= changeAmount;
                        }

                        // Gets new rotation based of old position +/- increase amount
                        // NUMPAD 7
                        else if (Game.IsControlJustPressed(0, Control.VehicleFlySelectTargetLeft))
                        {
                            rotation.Z += changeAmount * 10; 
                        }
                        // NUMPAD 9
                        else if (Game.IsControlJustPressed(0, Control.VehicleFlySelectTargetRight))
                        {
                            rotation.Z -= changeAmount * 10; 
                        }

                        if (!Entity.Exists(towVehicle) || !Entity.Exists(vehicleBeingTowed))
                        {
                            Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                            Screen.ShowNotification("~g~Attachment canceled.");

                            _attachmentStage = AttachmentStage.Cancel;

                            return;
                        }

                        // Confirms placement
                        // NUMPAD Enter
                        else if (Game.IsControlJustPressed(0, Control.FrontendAccept)) 
                        {
                            if (_attachmentStage == AttachmentStage.Position)
                            {
                                Screen.ShowNotification("~g~Attachment complete! Drive safe.");

                                vehicleBeingTowed.ResetOpacity();
                                vehicleBeingTowed.IsCollisionEnabled = true;
                            }
                            else if (_attachmentStage == AttachmentStage.Detach)
                            {
                                Screen.ShowNotification($"~g~{vehicleBeingTowed.LocalizedName} deatached!");

                                ResetTowedVehicle(vehicleBeingTowed);

                                _attachments.RemoveAll(
                                    i =>
                                        i.TowVehicle == _attachments.Last().TowVehicle &&
                                        i.VehicleBeingTowed == _attachments.Last().VehicleBeingTowed
                                );
                            }

                            Game.PlaySound("WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET");

                            _attachmentStage = AttachmentStage.None;

                            TriggerServerEvent("Inferno-Collection:Vehicle-Attachment:RemoveInUseVehicle", _attachments.Last().TowVehicle);
                            TriggerServerEvent("Inferno-Collection:Vehicle-Attachment:RemoveInUseVehicle", _attachments.Last().VehicleBeingTowed);

                            return;
                        }

                        // Checks for new position or rotation, so attching is not done every frame
                        if (position != _attachments.Last().AttachmentPosition || rotation != _attachments.Last().AttachmentRotation)
                        {
                            if (towVehicle.Position.DistanceToSquared2D(
                                towVehicle.GetOffsetPosition(position)
                            ) > CONFIG.MaxDistanceFromTowVehicle)
                            {
                                Screen.ShowNotification("~r~Cannot move there, too far from tow vehicle!", true);
                                return;
                            }

                            vehicleBeingTowed.AttachTo(towVehicle, position, rotation);

                            // Store current position so we can reference it later
                            _attachments.Last().AttachmentPosition = position;
                            _attachments.Last().AttachmentRotation = rotation;
                        }
                    }
                    break;
                #endregion
            }
        }
        #endregion

        #region Functions
        /// <summary>
        /// Returns the vehicle infront of the player
        /// </summary>
        /// <returns>Vehicle infront of player</returns>
        internal Vehicle FindVehicle()
        {
            RaycastResult raycast = World.RaycastCapsule(
                Game.PlayerPed.Position,
                Game.PlayerPed.GetOffsetPosition(RAYCAST_VECTOR),
                0.3f, (IntersectOptions)10, Game.PlayerPed
            );

            if (
                !raycast.DitHitEntity ||
                !Entity.Exists(raycast.HitEntity) ||
                !raycast.HitEntity.Model.IsVehicle
            ) return null;

            return (Vehicle)raycast.HitEntity;
        }

        /// <summary>
        /// Properly detaches and resets a vehicle that is attached to another vehicle
        /// </summary>
        /// <param name="entity">Vehicle to reset in entity form</param>
        internal async void ResetTowedVehicle(Entity entity)
        {
            Vector3 position;
            Vehicle vehicle = (Vehicle)entity;

            vehicle.Opacity = 0;
            vehicle.Detach();
            
            position = vehicle.Position;

            vehicle.PlaceOnGround();
            vehicle.IsCollisionEnabled = true;
            vehicle.IsPositionFrozen = false;

            await Delay(1000);

            vehicle.Position = position;
            vehicle.ResetOpacity();
            vehicle.LockStatus = VehicleLockStatus.Unlocked;
            vehicle.ApplyForce(new Vector3(0.0f, 0.0f, 0.001f));
        }

        /// <summary>
        /// Prints the tow controls to the chat box
        /// </summary>
        internal void ShowTowControls()
        {
            if (CONFIG.EnableChatMessage)
            {
                TriggerEvent("chatMessage", "Tow", new[] { 0, 255, 0 }, TOW_CONTROLS);
            }
        }
        #endregion
    }
}