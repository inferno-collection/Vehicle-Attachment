-- Inferno Collection Vehicle Attachment 1.1 Alpha
--
-- Copyright (c) 2020, Christopher M, Inferno Collection. All rights reserved.
--
-- This project is licensed under the following:
-- Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to use, copy, modify, and merge the software, under the following conditions:
-- The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
-- THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. THE SOFTWARE MAY NOT BE SOLD.
--

--
-- Resource Configuration
-- Please note, there is also some configuration required in the `server.lua` file, so make sure to edit that file as well
--
-- PLEASE RESTART SERVER AFTER MAKING CHANGES TO THIS CONFIGURATION
--

local Config = {} -- Do not edit this line

Config.TowVehicles = {
    "flatbed"
}

Config.ChangeAmount = 0.2

--
--		Nothing past this point needs to be edited, all the settings for the resource are found ABOVE this line.
--		Do not make changes below this line unless you know what you are doing!
--

local Attachment = 0
local SpeedUp = false
local SlowDown = false
local TowVehicle = false
local VehicleBeingTowed = false
local AttachVector = vector3(0.0, -2.0, 1.5)
local AttachRotVector = vector3(0.0, 0.0, 0.0)

local VehiclesInUse = {}

RegisterCommand("attachment", function()
    if Attachment == 0 then
        if not VehicleBeingTowed then
            Attachment = 1
        else
            Attachment = 4
        end
    else
        Attachment = -1
    end
end)

function TruckTest()
    local PlayerPed = PlayerPedId()
    local PlayerCoords = GetEntityCoords(PlayerPed, false)
    local RayCast = StartShapeTestRay(PlayerCoords.x, PlayerCoords.y, PlayerCoords.z, GetOffsetFromEntityInWorldCoords(PlayerPed, 0.0, 10.0, 0.0), 10, PlayerPed, 0)
    local _, _, RayCoords, _, RayEntity = GetRaycastResult(RayCast)

    if Vdist(PlayerCoords.x, PlayerCoords.y, PlayerCoords.z, RayCoords.x, RayCoords.y, RayCoords.z) < 7 then
        for _, Vehicle in ipairs(Config.TowVehicles) do if GetHashKey(Vehicle) == GetEntityModel(RayEntity) then return {RayEntity, PlayerCoords, GetEntityCoords(RayEntity, false), VehToNet(RayEntity)} end end

        return false
    else
        return false
    end
end

function VehicleTest()
    local PlayerPed = PlayerPedId()
    local PlayerCoords = GetEntityCoords(PlayerPed, false)
    local RayCast = StartShapeTestRay(PlayerCoords.x, PlayerCoords.y, PlayerCoords.z, GetOffsetFromEntityInWorldCoords(PlayerPed, 0.0, 10.0, 0.0), 10, PlayerPed, 0)
    local _, _, RayCoords, _, RayEntity = GetRaycastResult(RayCast)

    if Vdist(PlayerCoords.x, PlayerCoords.y, PlayerCoords.z, RayCoords.x, RayCoords.y, RayCoords.z) < 5 then
        return {RayEntity, PlayerCoords, GetEntityCoords(RayEntity, false), VehToNet(RayEntity)}
    else
        return false
    end
end

-- Draws a notification on the player's screen
function NewNotification(Text, Flash)
    SetNotificationTextEntry("STRING")
    AddTextComponentString(Text)
    DrawNotification(Flash, true)
end

-- Draws a hint on the player's screen
function NewHint(Text)
    SetTextComponentFormat("STRING")
    AddTextComponentString(Text)
    DisplayHelpTextFromStringLabel(0, 0, 1, -1)
end

function TowControls()
    TriggerEvent("chat:addMessage", {
        color = {0, 255, 0},
        multiline = true,
        args = {
            "Tow",
            "\nNUMPAD 8/5 (or Left Stick) = Forward/Backwards\n NUMPAD 4/6 (or Left Stick) = Left/Right\nNUMPAD +/- (or Left Stick) = Up/Down\nNUMPAD 7/9 (or Left Stick) = Rotate Left/Right\nHold Left Shift (or X)/Left Control (or A) = Speed Up/Slow Down\nEnter (or A) = Confirm Position"
        }
    })
end

RegisterNetEvent("Vehicle-Attachment:Client:Return")
AddEventHandler("Vehicle-Attachment:Client:Return", function(NewVehiclesInUse) VehiclesInUse = NewVehiclesInUse end)

Citizen.CreateThread(function()
    while true do
        Citizen.Wait(0)

        if Attachment ~= 0 then
            local PlayerServerId = GetPlayerServerId(PlayerId())

            if IsControlPressed(1, 131) then
                SpeedUp = true
            elseif SpeedUp == "toBeFalse" then
                SpeedUp = false
            elseif SpeedUp then
                SpeedUp = "toBeFalse"
            end

            if IsControlPressed(1, 132) then
                SlowDown = true
            elseif SlowDown == "toBeFalse" then
                SlowDown = false
            elseif SlowDown then
                SlowDown = "toBeFalse"
            end

            if Attachment == 1 then
                local TowTruck = TruckTest()

                if TowTruck then
                    DrawLine(TowTruck[2], TowTruck[3], 255.0, 0.0, 0.0, 0.5)

                    if not VehiclesInUse[TowTruck[4]] or VehiclesInUse[TowTruck[4]] == PlayerServerId then
                        NewHint("Press ~INPUT_FRONTEND_ACCEPT~ to confirm Tow vehicle.")
                    else
                        NewHint("Vehicle already in use.")
                    end

                else
                    NewHint("Get closer to the Tow vehicle.")
                end

                if IsControlJustPressed(1, 201) then
                    if TowTruck then
                        if not VehiclesInUse[TowTruck[4]] or VehiclesInUse[TowTruck[4]] == PlayerServerId then
                            Attachment = 2
                            TowVehicle = TowTruck[1]

                            TriggerServerEvent("Vehicle-Attachment:Server:Add", TowTruck[4])
                            NewNotification("~g~Tow vehicle confirmed!")
                        else
                            NewNotification("~g~Vehicle already in use.", true)
                        end
                    else
                        NewNotification("~r~No Tow vehicle found!", true)
                    end
                end
            elseif Attachment == 2 then
                local VehicleToBeTowed = VehicleTest()

                if VehicleToBeTowed and VehicleToBeTowed[1] ~= TowVehicle then
                    DrawLine(VehicleToBeTowed[2], VehicleToBeTowed[3], 0.0, 255.0, 0.0, 0.5)

                    if not VehiclesInUse[VehicleToBeTowed[4]] or VehiclesInUse[VehicleToBeTowed[4]] == PlayerServerId then
                        NewHint("Press ~INPUT_FRONTEND_ACCEPT~ to confirm vehicle to be towed.")
                    else
                        NewHint("Vehicle already in use.")
                    end
                else
                    NewHint("Get closer to the vehicle to be towed.")
                end

                if IsControlJustPressed(1, 201) then
                    if VehicleToBeTowed then
                        if not VehiclesInUse[VehicleToBeTowed[4]] or VehiclesInUse[VehicleToBeTowed[4]] == PlayerServerId then
                            local Occupied = false

                            for i = -1, GetVehicleModelNumberOfSeats(GetEntityModel(VehicleToBeTowed[1])) - 2, 1 do
                                if not IsVehicleSeatFree(VehicleToBeTowed[1], i) then
                                    Occupied = true
                                    break
                                end
                            end

                            if Occupied then
                                NewNotification("~r~Vehicle cannot be occupied!.", true)
                            else
                                VehicleBeingTowed = VehicleToBeTowed[1]

                                TriggerServerEvent("Vehicle-Attachment:Server:Add", VehicleToBeTowed[4])
                                NewNotification("~g~Vehicle to be towed confirmed! Follow instructions to position vehicle.", false)

                                NetworkRequestControlOfEntity(VehicleBeingTowed)
                                while not NetworkHasControlOfEntity(VehicleBeingTowed) do
                                    NewHint("Please wait...")
                                    Wait(0)
                                end

                                SetEntityAlpha(VehicleBeingTowed, 200, 0)
                                FreezeEntityPosition(VehicleBeingTowed, true)
                                SetEntityCollision(VehicleBeingTowed, false, false)
                                SetVehicleDoorsLockedForAllPlayers(VehicleBeingTowed, true)
                                SetEntityAsMissionEntity(VehicleBeingTowed, true, false)
                                AttachEntityToEntity(VehicleBeingTowed, TowVehicle, -1, AttachVector, AttachRotVector, false, false, true, false, 2, true)

                                TowControls()

                                Attachment = 3
                            end
                        else
                            NewNotification("~g~Vehicle already in use.", true)
                        end
                    end
                end
            elseif Attachment == 3 or Attachment == 5 then
                local VectorCopy = AttachVector
                local VectorRotCopy = AttachRotVector
                local AmountCopy = Config.ChangeAmount

                if SpeedUp then
                    AmountCopy = AmountCopy + 2.0
                elseif SlowDown then
                    AmountCopy = AmountCopy - 0.15
                end

                if DoesEntityExist(TowVehicle) and DoesEntityExist(VehicleBeingTowed) then
                    if IsControlJustPressed(1, 111) then
                        AttachVector = vector3(AttachVector.x, AttachVector.y + AmountCopy, AttachVector.z)
                    elseif IsControlJustPressed(1, 112) then
                        AttachVector = vector3(AttachVector.x, AttachVector.y - AmountCopy, AttachVector.z)
                    elseif IsControlJustPressed(1, 108) then
                        AttachVector = vector3(AttachVector.x - AmountCopy, AttachVector.y, AttachVector.z)
                    elseif IsControlJustPressed(1, 109) then
                        AttachVector = vector3(AttachVector.x + AmountCopy, AttachVector.y, AttachVector.z)
                    elseif IsControlJustPressed(1, 314) then
                        AttachVector = vector3(AttachVector.x, AttachVector.y, AttachVector.z + AmountCopy)
                    elseif IsControlJustPressed(1, 315) then
                        AttachVector = vector3(AttachVector.x, AttachVector.y, AttachVector.z - AmountCopy)
                    elseif IsControlJustPressed(1, 117) then
                        AttachRotVector = vector3(AttachRotVector.x, AttachRotVector.y, AttachRotVector.z + (AmountCopy * 10))
                    elseif IsControlJustPressed(1, 118) then
                        AttachRotVector = vector3(AttachRotVector.x, AttachRotVector.y, AttachRotVector.z - (AmountCopy * 10))
                    elseif IsControlJustPressed(1, 201) then
                        if Attachment == 3 then
                            NewNotification("~g~Attachment confirmed! Drive safe.", false)

                            ResetEntityAlpha(VehicleBeingTowed)

                            Attachment = 0
                            VectorCopy = AttachVector
                            VectorRotCopy = AttachRotVector
                        elseif Attachment == 5 then
                            NewNotification("~g~Vehicle detached.", false)

                            ResetEntityAlpha(VehicleBeingTowed)
                            DetachEntity(VehicleBeingTowed, true, true)
                            FreezeEntityPosition(VehicleBeingTowed, false)
                            SetEntityCollision(VehicleBeingTowed, true, true)
                            SetVehicleDoorsLockedForAllPlayers(VehicleBeingTowed, false)
                            ApplyForceToEntityCenterOfMass(VehicleBeingTowed, 1, 0.0, 0.0, 0.0001, false, false, false, false)

                            TriggerServerEvent("Vehicle-Attachment:Server:Remove", VehToNet(TowVehicle))
                            TriggerServerEvent("Vehicle-Attachment:Server:Remove", VehToNet(VehicleBeingTowed))

                            Attachment = 0
                            TowVehicle = false
                            VectorCopy = vector3(0.0, -2.0, 1.5)
                            VectorRotCopy = vector3(0.0, 0.0, 0.0)
                            AttachVector = vector3(0.0, -2.0, 1.5)
                            AttachRotVector = vector3(0.0, 0.0, 0.0)
                            VehicleBeingTowed = false
                        end
                    end

                    if VectorCopy ~= AttachVector or VectorRotCopy ~= AttachRotVector then AttachEntityToEntity(VehicleBeingTowed, TowVehicle, -1, AttachVector, AttachRotVector, false, false, true, false, 2, true) end
                else
                    Attachment = 0
                    TowVehicle = false
                    AttachVector = vector3(0.0, -2.0, 1.5)
                    AttachRotVector = vector3(0.0, 0.0, 0.0)
                    VehicleBeingTowed = false
                end
            elseif Attachment == 4 then
                SetEntityAlpha(VehicleBeingTowed, 200, 0)
                AttachEntityToEntity(VehicleBeingTowed, TowVehicle, -1, AttachVector, AttachRotVector, false, false, true, false, 2, true)

                TowControls()

                Attachment = 5
            elseif Attachment == -1 then
                if TowVehicle then
                    TriggerServerEvent("Vehicle-Attachment:Server:Remove", VehToNet(TowVehicle))

                    TowVehicle = false
                end

                if VehicleBeingTowed then
                    TriggerServerEvent("Vehicle-Attachment:Server:Remove", VehToNet(VehicleBeingTowed))

                    ResetEntityAlpha(VehicleBeingTowed)
                    DetachEntity(VehicleBeingTowed, true, true)
                    FreezeEntityPosition(VehicleBeingTowed, false)
                    SetEntityCollision(VehicleBeingTowed, true, true)
                    SetVehicleDoorsLockedForAllPlayers(VehicleBeingTowed, false)
                    ApplyForceToEntity(VehicleBeingTowed, 1, 0.0, 0.0, 0.0001, 0.0, 0.0, 0.0, 0, false, true, true, false, true)
                end

                NewNotification("~g~Attachment canceled.", false)

                Attachment = 0
            end
        end
    end
end)