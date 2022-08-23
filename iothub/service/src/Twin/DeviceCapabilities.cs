﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;

namespace Microsoft.Azure.Devices
{
    /// <summary>
    /// Status of capabilities enabled on the device.
    /// </summary>
    public class DeviceCapabilities
    {
        /// <summary>
        /// IoT edge capability.
        /// </summary>
        [JsonProperty(PropertyName = "iotEdge")]
        public bool IotEdge { get; set; }
    }
}
