﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices
{
    /// <summary>
    /// The service response to a get digital twin request.
    /// </summary>
    /// <typeparam name="T">The type of the digital twin.</typeparam>
    public class DigitalTwinGetResponse<T>
    {
        /// <summary>
        /// Initializes a new instance of this class.
        /// </summary>
        /// <param name="eTag">Weak ETag of the modified resource.</param>
        /// <param name="digitalTwin">The deserialized digital twin.</param>
        internal DigitalTwinGetResponse(T digitalTwin, string eTag = default(string))
        {
            DigitalTwin = digitalTwin;
            ETag = eTag;
        }

        /// <summary>
        /// Gets the deserialized digital twin.
        /// </summary>
        public T DigitalTwin { get; internal set; }

        /// <summary>
        /// Gets the weak ETag of the modified resource.
        /// </summary>
        public string ETag { get; internal set; }
    }
}