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
        internal static Config CONFIG = new Config();

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
        internal static AttachmentStage _currentAttachment;

        internal static HashSet<int> _vehiclesInUse = new HashSet<int>();

        internal static CrossFrameControl
            _goFaster,
            _goSlower;       

        internal static Vehicle
            _towVehicle,
            _vehicleBeingTowed;

        internal static Vector3
            _attachPosition,
            _attachRotation;
        #endregion

        #region Load configuration file and add chat suggestion
        public Main()
        {
            string ConfigFile = null;

            TriggerEvent("chat:addSuggestion", "/attachment", "Attach/detach one vehicle from another");

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
        [Command("attachment")]
        internal void OnAttachment()
        {

            Game.PlaySound("TOGGLE_ON", "HUD_FRONTEND_DEFAULT_SOUNDSET");

            if (_currentAttachment != AttachmentStage.None)
            {
                _currentAttachment = AttachmentStage.Cancel;

                return;
            }

            if (Entity.Exists(_vehicleBeingTowed))
                _currentAttachment = AttachmentStage.Predetach;
            else
                _currentAttachment = AttachmentStage.TowTruck;
        }
        #endregion

        #region Event Handlers
        [EventHandler("Vehicle-Attachment:Sync")]
        internal void OnSync(string json) =>
            _vehiclesInUse = new HashSet<int>(JsonConvert.DeserializeObject<HashSet<int>>(json));
        #endregion

        #region Tick Handlers
        [Tick]
        internal async Task OnTick()
        {
            switch (_currentAttachment)
            {
                #region No attachments
                case AttachmentStage.None:
                    await Delay(1000);

                    break;
                #endregion

                #region Selecting tow truck
                case AttachmentStage.TowTruck:
                    Vehicle towTruck = FindVehicle(true);

                    if (towTruck == null)
                    {
                        Screen.DisplayHelpTextThisFrame("No suitable towing vehicle found!");

                        break;
                    }

                    if (_vehiclesInUse.Contains(towTruck.NetworkId))
                    {
                        Screen.DisplayHelpTextThisFrame($"The {towTruck.LocalizedName} is already in use.");

                        break;
                    }

                    if (CONFIG.EnableLine)
                        World.DrawLine(
                            Game.PlayerPed.Position, towTruck.Position,
                            System.Drawing.Color.FromArgb(255, 0, 255, 0)
                        );

                    Screen.DisplayHelpTextThisFrame($"Press ~INPUT_FRONTEND_ACCEPT~ to use the {towTruck.LocalizedName} as the towing vehicle.");

                    if (!Game.IsControlJustPressed(0, Control.FrontendAccept)) break;

                    Screen.ShowNotification($"~g~{towTruck.LocalizedName} confirmed as towing vehicle!");
                    Game.PlaySound("OK", "HUD_FRONTEND_DEFAULT_SOUNDSET");

                    _towVehicle = towTruck;
                    _currentAttachment = AttachmentStage.VehicleToBeTowed;

                    TriggerServerEvent("Vehicle-Attachment:AddVehicle", _towVehicle.NetworkId);

                    await Delay(1000);

                    break;
                #endregion

                #region Selecting vehicle to be towed
                case AttachmentStage.VehicleToBeTowed:
                    Vehicle vehicleToBeTowed = FindVehicle(false);

                    if (vehicleToBeTowed == null)
                    {
                        Screen.DisplayHelpTextThisFrame("No vehicle that be towed found!");

                        break;
                    }

                    if (vehicleToBeTowed.Occupants.Length > 0)
                    {
                        Screen.DisplayHelpTextThisFrame($"The {vehicleToBeTowed.LocalizedName} is occupied!");

                        break;
                    }

                    if (_vehiclesInUse.Contains(vehicleToBeTowed.NetworkId))
                    {
                        Screen.DisplayHelpTextThisFrame($"The {vehicleToBeTowed.LocalizedName} is already in use.");

                        break;
                    }

                    if (CONFIG.EnableLine)
                        World.DrawLine(
                            Game.PlayerPed.Position, vehicleToBeTowed.Position,
                            System.Drawing.Color.FromArgb(255, 0, 255, 0)
                        );

                    Screen.DisplayHelpTextThisFrame($"Press ~INPUT_FRONTEND_ACCEPT~ to tow the {vehicleToBeTowed.LocalizedName}.");

                    if (!Game.IsControlJustPressed(0, Control.FrontendAccept)) break;

                    _vehicleBeingTowed = vehicleToBeTowed;
                    _currentAttachment = AttachmentStage.Position;
                    
                    TriggerServerEvent("Vehicle-Attachment:AddVehicle", _towVehicle.NetworkId);

                    int timeout = 4;
                    API.NetworkRequestControlOfNetworkId(_vehicleBeingTowed.NetworkId);
                    while (
                        timeout > 0 &&
                        !API.NetworkHasControlOfNetworkId(_vehicleBeingTowed.NetworkId)
                    )
                    {
                        await Delay(250);

                        timeout--;
                    }

                    if (!API.NetworkHasControlOfNetworkId(_vehicleBeingTowed.NetworkId))
                    {
                        Screen.ShowNotification($"~r~Could not tow the{_vehicleBeingTowed.LocalizedName}.", true);

                        Debug.WriteLine($"Unable to tow {_vehicleBeingTowed.LocalizedName} ({_vehicleBeingTowed.NetworkId}); ownership of the vehicle could not be requested!");

                        _currentAttachment = AttachmentStage.Cancel;

                        break;
                    }

                    Screen.ShowNotification($"~g~{_vehicleBeingTowed.LocalizedName} confirmed as vehicle to be towed! Follow instructions to position vehicle.");
                    Game.PlaySound("OK", "HUD_FRONTEND_DEFAULT_SOUNDSET");

                    ShowTowControls();

                    _vehicleBeingTowed.Opacity = 225;
                    _vehicleBeingTowed.IsPersistent = true;
                    _vehicleBeingTowed.IsPositionFrozen = true;
                    _vehicleBeingTowed.IsCollisionEnabled = false;
                    _vehicleBeingTowed.LockStatus = VehicleLockStatus.CannotBeTriedToEnter;
                    _vehicleBeingTowed.AttachTo(_towVehicle, POSITION_VECTOR, ROTATION_VECTOR);

                    _attachPosition = POSITION_VECTOR;
                    _attachRotation = ROTATION_VECTOR;

                    await Delay(1000);

                    break;
                #endregion

                #region Remove attached vehicle
                case AttachmentStage.Predetach:
                    ShowTowControls();

                    _vehicleBeingTowed.Opacity = 225;
                    _currentAttachment = AttachmentStage.detach;

                    break;
                #endregion

                #region Cancel current attachments
                case AttachmentStage.Cancel:
                    if (Entity.Exists(_towVehicle))
                        TriggerServerEvent("Vehicle-Attachment:RemoveVehicle", _towVehicle.NetworkId);

                    if (Entity.Exists(_vehicleBeingTowed))
                    {
                        TriggerServerEvent("Vehicle-Attachment:RemoveVehicle", _vehicleBeingTowed.NetworkId);

                        ResetTowedVehicle(_vehicleBeingTowed);
                    }

                    _towVehicle = null;
                    _vehicleBeingTowed = null;
                    _currentAttachment = AttachmentStage.None;

                    break;
                #endregion

                #region Position vehicle on tow truck
                default:
                    float changeAmount = CONFIG.ChangeAmount;

                    Vector3
                        position = _attachPosition,
                        rotation = _attachRotation;

                    bool
                        fast = Game.IsControlPressed(0, Control.Sprint),
                        slow = Game.IsControlPressed(0, Control.VehicleSubDescend);

                    // Because FiveM only takes one input at a time, this is how we
                    // check if Shift or Ctrl are being held as well as another key
                    if (fast)
                        _goFaster = CrossFrameControl.True;
                    else if (!fast && _goFaster == CrossFrameControl.True)
                        _goFaster = CrossFrameControl.FalseNextFrame;
                    else if (_goFaster == CrossFrameControl.FalseNextFrame)
                        _goFaster = CrossFrameControl.False;

                    if (!fast && slow)
                        _goSlower = CrossFrameControl.True;
                    else if (!slow && _goSlower == CrossFrameControl.True)
                        _goSlower = CrossFrameControl.FalseNextFrame;
                    else if (_goSlower == CrossFrameControl.FalseNextFrame)
                        _goSlower = CrossFrameControl.False;

                    if (_goFaster != CrossFrameControl.False) changeAmount += CONFIG.FasterAmount;
                    if (_goSlower != CrossFrameControl.False) changeAmount += CONFIG.SlowerAmount;

                    if (!Entity.Exists(_towVehicle) || !Entity.Exists(_vehicleBeingTowed))
                    {
                        Screen.ShowNotification("~g~Attachment canceled.");
                        Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");

                        _currentAttachment = AttachmentStage.Cancel;

                        break;
                    }
                    
                    // Gets new position based of old position +/- increase amount
                    if (Game.IsControlJustPressed(0, Control.VehicleFlyPitchUpOnly))
                        position.Y += changeAmount; // NUMPAD 8
                    else if (Game.IsControlJustPressed(0, Control.VehicleFlyPitchDownOnly))
                        position.Y -= changeAmount; // NUMPAD 5
                    else if (Game.IsControlJustPressed(0, Control.VehicleFlyRollLeftOnly))
                        position.X -= changeAmount; // NUMPAD 4
                    else if (Game.IsControlJustPressed(0, Control.VehicleFlyRollRightOnly))
                        position.X += changeAmount; // NUMPAD 6
                    else if (Game.IsControlJustPressed(0, Control.ReplayFOVIncrease))
                        position.Z += changeAmount; // NUMPAD +
                    else if (Game.IsControlJustPressed(0, Control.ReplayFOVDecrease))
                        position.Z -= changeAmount; // NUMPAD -

                    // Gets new rotation based of old position +/- increase amount
                    else if (Game.IsControlJustPressed(0, Control.VehicleFlySelectTargetLeft))
                        rotation.Z += changeAmount * 10; // NUMPAD 7
                    else if (Game.IsControlJustPressed(0, Control.VehicleFlySelectTargetRight))
                        rotation.Z -= changeAmount * 10; // NUMPAD 9

                    else if (Game.IsControlJustPressed(0, Control.FrontendAccept)) // NUMPAD Enter
                    {
                        if (_currentAttachment == AttachmentStage.Position)
                        {
                            Screen.ShowNotification("~g~Attachment complete! Drive safe.");

                            _vehicleBeingTowed.ResetOpacity();
                            _vehicleBeingTowed.IsCollisionEnabled = true;
                        }
                        else if (_currentAttachment == AttachmentStage.detach)
                        {
                            Screen.ShowNotification($"~g~{_vehicleBeingTowed.LocalizedName} deatached!");

                            ResetTowedVehicle(_vehicleBeingTowed);

                            TriggerServerEvent("Vehicle-Attachment:RemoveVehicle", _towVehicle.NetworkId);
                            TriggerServerEvent("Vehicle-Attachment:RemoveVehicle", _vehicleBeingTowed.NetworkId);

                            _towVehicle = null;
                            _vehicleBeingTowed = null;
                        }

                        Game.PlaySound("WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET");

                        _currentAttachment = AttachmentStage.None;

                        break;
                    }

                    // Checks for new position or rotation, so attching is not done every frame
                    if (position != _attachPosition || rotation != _attachRotation)
                    {
                        _vehicleBeingTowed.AttachTo(_towVehicle, position, rotation);

                        // Store current position so we can reference it later
                        _attachPosition = position;
                        _attachRotation = rotation;
                    }

                    break;
                    #endregion
            }

            await Task.FromResult(0);
        }
        #endregion

        #region Functions
        internal Vehicle FindVehicle(bool towTruck)
        {
            RaycastResult raycast = World.RaycastCapsule(
                Game.PlayerPed.Position,
                Game.PlayerPed.GetOffsetPosition(RAYCAST_VECTOR),
                0.3f, (IntersectOptions)10, Game.PlayerPed
            );

            if (
                !raycast.DitHitEntity ||
                !Entity.Exists(raycast.HitEntity) ||
                !raycast.HitEntity.Model.IsVehicle ||
                (towTruck && !CONFIG.TowVehicles.Contains(raycast.HitEntity.Model))
            ) return null;

            return (Vehicle)raycast.HitEntity;
        }

        internal void ResetTowedVehicle(Vehicle vehicle)
        {
            vehicle.Detach();
            vehicle.Speed = 0.0f;
            vehicle.ResetOpacity();
            vehicle.IsPositionFrozen = false;
            vehicle.IsCollisionEnabled = true;
            vehicle.LockStatus = VehicleLockStatus.Unlocked;
            vehicle.ApplyForce(new Vector3(0.0f, 0.0f, 0.001f));
        }

        internal void ShowTowControls()
        {
            if (CONFIG.EnableChatMessage)
                TriggerEvent("chatMessage", "Tow", new[] { 0, 255, 0 }, TOW_CONTROLS);
        }
        #endregion
    }
}