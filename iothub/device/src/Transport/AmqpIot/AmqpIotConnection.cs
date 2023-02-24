﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Azure.Devices.Client.Transport.Amqp;

namespace Microsoft.Azure.Devices.Client.Transport.AmqpIot
{
    internal sealed class AmqpIotConnection
    {
        public event EventHandler Closed;

        private readonly AmqpConnection _amqpConnection;
        private readonly AmqpIotCbsLink _amqpIotCbsLink;

        internal AmqpIotConnection(AmqpConnection amqpConnection)
        {
            _amqpConnection = amqpConnection;
            _amqpIotCbsLink = new AmqpIotCbsLink(new AmqpCbsLink(amqpConnection));
        }

        internal AmqpIotCbsLink GetCbsLink()
        {
            return _amqpIotCbsLink;
        }

        // This event handler is not invoked by the AMQP library in an async fashion.
        // This also co-relates with the fact that AmqpConnection.SafeClose() is a sync method.
        internal void AmqpConnectionClosed(object sender, EventArgs e)
        {
            if (Logging.IsEnabled)
                Logging.Enter(this, nameof(AmqpConnectionClosed));

            Closed?.Invoke(this, e);

            // After the Closed event handler has been invoked, the AmqpConnection has now been effectively cleaned up.
            // This is a good point for us to detach the Closed event handler from the AmqpConnection instance.
            _amqpConnection.Closed -= AmqpConnectionClosed;

            if (Logging.IsEnabled)
                Logging.Exit(this, nameof(AmqpConnectionClosed));
        }

        internal async Task<AmqpIotSession> OpenSessionAsync(CancellationToken cancellationToken)
        {
            if (_amqpConnection.IsClosing())
            {
                throw new IotHubClientException("Amqp connection is disconnected.", IotHubClientErrorCode.NetworkErrors);
            }

            var amqpSessionSettings = new AmqpSessionSettings
            {
                Properties = new Fields(),
            };

            try
            {
                var amqpSession = new AmqpSession(_amqpConnection, amqpSessionSettings, AmqpIotLinkFactory.Instance);
                _amqpConnection.AddSession(amqpSession, new ushort?());
                await amqpSession.OpenAsync(cancellationToken).ConfigureAwait(false);
                return new AmqpIotSession(amqpSession);
            }
            catch (Exception ex) when (!Fx.IsFatal(ex))
            {
                Exception convertedEx = AmqpIotExceptionAdapter.ConvertToIotHubException(ex, _amqpConnection);
                if (ReferenceEquals(ex, convertedEx))
                {
                    throw;
                }

                if (convertedEx is IotHubClientException hubEx && hubEx.InnerException is AmqpException)
                {
                    _amqpConnection.SafeClose();
                    throw convertedEx;
                }

                throw convertedEx;
            }
        }

        internal async Task<IAmqpAuthenticationRefresher> CreateRefresherAsync(IConnectionCredentials connectionCredentials, CancellationToken cancellationToken)
        {
            if (_amqpConnection.IsClosing())
            {
                throw new IotHubClientException("Amqp connection is disconnected.", IotHubClientErrorCode.NetworkErrors);
            }

            try
            {
                IAmqpAuthenticationRefresher amqpAuthenticator = new AmqpAuthenticationRefresher(connectionCredentials, _amqpIotCbsLink);
                await amqpAuthenticator.RefreshSasTokenAsync(cancellationToken).ConfigureAwait(false);

                return amqpAuthenticator;
            }
            catch (Exception ex) when (!Fx.IsFatal(ex))
            {
                Exception iotEx = AmqpIotExceptionAdapter.ConvertToIotHubException(ex, _amqpConnection);
                if (ReferenceEquals(ex, iotEx))
                {
                    throw;
                }

                throw iotEx;
            }
        }

        internal void SafeClose()
        {
            _amqpConnection.SafeClose();
        }

        internal bool IsClosing()
        {
            return _amqpConnection.IsClosing();
        }
    }
}
