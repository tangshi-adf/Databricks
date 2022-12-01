﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.Devices
{
    /// <summary>
    /// The data structure represent the message that is used for interacting with IoT hub.
    /// </summary>
    public sealed class Message
    {
        private readonly byte[] _payload;

        /// <summary>
        /// Default constructor with no body data.
        /// </summary>
        public Message()
        {
        }

        /// <summary>
        /// Creates a telemetry message with the specified payload.
        /// </summary>
        /// <remarks>User should treat the input byte array as immutable when sending the message.</remarks>
        /// <param name="payload">A byte array to send as a payload.</param>
        public Message(byte[] payload)
        {
            _payload = payload;
        }

        /// <summary>
        /// [Required for two way requests] Used to correlate two-way communication.
        /// Format: A case-sensitive string ( up to 128 char long) of ASCII 7-bit alphanumeric chars
        /// + {'-', ':', '/', '\', '.', '+', '%', '_', '#', '*', '?', '!', '(', ')', ',', '=', '@', ';', '$', '''}.
        /// Non-alphanumeric characters are from URN RFC.
        /// </summary>
        /// <remarks>
        /// If this value is not supplied by the user, the service client will set this to a new GUID.
        /// </remarks>
        public string MessageId
        {
            get => GetSystemProperty<string>(MessageSystemPropertyNames.MessageId);
            set => SystemProperties[MessageSystemPropertyNames.MessageId] = value;
        }

        /// <summary>
        /// [Required] Destination of the message.
        /// </summary>
        public string To
        {
            get => GetSystemProperty<string>(MessageSystemPropertyNames.To);
            set => SystemProperties[MessageSystemPropertyNames.To] = value;
        }

        /// <summary>
        /// [Optional] The time when this message is considered expired.
        /// </summary>
        public DateTimeOffset ExpiresOnUtc
        {
            get => GetSystemProperty<DateTimeOffset>(MessageSystemPropertyNames.ExpiryTimeUtc);
            set => SystemProperties[MessageSystemPropertyNames.ExpiryTimeUtc] = value;
        }

        /// <summary>
        /// A string property in a response message that typically contains the MessageId of the request, in request-reply patterns.
        /// </summary>
        public string CorrelationId
        {
            get => GetSystemProperty<string>(MessageSystemPropertyNames.CorrelationId);
            set => SystemProperties[MessageSystemPropertyNames.CorrelationId] = value;
        }

        /// <summary>
        /// [Required] SequenceNumber of the received message.
        /// </summary>
        internal ulong SequenceNumber
        {
            get => GetSystemProperty<ulong>(MessageSystemPropertyNames.SequenceNumber);
            set => SystemProperties[MessageSystemPropertyNames.SequenceNumber] = value;
        }

        /// <summary>
        /// [Required] LockToken of the received message.
        /// </summary>
        public string LockToken
        {
            get => GetSystemProperty<string>(MessageSystemPropertyNames.LockToken);
            internal set => SystemProperties[MessageSystemPropertyNames.LockToken] = value;
        }

        /// <summary>
        /// When the message was received by the server.
        /// </summary>
        internal DateTimeOffset EnqueuedOnUtc
        {
            get => GetSystemProperty<DateTimeOffset>(MessageSystemPropertyNames.EnqueuedOn);
            set => SystemProperties[MessageSystemPropertyNames.EnqueuedOn] = value;
        }

        /// <summary>
        /// [Required in feedback messages] Used to specify the origin of messages generated by device hub.
        /// Possible value: “{hub name}/”
        /// </summary>
        public string UserId
        {
            get => GetSystemProperty<string>(MessageSystemPropertyNames.UserId);
            set => SystemProperties[MessageSystemPropertyNames.UserId] = value;
        }

        /// <summary>
        /// Used to specify the schema of the message content.
        /// </summary>
        public string MessageSchema
        {
            get => GetSystemProperty<string>(MessageSystemPropertyNames.MessageSchema);
            set => SystemProperties[MessageSystemPropertyNames.MessageSchema] = value;
        }

        /// <summary>
        /// Custom date property set by the originator of the message.
        /// </summary>
        public DateTimeOffset CreatedOnUtc
        {
            get => GetSystemProperty<DateTimeOffset>(MessageSystemPropertyNames.CreationTimeUtc);
            set => SystemProperties[MessageSystemPropertyNames.CreationTimeUtc] = value;
        }

        /// <summary>
        /// Used to specify the content type of the message.
        /// </summary>
        public string ContentType
        {
            get => GetSystemProperty<string>(MessageSystemPropertyNames.ContentType);
            set => SystemProperties[MessageSystemPropertyNames.ContentType] = value;
        }

        /// <summary>
        /// Used to specify the content encoding type of the message.
        /// </summary>
        public string ContentEncoding
        {
            get => GetSystemProperty<string>(MessageSystemPropertyNames.ContentEncoding);
            set => SystemProperties[MessageSystemPropertyNames.ContentEncoding] = value;
        }

        /// <summary>
        /// Used in cloud-to-device messages to request IoT hub to generate feedback messages as a result of the consumption of the message by the device.
        /// </summary>
        /// <remarks>
        /// Possible values:
        /// <para>none (default): no feedback message is generated.</para>
        /// <para>positive: receive a feedback message if the message was completed.</para>
        /// <para>negative: receive a feedback message if the message expired (or maximum delivery count was reached) without being completed by the device.</para>
        /// <para>full: both positive and negative.</para>
        /// <para>
        /// In order to receive feedback messages on the service client, use <see cref="IotHubServiceClient.MessageFeedback"/>.
        /// </para>
        /// </remarks>
        public DeliveryAcknowledgement Ack
        {
            get
            {
                string deliveryAckAsString = GetSystemProperty<string>(MessageSystemPropertyNames.Ack);

                if (string.IsNullOrWhiteSpace(deliveryAckAsString))
                {
                    throw new IotHubServiceException("Invalid delivery ack mode");
                }

                return deliveryAckAsString switch
                {
                    "none" => DeliveryAcknowledgement.None,
                    "positive" => DeliveryAcknowledgement.PositiveOnly,
                    "negative" => DeliveryAcknowledgement.NegativeOnly,
                    "full" => DeliveryAcknowledgement.Full,
                    _ => throw new IotHubServiceException("Invalid delivery ack mode"),
                };
            }
            set
            {
                string valueToSet = value switch
                {
                    DeliveryAcknowledgement.None => "none",
                    DeliveryAcknowledgement.PositiveOnly => "positive",
                    DeliveryAcknowledgement.NegativeOnly => "negative",
                    DeliveryAcknowledgement.Full => "full",
                    _ => throw new IotHubServiceException("Invalid delivery ack mode"),
                };
                SystemProperties[MessageSystemPropertyNames.Ack] = valueToSet;
            }
        }

        /// <summary>
        /// Gets the dictionary of user properties which are set when user send the data.
        /// </summary>
        public IDictionary<string, string> Properties { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the dictionary of system properties which are managed internally.
        /// </summary>
        internal IDictionary<string, object> SystemProperties { get; private set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The message payload.
        /// </summary>
        public byte[] Payload => _payload ?? Array.Empty<byte>();

        /// <summary>
        /// Indicates if the message has a payload.
        /// </summary>
        /// <returns>True, if there is a payload.</returns>
        public bool HasPayload => _payload != null;

        /// <summary>
        /// Gets or sets the delivery tag which is used for server side checkpointing.
        /// </summary>
        internal ArraySegment<byte> DeliveryTag { get; set; }

        private T GetSystemProperty<T>(string key)
        {
            return SystemProperties.TryGetValue(key, out object value)
                ? (T)value
                : default;
        }
    }
}
