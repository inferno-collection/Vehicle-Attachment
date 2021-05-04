/*
 * Inferno Collection Vehicle Attachment 1.4 Alpha
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
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using CitizenFX.Core;
using CitizenFX.Core.UI;
using CitizenFX.Core.Native;
using InfernoCollection.VehicleAttachment.Client.Models;

namespace InfernoCollection.VehicleCollection.Client
{
    public class Main : BaseScript
    {
        #region Configuration Variables
        internal readonly Vector3
            POSITION_VECTOR = new Vector3(0.0f, -2.0f, 1.5f),
            ROTATION_VECTOR = new Vector3(0.0f, 0.0f, 0.0f),
            RAYCAST_VECTOR = new Vector3(0.0f, 2.0f, 0.0f);

        internal const string
            CONFIG_FILE_NAME = "config.json",
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
        internal bool
            _goFaster,
            _goSlower;

        internal Vehicle
            _tempTowVehicle,
            _tempVehicleBeingTowed;

        internal Config _config = new Config();       

        internal AttachmentStage _attachmentStage;
        internal AttachmentStage _previousAttachmentStage;
        #endregion

        #region Constructor
        public Main()
        {
            Game.PlayerPed.State.Set("oneSyncTest", "test", true);
            if (Game.PlayerPed.State.Get("oneSyncTest") == null)
            {
                throw new Exception("This resource requires at least OneSync \"legacy\". Use Public Beta Version 1.3 if you do not want to use OneSync.");
            }

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
            API.RegisterKeyMapping("inferno-vehicle-attachment-confirm", "Confirm attached vehicle.", "keyboard", "NUMPADENTER"); // ~INPUT_CAAAA4F4~
            #endregion

            #region Load configuration file
            string ConfigFile = null;

            try
            {
                ConfigFile = API.LoadResourceFile("inferno-vehicle-attachment", CONFIG_FILE_NAME);
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
                    _config = JsonConvert.DeserializeObject<Config>(ConfigFile);
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
            if (args.Count() > 0)
            {
                if (args[0] == "help")
                {
                    ShowTowControls();
                }
                else if (args[0] == "cancel")
                {
                    if (_attachmentStage != AttachmentStage.None)
                    {
                        _previousAttachmentStage = _attachmentStage;
                        _attachmentStage = AttachmentStage.Cancel;

                        Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                    }
                    else
                    {
                        Screen.ShowNotification("~r~You are not interacting with a vehicle right now!");
                    }
                }
            }
            else
            {
                OnNewAttachment();
            }
        }

        /// <summary>
        /// Triggers event that starts the detaching process.
        /// Also handles the triggering of the canceling process, and showing the help information.
        /// </summary>
        /// <param name="args">Command arguments</param>
        [Command("detach")]
        internal void OnDetach(string[] args)
        {
            if (args.Count() > 0)
            {
                if (args[0] == "help")
                {
                    ShowTowControls();
                }
                else if (args[0] == "cancel")
                {
                    if (_attachmentStage != AttachmentStage.None)
                    {
                        _previousAttachmentStage = _attachmentStage;
                        _attachmentStage = AttachmentStage.Cancel;

                        Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                    }
                    else
                    {
                        Screen.ShowNotification("~r~You are not interacting with a vehicle right now!");
                    }
                }
            }
            else
            {
                OnRemoveLastAttachment();
            }
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
        /// Starts the process of attaching a <see cref="Vehicle"/> to another <see cref="Vehicle"/>
        /// </summary>
        [EventHandler("Inferno-Collection:Vehicle-Attachment:NewAttachment")]
        internal void OnNewAttachment()
        {
            if (_attachmentStage != AttachmentStage.None)
            {
                Screen.ShowNotification("~r~You are already interacting with another vehicle!");
            }
            else
            {
                _attachmentStage = AttachmentStage.TowTruck;
                Tick += AttachmentTick;

                Game.PlaySound("TOGGLE_ON", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                Screen.ShowNotification("~g~Select a towing vehicle to get started!");
            }
        }

        /// <summary>
        /// Starts the process of detaching one <see cref="Vehicle"/> from <see cref="Vehicle"/> vehicle
        /// </summary>
        [EventHandler("Inferno-Collection:Vehicle-Attachment:RemoveLastAttachment")]
        internal void OnRemoveLastAttachment()
        {
            if (Entity.Exists(_tempTowVehicle) || Entity.Exists(_tempVehicleBeingTowed))
            {
                Screen.ShowNotification("~o~Use \"/attach cancel\" to cancel attachment");
            }
            else
            {
                Vehicle towVehicle = Game.PlayerPed.LastVehicle;

                if (_attachmentStage != AttachmentStage.None)
                {
                    Screen.ShowNotification("~r~You are already interacting with another vehicle!");
                }
                else if (!Entity.Exists(towVehicle))
                {
                    Screen.ShowNotification("~r~Your last vehicle was not towing anything!", true);
                }
                else if (towVehicle.Position.DistanceToSquared2D(Game.PlayerPed.Position) > _config.MaxDistanceFromTowVehicle)
                {
                    Screen.ShowNotification($"~r~{towVehicle.LocalizedName ?? "Tow truck"} too far away!", true);
                }
                else
                {
                    List<TowedVehicle> towedVehicles = GetTowedVehicles(towVehicle);

                    if (towedVehicles.Count() == 0)
                    {
                        Screen.ShowNotification("~r~Your last vehicle is not towing anything!");
                    }
                    else
                    {
                        TowedVehicle towedVehicle = towedVehicles.Last();

                        Vehicle vehicleBeingTowed = (Vehicle)Entity.FromNetworkId(towedVehicle.NetworkId);

                        if (!Entity.Exists(vehicleBeingTowed))
                        {
                            Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                            Screen.ShowNotification("~r~Vehicle being towed deleted!");
                        }
                        else
                        {
                            _tempTowVehicle = towVehicle;
                            _tempVehicleBeingTowed = vehicleBeingTowed;

                            ShowTowControls();

                            _tempVehicleBeingTowed.Opacity = 225;

                            _attachmentStage = AttachmentStage.Detach;
                            Tick += AttachmentTick;

                            Game.PlaySound("TOGGLE_ON", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                            Screen.ShowNotification("~g~Follow the instructions to detach the vehicle.");
                        }
                    }
                }
            }
        }
        #endregion

        #region Tick Handlers
        /// <summary>
        /// Handles vehicle selection, attaching, detaching, and canceling
        /// </summary>
        internal async Task AttachmentTick()
        {
            switch (_attachmentStage)
            {
                #region Selecting tow truck
                case AttachmentStage.TowTruck:
                    {
                        Vehicle towTruck = FindVehicle();

                        if (towTruck == null)
                        {
                            Screen.DisplayHelpTextThisFrame("No vehicle found!");
                        }
                        else if (IsAlreadyBeingUsed(towTruck))
                        {
                            Screen.DisplayHelpTextThisFrame($"Someone else is using the {towTruck.LocalizedName ?? "tow truck"}.");
                        }
                        else if (
                            (!_config.BlacklistToWhitelist && _config.AttachmentBlacklist.Contains(towTruck.Model)) ||
                            (_config.BlacklistToWhitelist && !_config.AttachmentBlacklist.Contains(towTruck.Model))
                        )
                        {
                            Screen.DisplayHelpTextThisFrame($"The {towTruck.LocalizedName ?? "tow truck"} cannot be used as a tow vehicle!");
                        }
                        else if (_config.MaxNumberOfAttachedVehicles > 0 && GetTowedVehicles(towTruck).Count() >= _config.MaxNumberOfAttachedVehicles)
                        {
                            Screen.DisplayHelpTextThisFrame($"{towTruck.LocalizedName ?? "tow truck"} cannot tow any more vehicles.");
                        }
                        else
                        {
                            if (_config.EnableLine)
                            {
                                World.DrawLine(Game.PlayerPed.Position, towTruck.Position, System.Drawing.Color.FromArgb(255, 0, 255, 0));
                            }

                            Screen.DisplayHelpTextThisFrame($"~INPUT_FRONTEND_ACCEPT~ to use the {towTruck.LocalizedName ?? "tow truck"} as the towing vehicle.");

                            if (Game.IsControlJustPressed(0, Control.FrontendAccept))
                            {
                                Game.PlaySound("OK", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                                Screen.ShowNotification($"~g~{towTruck.LocalizedName ?? "tow truck"} confirmed as towing vehicle! Now select a vehicle to be towed.");

                                _tempTowVehicle = towTruck;
                                _attachmentStage = AttachmentStage.VehicleToBeTowed;

                                SetVehicleAsBeingUsed(towTruck, true);

                                await Delay(1000);
                            }
                        }
                    }
                    break;
                #endregion

                #region Selecting vehicle to be towed
                case AttachmentStage.VehicleToBeTowed:
                    {
                        Vehicle vehicleToBeTowed = FindVehicle();

                        if (vehicleToBeTowed == null)
                        {
                            Screen.DisplayHelpTextThisFrame("No vehicle that be can towed found!");
                        }
                        else if (!Entity.Exists(_tempTowVehicle))
                        {
                            _attachmentStage = AttachmentStage.Cancel;

                            Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                            Screen.ShowNotification("~r~Tow vehicle deleted, cannot attach to nothing!");
                        }
                        else if (IsAlreadyBeingUsed(vehicleToBeTowed))
                        {
                            Screen.DisplayHelpTextThisFrame($"The {vehicleToBeTowed.LocalizedName ?? "vehicle"} is already in use.");
                        }
                        else if (
                            (!_config.BlacklistToWhitelist && _config.AttachmentBlacklist.Contains(vehicleToBeTowed.Model)) ||
                            (_config.BlacklistToWhitelist && _config.WhitelistForTowedVehicles && !_config.AttachmentBlacklist.Contains(vehicleToBeTowed.Model))
                        )
                        {
                            Screen.DisplayHelpTextThisFrame($"The {vehicleToBeTowed.LocalizedName ?? "vehicle"} cannot be towed!");
                        }
                        else if (vehicleToBeTowed.Occupants.Length > 0)
                        {
                            Screen.DisplayHelpTextThisFrame($"The {vehicleToBeTowed.LocalizedName ?? "vehicle"} is occupied!");
                        }
                        else if (Entity.Exists(_tempTowVehicle) && vehicleToBeTowed.Position.DistanceToSquared2D(_tempTowVehicle.Position) > _config.MaxDistanceFromTowVehicle)
                        {
                            Screen.DisplayHelpTextThisFrame($"The {vehicleToBeTowed.LocalizedName ?? "vehicle"} is too far from the {_tempTowVehicle.LocalizedName ?? "tow truck"}!");
                        }
                        else
                        {
                            if (_config.EnableLine)
                            {
                                World.DrawLine(Game.PlayerPed.Position, vehicleToBeTowed.Position, System.Drawing.Color.FromArgb(255, 0, 255, 0));
                            }

                            Screen.DisplayHelpTextThisFrame($"~INPUT_FRONTEND_ACCEPT~ to tow the {vehicleToBeTowed.LocalizedName ?? "vehicle"}.");

                            if (Game.IsControlJustPressed(0, Control.FrontendAccept))
                            {
                                SetVehicleAsBeingUsed(vehicleToBeTowed, true);

                                int timeout = 4;
                                API.NetworkRequestControlOfNetworkId(vehicleToBeTowed.NetworkId);
                                while (!API.NetworkHasControlOfNetworkId(vehicleToBeTowed.NetworkId) && timeout > 0)
                                {
                                    timeout--;

                                    API.NetworkRequestControlOfNetworkId(vehicleToBeTowed.NetworkId);
                                    await Delay(250);
                                }

                                if (!API.NetworkHasControlOfNetworkId(vehicleToBeTowed.NetworkId))
                                {
                                    Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                                    Screen.ShowNotification($"~r~Could not tow the {vehicleToBeTowed.LocalizedName ?? "vehicle"}.", true);

                                    Debug.WriteLine($"Unable to tow {vehicleToBeTowed.LocalizedName} ({vehicleToBeTowed.NetworkId}); ownership of the vehicle could not be requested!");

                                    _attachmentStage = AttachmentStage.Cancel;
                                }
                                else
                                {
                                    Game.PlaySound("OK", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                                    Screen.ShowNotification($"~g~{vehicleToBeTowed.LocalizedName ?? "vehicle"} confirmed as vehicle to be towed! Follow instructions to position vehicle.");

                                    ShowTowControls();

                                    vehicleToBeTowed.Opacity = 225;
                                    vehicleToBeTowed.IsPersistent = true;
                                    vehicleToBeTowed.IsPositionFrozen = true;
                                    vehicleToBeTowed.IsCollisionEnabled = false;
                                    vehicleToBeTowed.LockStatus = VehicleLockStatus.CannotBeTriedToEnter;
                                    vehicleToBeTowed.AttachTo(_tempTowVehicle, POSITION_VECTOR, ROTATION_VECTOR);

                                    AddNewTowedVehicle(_tempTowVehicle, new TowedVehicle()
                                    {
                                        NetworkId = vehicleToBeTowed.NetworkId,
                                        AttachmentPosition = POSITION_VECTOR,
                                        AttachmentRotation = ROTATION_VECTOR
                                    });

                                    _tempVehicleBeingTowed = vehicleToBeTowed;
                                    _attachmentStage = AttachmentStage.Position;

                                    await Delay(1000);
                                }
                            }
                        }
                    }
                    break;
                #endregion

                #region Cancel current attachment
                case AttachmentStage.Cancel:
                    {
                        if (Entity.Exists(_tempTowVehicle))
                        {
                            if (!Entity.Exists(_tempVehicleBeingTowed))
                            {
                                SetVehicleAsBeingUsed(_tempTowVehicle, false);

                                Screen.ShowNotification("~g~Attachment canceled.");
                                Tick -= AttachmentTick;
                                _attachmentStage = AttachmentStage.None;
                            }
                            else
                            {
                                if (_tempTowVehicle.Position.DistanceToSquared2D(Game.PlayerPed.Position) > _config.MaxDistanceFromTowVehicle)
                                {
                                    Screen.ShowNotification($"~r~{_tempTowVehicle.LocalizedName ?? "Tow truck"} too far away!", true);
                                }
                                else
                                {
                                    ResetTowedVehicle(_tempVehicleBeingTowed);
                                    SetVehicleAsBeingUsed(_tempTowVehicle, false);
                                    SetVehicleAsBeingUsed(_tempVehicleBeingTowed, false);
                                    RemoveTowedVehicle(_tempTowVehicle, _tempVehicleBeingTowed);

                                    Screen.ShowNotification("~g~Attachment canceled.");
                                    Tick -= AttachmentTick;
                                    _attachmentStage = AttachmentStage.None;
                                }
                            }
                        }
                        else
                        {
                            Screen.ShowNotification("~g~Attachment canceled.");
                            Tick -= AttachmentTick;
                            _attachmentStage = AttachmentStage.None;
                        }
                    }
                    break;
                #endregion

                #region Position/Detach
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
                #endregion
            }
        }
        #endregion

        #region Functions
        /// <summary>
        /// Returns the <see cref="Vehicle"/> infront of the player
        /// </summary>
        /// <returns><see cref="Vehicle"/> infront of player</returns>
        internal Vehicle FindVehicle()
        {
            RaycastResult raycast = World.RaycastCapsule(Game.PlayerPed.Position, Game.PlayerPed.GetOffsetPosition(RAYCAST_VECTOR), 0.3f, (IntersectOptions)10, Game.PlayerPed);

            if (!raycast.DitHitEntity || !Entity.Exists(raycast.HitEntity) || !raycast.HitEntity.Model.IsVehicle)
            {
                return null;
            }

            return (Vehicle)raycast.HitEntity;
        }

        /// <summary>
        /// Properly detaches and resets a <see cref="Vehicle"/> that is attached to another <see cref="Vehicle"/>
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
            if (_config.EnableInstructions)
            {
                API.BeginTextCommandDisplayHelp("CELL_EMAIL_BCON");

                foreach (string s in Screen.StringToArray(TOW_CONTROLS))
                {
                    API.AddTextComponentSubstringPlayerName(s);
                }

                API.EndTextCommandDisplayHelp(0, false, true, _config.InstructionDisplayTime);
            }
        }

        /// <summary>
        /// Handles the control input from the keybind maps
        /// </summary>
        /// <param name="attachmentControl"></param>
        internal void OnControl(AttachmentControl attachmentControl)
        {
            if (_attachmentStage != AttachmentStage.Position && _attachmentStage != AttachmentStage.Detach)
            {
                return;
            }

            float changeAmount = _config.ChangeAmount;

            changeAmount += _goFaster ? _config.FasterAmount : _goSlower ? _config.SlowerAmount : 0f;

            if (!Entity.Exists(_tempTowVehicle) || !Entity.Exists(_tempVehicleBeingTowed))
            {
                Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                Screen.ShowNotification("~g~Attachment canceled.");

                _attachmentStage = AttachmentStage.Cancel;
            }
            else
            {
                TowedVehicle towedVehicle = GetTowedVehicles(_tempTowVehicle).Last();

                Vector3
                    position = towedVehicle.AttachmentPosition,
                    rotation = towedVehicle.AttachmentRotation;

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
                        SetVehicleAsBeingUsed(_tempTowVehicle, false);
                        SetVehicleAsBeingUsed(_tempVehicleBeingTowed, false);

                        if (_attachmentStage == AttachmentStage.Position)
                        {
                            Screen.ShowNotification("~g~Attachment complete! Drive safe.");

                            _tempVehicleBeingTowed.ResetOpacity();
                            _tempVehicleBeingTowed.IsCollisionEnabled = true;
                        }
                        else if (_attachmentStage == AttachmentStage.Detach)
                        {
                            Screen.ShowNotification($"~g~{_tempVehicleBeingTowed.LocalizedName ?? "Vehicle"} detached!");

                            ResetTowedVehicle(_tempVehicleBeingTowed);
                            SetVehicleAsBeingUsed(_tempVehicleBeingTowed, false);
                            RemoveTowedVehicle(_tempTowVehicle, _tempVehicleBeingTowed);
                        }

                        _tempTowVehicle = null;
                        _tempVehicleBeingTowed = null;

                        Game.PlaySound("WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET");

                        Tick -= AttachmentTick;
                        _attachmentStage = AttachmentStage.None;
                        return;
                }

                if (_tempTowVehicle.Position.DistanceToSquared(_tempTowVehicle.GetOffsetPosition(position)) > _config.MaxDistanceFromTowVehicle)
                {
                    Screen.ShowNotification("~r~Cannot move there, too far from tow vehicle!", true);
                }
                else
                {
                    _tempVehicleBeingTowed.AttachTo(_tempTowVehicle, position, rotation);

                    TowedVehicle updatedTowedVehicle = new TowedVehicle()
                    {
                        NetworkId = towedVehicle.NetworkId,
                        AttachmentPosition = position,
                        AttachmentRotation = rotation
                    };

                    UpdateTowedVehicle(_tempTowVehicle, _tempVehicleBeingTowed, updatedTowedVehicle);
                }
            }
        }

        /// <summary>
        /// Determines if a <see cref="Vehicle"/> is already being used as a
        /// tow truck (mid placement), or a vehicle being towed
        /// </summary>
        /// <param name="vehicle"><see cref="Vehicle"/> to check</param>
        /// <returns></returns>
        internal bool IsAlreadyBeingUsed(Vehicle vehicle)
        {
            if (vehicle.State.Get("isBeingUsed") == null)
            {
                vehicle.State.Set("isBeingUsed", false, true);
            }

            return vehicle.State.Get("isBeingUsed");
        }

        /// <summary>
        /// Sets a <see cref="Vehicle"/> as in use
        /// </summary>
        /// <param name="vehicle"><see cref="Vehicle"/> to set</param>
        /// <param name="beingUsed"><see cref="bool"/> to set</param>
        internal void SetVehicleAsBeingUsed(Vehicle vehicle, bool beingUsed)
        {
            // Initializes if null
            bool _ = IsAlreadyBeingUsed(vehicle);

            vehicle.State.Set("isBeingUsed", beingUsed, true);
        }

        /// <summary>
        /// Returns a <see cref="List{TowedVehicle}"/> a <see cref="Vehicle"/> is towing
        /// </summary>
        /// <param name="vehicle"><see cref="Vehicle"/> to check</param>
        /// <returns></returns>
        internal List<TowedVehicle> GetTowedVehicles(Vehicle vehicle)
        {
            if (vehicle.State.Get("vehiclesBeingTowed") == null)
            {
                vehicle.State.Set("vehiclesBeingTowed", JsonConvert.SerializeObject(new List<TowedVehicle>()), true);
            }

            return JsonConvert.DeserializeObject<List<TowedVehicle>>(vehicle.State.Get("vehiclesBeingTowed"));
        }

        /// <summary>
        /// Adds a new <see cref="TowedVehicle"/> as a vehicle being towed
        /// </summary>
        /// <param name="towVehicle"><see cref="Vehicle"/> doing the towing</param>
        /// <param name="towedVehicle"><see cref="TowedVehicle"/> being towed</param>
        internal void AddNewTowedVehicle(Vehicle towVehicle, TowedVehicle towedVehicle)
        {
            List<TowedVehicle> towedVehicles = GetTowedVehicles(towVehicle);

            towedVehicles.Add(towedVehicle);

            towVehicle.State.Set("vehiclesBeingTowed", JsonConvert.SerializeObject(towedVehicles), true);
        }

        /// <summary>
        /// Updates a <see cref="TowedVehicle"/> that is already being towed
        /// </summary>
        /// <param name="towVehicle"><see cref="Vehicle"/> doing the towing</param>
        /// <param name="towedVehicle"><see cref="Vehicle"/> being towed</param>
        /// <param name="updatedTowedVehicle">Updated <see cref="TowedVehicle"/> information</param>
        internal void UpdateTowedVehicle(Vehicle towVehicle, Vehicle towedVehicle, TowedVehicle updatedTowedVehicle)
        {
            List<TowedVehicle> towedVehicles = GetTowedVehicles(towVehicle);

            towedVehicles.RemoveAll(i => i.NetworkId == towedVehicle.NetworkId);
            towedVehicles.Add(updatedTowedVehicle);

            towVehicle.State.Set("vehiclesBeingTowed", JsonConvert.SerializeObject(towedVehicles), true);
        }

        /// <summary>
        /// Removes a <see cref="Vehicle"/> as being towed
        /// </summary>
        /// <param name="towVehicle"><see cref="Vehicle"/> doing the towing</param>
        /// <param name="towedVehicle"><see cref="Vehicle"/> being towed</param>
        internal void RemoveTowedVehicle(Vehicle towVehicle, Vehicle towedVehicle)
        {
            List<TowedVehicle> towedVehicles = GetTowedVehicles(towVehicle);

            towedVehicles.RemoveAll(i => i.NetworkId == towedVehicle.NetworkId);

            towVehicle.State.Set("vehiclesBeingTowed", JsonConvert.SerializeObject(towedVehicles), true);
        }
        #endregion
    }
}
