﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Microsoft.Azure.Devices.Provisioning.Client
{
    /// <summary>
    /// The provisioning substatus type.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ProvisioningRegistrationSubstatus
    {
        /// <summary>
        /// Device has been assigned to an IoT hub for the first time.
        /// </summary>
        [EnumMember(Value = "initialAssignment")]
        InitialAssignment,

        /// <summary>
        /// Device has been assigned to a different IoT hub and its device data was migrated from the previously assigned IoT hub.
        /// Device data was removed from the previously assigned IoT hub.
        /// </summary>
        [EnumMember(Value = "deviceDataMigrated")]
        DeviceDataMigrated,

        /// <summary>
        /// Device has been assigned to a different IoT hub and its device data was populated from the initial state stored in the enrollment.
        /// Device data was removed from the previously assigned IoT hub.
        /// </summary>
        [EnumMember(Value = "deviceDataReset")]
        DeviceDataReset,

        /// <summary>
        /// Device has been reprovisioned to a previously assigned IoT hub.
        /// </summary>
        [EnumMember(Value = "reprovisionedToInitialAssignment")]
        ReprovisionedToInitialAssignment,
    }
}