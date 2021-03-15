/*
 * Inferno Collection Vehicle Attachment 1.3 Beta
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
            TOW_CONTROLS =
                "~INPUT_F8DD5118~/~INPUT_2F20FA6E~ = Forward/Backwards" +
                "\n~INPUT_872241C1~/~INPUT_DEEBB52A~ = Left/Right" +
                "\n~INPUT_32D078AF~/~INPUT_7B7B256B~ = Up/Down" +
                "\n~INPUT_6DC8415B~/~INPUT_4EEC321F~ = Rotate Left/Right" +
                "\n~INPUT_83B8F159~/~INPUT_EE722E7A~ = Rotate Up/Down" +
                "\nHold ~INPUT_SPRINT~/~INPUT_DUCK~ = Speed Up/Slow Down" +
                "\n~INPUT_94172EE1~ = Confirm Position";
        #endregion

        #region General Variables
        internal static bool
            _goFaster,
            _goSlower;

        internal static Config CONFIG = new Config();

        internal static List<int> _vehiclesInUse = new List<int>();

        internal static List<Towing> _attachments = new List<Towing>();

        internal static AttachmentStage _attachmentStage = AttachmentStage.None;
        #endregion

        #region Constructor
        public Main()
        {
            TriggerEvent("chat:addSuggestion", "/attach [help|cancel]", "Starts the process of attaching one vehicle to another.");
            TriggerEvent("chat:addSuggestion", "/detach [help|cancel]", "Starts the process of detaching one vehicle from another.");

            #region Key Mapping
            API.RegisterKeyMapping("inferno-vehicle-attachment-forward", "Move attached vehicle forward.", "keyboard", "NUMPAD8"); // ~INPUT_F8DD5118~
            API.RegisterKeyMapping("inferno-vehicle-attachment-back", "Move attached vehicle back.", "keyboard", "NUMPAD5"); // ~INPUT_2F20FA6E~
            API.RegisterKeyMapping("inferno-vehicle-attachment-left", "Move attached vehicle left.", "keyboard", "NUMPAD4"); // ~INPUT_872241C1~
            API.RegisterKeyMapping("inferno-vehicle-attachment-right", "Move attached vehicle right.", "keyboard", "NUMPAD6"); // ~INPUT_DEEBB52A~
            API.RegisterKeyMapping("inferno-vehicle-attachment-up", "Move attached vehicle up.", "keyboard", "PAGEUP"); // ~INPUT_32D078AF~
            API.RegisterKeyMapping("inferno-vehicle-attachment-down", "Move attached vehicle down.", "keyboard", "PAGEDOWN"); // ~INPUT_7B7B256B~
            API.RegisterKeyMapping("inferno-vehicle-attachment-rotate-left", "Rotate attached vehicle left.", "keyboard", "NUMPAD7"); // ~INPUT_6DC8415B~
            API.RegisterKeyMapping("inferno-vehicle-attachment-rotate-right", "Rotate attached vehicle right.", "keyboard", "NUMPAD9"); /// ~INPUT_4EEC321F~
            API.RegisterKeyMapping("inferno-vehicle-attachment-rotate-up", "Rotate attached vehicle up.", "keyboard", "INSERT"); // ~INPUT_83B8F159~
            API.RegisterKeyMapping("inferno-vehicle-attachment-rotate-down", "Rotate attached vehicle down.", "keyboard", "DELETE"); // ~INPUT_EE722E7A~
            API.RegisterKeyMapping("inferno-vehicle-attachment-confirm", "Confirm attached vehicle.", "keyboard", "ENTER"); // ~INPUT_CAAAA4F4~
            API.RegisterKeyMapping("inferno-vehicle-attachment-confirm", "Confirm attached vehicle.", "keyboard", "NUMPADENTER"); // ~INPUT_CAAAA4F4~
            #endregion

            #region Load configuration file
            string ConfigFile = null;

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
            #endregion
        }
        #endregion

        #region Command Handlers
        #region Attach/detach
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

        #region Controls
        [Command("inferno-vehicle-attachment-forward")]
        internal void OnForward() => OnControl(AttachmentControl.Forward);

        [Command("inferno-vehicle-attachment-back")]
        internal void OnBack() => OnControl(AttachmentControl.Back);

        [Command("inferno-vehicle-attachment-left")]
        internal void OnLeft() => OnControl(AttachmentControl.Left);

        [Command("inferno-vehicle-attachment-right")]
        internal void OnRight() => OnControl(AttachmentControl.Right);

        [Command("inferno-vehicle-attachment-up")]
        internal void OnUp() => OnControl(AttachmentControl.Up);

        [Command("inferno-vehicle-attachment-down")]
        internal void OnDown() => OnControl(AttachmentControl.Down);

        [Command("inferno-vehicle-attachment-rotate-left")]
        internal void OnRotateLeft() => OnControl(AttachmentControl.RotateLeft);

        [Command("inferno-vehicle-attachment-rotate-right")]
        internal void OnRotateRight() => OnControl(AttachmentControl.RotateRight);

        [Command("inferno-vehicle-attachment-rotate-up")]
        internal void OnRotateUp() => OnControl(AttachmentControl.RotateUp);

        [Command("inferno-vehicle-attachment-rotate-down")]
        internal void OnRotateDown() => OnControl(AttachmentControl.RotateDown);

        [Command("inferno-vehicle-attachment-confirm")]
        internal void OnConfirm() => OnControl(AttachmentControl.Confirm);
        #endregion
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
            Screen.ShowNotification("~g~Follow the instructions to detach the vehicle.");
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
                        if (_attachments.Count > 0)
                        {
                            Entity vehicleBeingTowed = Entity.FromNetworkId(_attachments.Last().VehicleBeingTowed);

                            if (Entity.Exists(vehicleBeingTowed))
                            {
                                ResetTowedVehicle(vehicleBeingTowed);
                            }

                            // Even though the vehicles may no longer exist, we still need to clear the Network IDs in case they get reused
                            TriggerServerEvent("Inferno-Collection:Vehicle-Attachment:RemoveInUseVehicle", _attachments.Last().TowVehicle);
                            TriggerServerEvent("Inferno-Collection:Vehicle-Attachment:RemoveInUseVehicle", _attachments.Last().VehicleBeingTowed);

                            _attachments.RemoveAll(
                                i =>
                                    i.TowVehicle == _attachments.Last().TowVehicle &&
                                    i.VehicleBeingTowed == _attachments.Last().VehicleBeingTowed
                            );
                        }

                        _attachmentStage = AttachmentStage.None;                
                    }
                    break;
                #endregion

                default:
                    if (Game.IsControlPressed(0, Control.Sprint))
                    {
                        _goFaster = true;
                        _goSlower = false;
                        break;
                    }
                    else if (Game.IsControlPressed(0, Control.Duck))
                    {
                        _goFaster = false;
                        _goSlower = true;
                        break;
                    }

                    _goFaster = false;
                    _goSlower = false;
                    break;
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
            if (CONFIG.EnableInstructions)
            {
                API.BeginTextCommandDisplayHelp("CELL_EMAIL_BCON");

                foreach (string s in Screen.StringToArray(TOW_CONTROLS))
                {
                    API.AddTextComponentSubstringPlayerName(s);
                }

                API.EndTextCommandDisplayHelp(0, false, true, CONFIG.InstructionDisplayTime);
            }
        }

        internal void OnControl(AttachmentControl attachmentControl)
        {
            if (_attachmentStage != AttachmentStage.Position && _attachmentStage != AttachmentStage.Detach)
            {
                return;
            }

            float changeAmount = CONFIG.ChangeAmount;

            if (_goFaster)
            {
                changeAmount += CONFIG.FasterAmount;
            }
            else if (_goSlower)
            {
                changeAmount += CONFIG.SlowerAmount;
            }

            Vector3
                position = _attachments.Last().AttachmentPosition,
                rotation = _attachments.Last().AttachmentRotation;

            Vehicle
                towVehicle = (Vehicle)Entity.FromNetworkId(_attachments.Last().TowVehicle),
                vehicleBeingTowed = (Vehicle)Entity.FromNetworkId(_attachments.Last().VehicleBeingTowed);

            if (!Entity.Exists(towVehicle) || !Entity.Exists(vehicleBeingTowed))
            {
                Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                Screen.ShowNotification("~g~Attachment canceled.");

                _attachmentStage = AttachmentStage.Cancel;

                return;
            }

            switch (attachmentControl)
            {
                case AttachmentControl.Forward:
                    position.Y += changeAmount;
                    break;

                case AttachmentControl.Back:
                    position.Y -= changeAmount;
                    break;

                case AttachmentControl.Left:
                    position.X -= changeAmount;
                    break;

                case AttachmentControl.Right:
                    position.X += changeAmount;
                    break;

                case AttachmentControl.Up:
                    position.Z += changeAmount;
                    break;

                case AttachmentControl.Down:
                    position.Z -= changeAmount;
                    break;

                case AttachmentControl.RotateLeft:
                    rotation.Z += changeAmount * 10;
                    break;

                case AttachmentControl.RotateRight:
                    rotation.Z -= changeAmount * 10;
                    break;

                case AttachmentControl.RotateUp:
                    rotation.X += changeAmount * 10;
                    break;

                case AttachmentControl.RotateDown:
                    rotation.X -= changeAmount * 10;
                    break;

                case AttachmentControl.Confirm:
                    TriggerServerEvent("Inferno-Collection:Vehicle-Attachment:RemoveInUseVehicle", _attachments.Last().TowVehicle);
                    TriggerServerEvent("Inferno-Collection:Vehicle-Attachment:RemoveInUseVehicle", _attachments.Last().VehicleBeingTowed);

                    if (_attachmentStage == AttachmentStage.Position)
                    {
                        Screen.ShowNotification("~g~Attachment complete! Drive safe.");

                        vehicleBeingTowed.ResetOpacity();
                        vehicleBeingTowed.IsCollisionEnabled = true;
                    }
                    else if (_attachmentStage == AttachmentStage.Detach)
                    {
                        Screen.ShowNotification($"~g~{vehicleBeingTowed.LocalizedName} detached!");

                        ResetTowedVehicle(vehicleBeingTowed);

                        _attachments.RemoveAll(
                            i =>
                                i.TowVehicle == _attachments.Last().TowVehicle &&
                                i.VehicleBeingTowed == _attachments.Last().VehicleBeingTowed
                        );
                    }

                    Game.PlaySound("WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET");

                    _attachmentStage = AttachmentStage.None;

                    return;
            }

            if (towVehicle.Position.DistanceToSquared(towVehicle.GetOffsetPosition(position)) > CONFIG.MaxDistanceFromTowVehicle)
            {
                Screen.ShowNotification("~r~Cannot move there, too far from tow vehicle!", true);
                return;
            }

            vehicleBeingTowed.AttachTo(towVehicle, position, rotation);

            // Store current position so we can reference it later
            _attachments.Last().AttachmentPosition = position;
            _attachments.Last().AttachmentRotation = rotation;
        }
        #endregion
    }
}
