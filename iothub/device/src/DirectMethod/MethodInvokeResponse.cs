﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.Devices.Client
{
    /// <summary>
    /// Represents the method invocation result.
    /// </summary>
    internal class MethodInvokeResponse
    {
        /// <summary>
        /// Gets or sets the status of device method invocation.
        /// </summary>
        [JsonProperty("status")]
        public int Status { get; set; }

        /// <summary>
        /// Get payload as json.
        /// </summary>
        public string GetPayloadAsJson()
        {
            return (string)Payload;
        }

        [JsonProperty("payload")]
        internal JRaw Payload { get; set; }
    }
}
