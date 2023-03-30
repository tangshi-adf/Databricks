﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.Azure.Devices
{
    /// <summary>
    /// The context for a given connection loss event for <see cref="MessageFeedbackProcessorClient"/>,
    /// <see cref="FileUploadNotificationProcessorClient"/>, and <see cref="MessagesClient"/>.
    /// </summary>
    /// <remarks>
    /// The context includes the cause of the connection loss and makes a distinction between network
    /// level issues(no internet) and IoT Hub level issues(resource not found, throttling, internal server error, etc.).
    /// </remarks>
    public class ErrorContext
    {
        /// <summary>
        /// The IoT hub level exception, if any IoT hub level exception caused this connection loss.
        /// </summary>
        /// <remarks>
        /// For example, if the device does not exist.
        /// </remarks>
        /// <param name="iotHubServiceException"></param>
        internal ErrorContext(IotHubServiceException iotHubServiceException)
        {
            IotHubServiceException = iotHubServiceException;
        }

        /// <summary>
        /// The network level exception, if any network level exception caused this connection loss.
        /// </summary>
        /// <remarks>
        /// For example, if the device has no internet connection.
        /// </remarks>
        /// <param name="ioException"></param>
        internal ErrorContext(IOException ioException)
        {
            IOException = ioException;
        }

        /// <summary>
        /// The IoT hub level exception, if any IoT hub level exception caused this connection loss.
        /// </summary>
        /// <remarks>
        /// For example, if you attempt to send a cloud-to-device message to a device that does not exist. if this exception is null,
        /// then <see cref="IOException"/> will not be null.
        /// </remarks>
        public IotHubServiceException IotHubServiceException { get; }

        /// <summary>
        /// The network level exception, if any network level exception caused this connection loss.
        /// </summary>
        /// <remarks>
        /// For example, if you attempt to send a cloud-to-device message to a device when your device has no internet connection.
        /// If this exception is null, then <see cref="IotHubServiceException"/> will not be null.
        /// </remarks>
        public IOException IOException { get; }
    }
}