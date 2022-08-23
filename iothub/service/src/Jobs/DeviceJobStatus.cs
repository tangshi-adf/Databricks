﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Microsoft.Azure.Devices
{
    /// <summary>
    /// Device job status enumeration
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum DeviceJobStatus
    {
        /// <summary>
        /// Pending
        /// </summary>
        [EnumMember(Value = "pending")]
        Pending,

        /// <summary>
        /// Scheduled
        /// </summary>
        [EnumMember(Value = "scheduled")]
        Scheduled,

        /// <summary>
        /// Running
        /// </summary>
        [EnumMember(Value = "running")]
        Running,

        /// <summary>
        /// Completed
        /// </summary>
        [EnumMember(Value = "completed")]
        Completed,

        /// <summary>
        /// Failed
        /// </summary>
        [EnumMember(Value = "failed")]
        Failed,

        /// <summary>
        /// Canceled
        /// </summary>
        [EnumMember(Value = "canceled")]
        Canceled
    }
}
