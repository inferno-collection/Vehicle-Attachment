-- Inferno Collection Vehicle Attachment 1.0 Alpha
--
-- Copyright (c) 2020, Christopher M, Inferno Collection. All rights reserved.
--
-- This project is licensed under the following:
-- Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to use, copy, modify, and merge the software, under the following conditions:
-- The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
-- THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. THE SOFTWARE MAY NOT BE SOLD.
--

--
--		Nothing past this point needs to be edited, all the settings for the resource are found ABOVE this line.
--		Do not make changes below this line unless you know what you are doing!
--

local VehiclesInUse = {}

RegisterServerEvent("Vehicle-Attachment:Server:Add")
AddEventHandler("Vehicle-Attachment:Server:Add", function(networkId)
    if not VehiclesInUse[networkId] then
        VehiclesInUse[networkId] = source
    end

    TriggerClientEvent("Vehicle-Attachment:Client:Return", -1, VehiclesInUse)
end)

RegisterServerEvent("Vehicle-Attachment:Server:Remove")
AddEventHandler("Vehicle-Attachment:Server:Remove", function(networkId)
    if VehiclesInUse[networkId] then
        VehiclesInUse[networkId] = source
    end

    TriggerClientEvent("Vehicle-Attachment:Client:Return", -1, VehiclesInUse)
end)