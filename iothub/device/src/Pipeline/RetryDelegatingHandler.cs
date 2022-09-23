// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Client.Transport
{
    internal class RetryDelegatingHandler : DefaultDelegatingHandler
    {
        // RetryCount is used for testing purpose and is equal to MaxValue in prod.
        private const int RetryMaxCount = int.MaxValue;

        private RetryPolicy _internalRetryPolicy;

        private SemaphoreSlim _handlerSemaphore = new SemaphoreSlim(1, 1);
        private bool _openCalled;
        private bool _opened;
        private bool _methodsEnabled;
        private bool _twinEnabled;
        private bool _eventsEnabled;
        private bool _deviceReceiveMessageEnabled;
        private bool _isAnEdgeModule = true;

        private Task _transportClosedTask;
        private readonly CancellationTokenSource _handleDisconnectCts = new CancellationTokenSource();

        private readonly Action<ConnectionStatusInfo> _onConnectionStatusChanged;

        public RetryDelegatingHandler(PipelineContext context, IDelegatingHandler innerHandler)
            : base(context, innerHandler)
        {
            IRetryPolicy defaultRetryStrategy = new ExponentialBackoff(
                retryCount: RetryMaxCount,
                minBackoff: TimeSpan.FromMilliseconds(100),
                maxBackoff: TimeSpan.FromSeconds(10),
                deltaBackoff: TimeSpan.FromMilliseconds(100));

            _internalRetryPolicy = new RetryPolicy(new TransientErrorStrategy(), new RetryStrategyAdapter(defaultRetryStrategy));
            _onConnectionStatusChanged = context.ConnectionStatusChangeHandler;

            if (Logging.IsEnabled)
                Logging.Associate(this, _internalRetryPolicy, nameof(SetRetryPolicy));
        }

        private class TransientErrorStrategy : ITransientErrorDetectionStrategy
        {
            public bool IsTransient(Exception ex)
            {
                return ex is IotHubClientException exception && exception.IsTransient;
            }
        }

        public virtual void SetRetryPolicy(IRetryPolicy retryPolicy)
        {
            _internalRetryPolicy = new RetryPolicy(
                new TransientErrorStrategy(),
                new RetryStrategyAdapter(retryPolicy));

            if (Logging.IsEnabled)
                Logging.Associate(this, _internalRetryPolicy, nameof(SetRetryPolicy));
        }

        public override async Task SendEventAsync(Message message, CancellationToken cancellationToken)
        {
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, message, cancellationToken, nameof(SendEventAsync));

                await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            await base.SendEventAsync(message, cancellationToken).ConfigureAwait(false);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, message, cancellationToken, nameof(SendEventAsync));
            }
        }

        public override async Task SendEventAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, messages, cancellationToken, nameof(SendEventAsync));

                await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            await base.SendEventAsync(messages, cancellationToken).ConfigureAwait(false);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, messages, cancellationToken, nameof(SendEventAsync));
            }
        }

        public override async Task SendMethodResponseAsync(DirectMethodResponse method, CancellationToken cancellationToken)
        {
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, method, cancellationToken, nameof(SendMethodResponseAsync));

                await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            await base.SendMethodResponseAsync(method, cancellationToken).ConfigureAwait(false);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, method, cancellationToken, nameof(SendMethodResponseAsync));
            }
        }

        public override async Task<Message> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, cancellationToken, nameof(ReceiveMessageAsync));

                return await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            return await base.ReceiveMessageAsync(cancellationToken).ConfigureAwait(false);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, cancellationToken, nameof(ReceiveMessageAsync));
            }
        }

        public override async Task EnableReceiveMessageAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, cancellationToken, nameof(EnableReceiveMessageAsync));

                await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            // Wait to acquire the _handlerSemaphore. This ensures that concurrently invoked API calls are invoked in a thread-safe manner.
                            await _handlerSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                            try
                            {
                                // The telemetry downlink needs to be enabled only for the first time that the callback is set.
                                Debug.Assert(!_deviceReceiveMessageEnabled);
                                await base.EnableReceiveMessageAsync(cancellationToken).ConfigureAwait(false);
                                _deviceReceiveMessageEnabled = true;
                            }
                            finally
                            {
                                _handlerSemaphore?.Release();
                            }
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, cancellationToken, nameof(EnableReceiveMessageAsync));
            }
        }

        public override async Task DisableReceiveMessageAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, cancellationToken, nameof(DisableReceiveMessageAsync));

                await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            // Wait to acquire the _handlerSemaphore. This ensures that concurrently invoked API calls are invoked in a thread-safe manner.
                            await _handlerSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                            try
                            {
                                // Ensure that a callback for receiving messages has been previously set.
                                Debug.Assert(_deviceReceiveMessageEnabled);
                                await base.DisableReceiveMessageAsync(cancellationToken).ConfigureAwait(false);
                                _deviceReceiveMessageEnabled = false;
                            }
                            finally
                            {
                                _handlerSemaphore?.Release();
                            }
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, cancellationToken, nameof(DisableReceiveMessageAsync));
            }
        }

        public override async Task EnableMethodsAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, cancellationToken, nameof(EnableMethodsAsync));

                await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            await _handlerSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                            try
                            {
                                Debug.Assert(!_methodsEnabled);
                                await base.EnableMethodsAsync(cancellationToken).ConfigureAwait(false);
                                _methodsEnabled = true;
                            }
                            finally
                            {
                                _handlerSemaphore?.Release();
                            }
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, cancellationToken, nameof(EnableMethodsAsync));
            }
        }

        public override async Task DisableMethodsAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, cancellationToken, nameof(DisableMethodsAsync));

                await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            await _handlerSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                            try
                            {
                                Debug.Assert(_methodsEnabled);
                                await base.DisableMethodsAsync(cancellationToken).ConfigureAwait(false);
                                _methodsEnabled = false;
                            }
                            finally
                            {
                                _handlerSemaphore?.Release();
                            }
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, cancellationToken, nameof(DisableMethodsAsync));
            }
        }

        public override async Task EnableEventReceiveAsync(bool isAnEdgeModule, CancellationToken cancellationToken)
        {
            try
            {
                _isAnEdgeModule = isAnEdgeModule;
                if (Logging.IsEnabled)
                    Logging.Enter(this, cancellationToken, nameof(EnableEventReceiveAsync));

                await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            await _handlerSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                            try
                            {
                                await base.EnableEventReceiveAsync(isAnEdgeModule, cancellationToken).ConfigureAwait(false);
                                Debug.Assert(!_eventsEnabled);
                                _eventsEnabled = true;
                            }
                            finally
                            {
                                _handlerSemaphore?.Release();
                            }
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, cancellationToken, nameof(EnableEventReceiveAsync));
            }
        }

        public override async Task DisableEventReceiveAsync(bool isAnEdgeModule, CancellationToken cancellationToken)
        {
            try
            {
                _isAnEdgeModule = isAnEdgeModule;
                if (Logging.IsEnabled)
                    Logging.Enter(this, cancellationToken, nameof(DisableEventReceiveAsync));

                await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            await _handlerSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                            try
                            {
                                Debug.Assert(_eventsEnabled);
                                await base.DisableEventReceiveAsync(isAnEdgeModule, cancellationToken).ConfigureAwait(false);
                                _eventsEnabled = false;
                            }
                            finally
                            {
                                _handlerSemaphore?.Release();
                            }
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, cancellationToken, nameof(DisableEventReceiveAsync));
            }
        }

        public override async Task EnableTwinPatchAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, cancellationToken, nameof(EnableTwinPatchAsync));

                await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            await _handlerSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                            try
                            {
                                Debug.Assert(!_twinEnabled);
                                await base.EnableTwinPatchAsync(cancellationToken).ConfigureAwait(false);
                                _twinEnabled = true;
                            }
                            finally
                            {
                                _handlerSemaphore?.Release();
                            }
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, cancellationToken, nameof(EnableTwinPatchAsync));
            }
        }

        public override async Task DisableTwinPatchAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, cancellationToken, nameof(DisableTwinPatchAsync));

                await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            await _handlerSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                            try
                            {
                                Debug.Assert(_twinEnabled);
                                await base.DisableTwinPatchAsync(cancellationToken).ConfigureAwait(false);
                                _twinEnabled = false;
                            }
                            finally
                            {
                                _handlerSemaphore?.Release();
                            }
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, cancellationToken, nameof(DisableTwinPatchAsync));
            }
        }

        public override async Task<Twin> SendTwinGetAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, cancellationToken, nameof(SendTwinGetAsync));

                return await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            return await base.SendTwinGetAsync(cancellationToken).ConfigureAwait(false);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, cancellationToken, nameof(SendTwinGetAsync));
            }
        }

        public override async Task<long> SendTwinPatchAsync(TwinCollection reportedProperties, CancellationToken cancellationToken)
        {
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, reportedProperties, cancellationToken, nameof(SendTwinPatchAsync));

                return await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            return await base.SendTwinPatchAsync(reportedProperties, cancellationToken).ConfigureAwait(false);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, reportedProperties, cancellationToken, nameof(SendTwinPatchAsync));
            }
        }

        public override async Task CompleteMessageAsync(string lockToken, CancellationToken cancellationToken)
        {
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, lockToken, cancellationToken, nameof(CompleteMessageAsync));

                await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            await base.CompleteMessageAsync(lockToken, cancellationToken).ConfigureAwait(false);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, lockToken, cancellationToken, nameof(CompleteMessageAsync));
            }
        }

        public override async Task AbandonMessageAsync(string lockToken, CancellationToken cancellationToken)
        {
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, lockToken, cancellationToken, nameof(AbandonMessageAsync));

                await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            await base.AbandonMessageAsync(lockToken, cancellationToken).ConfigureAwait(false);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, lockToken, cancellationToken, nameof(AbandonMessageAsync));
            }
        }

        public override async Task RejectMessageAsync(string lockToken, CancellationToken cancellationToken)
        {
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, lockToken, cancellationToken, nameof(RejectMessageAsync));

                await _internalRetryPolicy
                    .RunWithRetryAsync(
                        async () =>
                        {
                            await EnsureOpenedAsync(cancellationToken).ConfigureAwait(false);
                            await base.RejectMessageAsync(lockToken, cancellationToken).ConfigureAwait(false);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, lockToken, cancellationToken, nameof(RejectMessageAsync));
            }
        }

        public override async Task OpenAsync(CancellationToken cancellationToken)
        {
            // If this object has already been disposed, we will throw an exception indicating that.
            // This is the entry point for interacting with the client and this safety check should be done here.
            // The current behavior does not support open->close->open
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RetryDelegatingHandler));
            }

            if (Volatile.Read(ref _opened))
            {
                return;
            }

            await _handlerSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_opened)
                {
                    if (Logging.IsEnabled)
                        Logging.Info(this, "Opening connection", nameof(OpenAsync));

                    // This is to ensure that if OpenInternalAsync() fails on retry expiration with a custom retry policy,
                    // we are returning the corresponding connection status change event => disconnected: retry_expired.
                    try
                    {
                        await OpenInternalAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!Fx.IsFatal(ex))
                    {
                        HandleConnectionStatusExceptions(ex, true);
                        throw;
                    }

                    if (!_disposed)
                    {
                        _opened = true;
                        _openCalled = true;

                        // Send the request for transport close notification.
                        _transportClosedTask = HandleDisconnectAsync();
                    }
                    else
                    {
                        if (Logging.IsEnabled)
                            Logging.Info(this, "Race condition: Disposed during opening.", nameof(OpenAsync));

                        _handleDisconnectCts.Cancel();
                    }
                }
            }
            finally
            {
                _handlerSemaphore?.Release();
            }
        }

        public override async Task CloseAsync(CancellationToken cancellationToken)
        {
            await _handlerSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (Logging.IsEnabled)
                    Logging.Enter(this, cancellationToken, nameof(CloseAsync));

                if (!_openCalled)
                {
                    // Already closed so gracefully exit, instead of throw.
                    return;
                }

                _handleDisconnectCts.Cancel();
                await base.CloseAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (Logging.IsEnabled)
                    Logging.Exit(this, cancellationToken, nameof(CloseAsync));

                _handlerSemaphore?.Release();
                Dispose(true);
            }
        }

        private async Task EnsureOpenedAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _handlerSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (!_opened)
                {
                    throw new InvalidOperationException(_openCalled
                        ? $"The transport has disconnected; call '{nameof(OpenAsync)}' to reconnect."
                        : $"The client connection must be opened before operations can begin. Call '{nameof(OpenAsync)}' and try again.");
                }
            }
            finally
            {
                _handlerSemaphore?.Release();
            }
        }

        private async Task OpenInternalAsync(CancellationToken cancellationToken)
        {
            var connectionStatusInfo = new ConnectionStatusInfo();

            await _internalRetryPolicy
                .RunWithRetryAsync(
                    async () =>
                    {
                        try
                        {
                            if (Logging.IsEnabled)
                                Logging.Enter(this, cancellationToken, nameof(OpenAsync));

                            // Will throw on error.
                            await base.OpenAsync(cancellationToken).ConfigureAwait(false);

                            connectionStatusInfo = new ConnectionStatusInfo(ConnectionStatus.Connected, ConnectionStatusChangeReason.ConnectionOk);
                            _onConnectionStatusChanged(connectionStatusInfo);
                        }
                        catch (Exception ex) when (!Fx.IsFatal(ex))
                        {
                            HandleConnectionStatusExceptions(ex);
                            throw;
                        }
                        finally
                        {
                            if (Logging.IsEnabled)
                                Logging.Exit(this, cancellationToken, nameof(OpenAsync));
                        }
                    },
                    cancellationToken).ConfigureAwait(false);
        }

        // Triggered from connection loss event
        private async Task HandleDisconnectAsync()
        {
            var connectionStatusInfo = new ConnectionStatusInfo();

            if (_disposed)
            {
                if (Logging.IsEnabled)
                    Logging.Info(this, "Disposed during disconnection.", nameof(HandleDisconnectAsync));

                _handleDisconnectCts.Cancel();
            }

            try
            {
                // No timeout on connection being established.
                await WaitForTransportClosedAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Canceled when the transport is being closed by the application.
                if (Logging.IsEnabled)
                    Logging.Info(this, "Transport disconnected: closed by application.", nameof(HandleDisconnectAsync));

                connectionStatusInfo = new ConnectionStatusInfo(ConnectionStatus.Closed, ConnectionStatusChangeReason.ClientClosed);
                _onConnectionStatusChanged(connectionStatusInfo);
                return;
            }

            if (Logging.IsEnabled)
                Logging.Info(this, "Transport disconnected: unexpected.", nameof(HandleDisconnectAsync));

            await _handlerSemaphore.WaitAsync().ConfigureAwait(false);
            _opened = false;

            try
            {
                // This is used to ensure that when NoRetry() policy is enabled, we should not be retrying.
                if (!_internalRetryPolicy.RetryStrategy.GetShouldRetry().Invoke(0, new IotHubClientException(true, IotHubStatusCode.NetworkErrors), out TimeSpan delay))
                {
                    if (Logging.IsEnabled)
                        Logging.Info(this, "Transport disconnected: closed by application.", nameof(HandleDisconnectAsync));

                    connectionStatusInfo = new ConnectionStatusInfo(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.RetryExpired);
                    _onConnectionStatusChanged(connectionStatusInfo);
                    return;
                }

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay).ConfigureAwait(false);
                }

                // always reconnect.
                connectionStatusInfo = new ConnectionStatusInfo(ConnectionStatus.DisconnectedRetrying, ConnectionStatusChangeReason.CommunicationError);
                _onConnectionStatusChanged(connectionStatusInfo);
                CancellationToken cancellationToken = _handleDisconnectCts.Token;

                // This will recover to the status before the disconnect.
                await _internalRetryPolicy.RunWithRetryAsync(async () =>
                {
                    if (Logging.IsEnabled)
                        Logging.Info(this, "Attempting to recover subscriptions.", nameof(HandleDisconnectAsync));

                    await base.OpenAsync(cancellationToken).ConfigureAwait(false);

                    var tasks = new List<Task>(4);

                    // This is to ensure that, if previously enabled, the callback to receive direct methods is recovered.
                    if (_methodsEnabled)
                    {
                        tasks.Add(base.EnableMethodsAsync(cancellationToken));
                    }

                    // This is to ensure that, if previously enabled, the callback to receive twin properties is recovered.
                    if (_twinEnabled)
                    {
                        tasks.Add(base.EnableTwinPatchAsync(cancellationToken));
                    }

                    // This is to ensure that, if previously enabled, the callback to receive events for modules is recovered.
                    if (_eventsEnabled)
                    {
                        tasks.Add(base.EnableEventReceiveAsync(_isAnEdgeModule, cancellationToken));
                    }

                    // This is to ensure that, if previously enabled, the callback to receive C2D messages is recovered.
                    if (_deviceReceiveMessageEnabled)
                    {
                        tasks.Add(base.EnableReceiveMessageAsync(cancellationToken));
                    }

                    if (tasks.Any())
                    {
                        await Task.WhenAll(tasks).ConfigureAwait(false);
                    }

                    // Send the request for transport close notification.
                    _transportClosedTask = HandleDisconnectAsync();

                    _opened = true;
                    connectionStatusInfo = new ConnectionStatusInfo(ConnectionStatus.Connected, ConnectionStatusChangeReason.ConnectionOk);
                    _onConnectionStatusChanged(connectionStatusInfo);

                    if (Logging.IsEnabled)
                        Logging.Info(this, "Subscriptions recovered.", nameof(HandleDisconnectAsync));
                },
                cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (Logging.IsEnabled)
                    Logging.Error(this, ex.ToString(), nameof(HandleDisconnectAsync));

                HandleConnectionStatusExceptions(ex, true);
            }
            finally
            {
                _handlerSemaphore?.Release();
            }
        }

        // The retryAttemptsExhausted flag differentiates between calling this method while still retrying
        // vs calling this when no more retry attempts are being made.
        private void HandleConnectionStatusExceptions(Exception exception, bool retryAttemptsExhausted = false)
        {
            if (Logging.IsEnabled)
                Logging.Info(
                    this,
                    $"Received exception: {exception}, retryAttemptsExhausted={retryAttemptsExhausted}",
                    nameof(HandleConnectionStatusExceptions));

            ConnectionStatusChangeReason reason = ConnectionStatusChangeReason.CommunicationError;
            ConnectionStatus status = ConnectionStatus.Disconnected;

            if (exception is IotHubClientException hubException)
            {
                if (hubException.IsTransient)
                {
                    if (retryAttemptsExhausted)
                    {
                        reason = ConnectionStatusChangeReason.RetryExpired;
                    }
                    else
                    {
                        status = ConnectionStatus.DisconnectedRetrying;
                    }
                }
                else if (hubException.StatusCode is IotHubStatusCode.Unauthorized)
                {
                    reason = ConnectionStatusChangeReason.BadCredential;
                }
                else if (hubException.StatusCode is IotHubStatusCode.DeviceNotFound)
                {
                    reason = ConnectionStatusChangeReason.DeviceDisabled;
                }
            }

            _onConnectionStatusChanged(new ConnectionStatusInfo(status, reason));
            if (Logging.IsEnabled)
                Logging.Info(
                    this,
                    $"Connection status change: status={status}, reason={reason}",
                    nameof(HandleConnectionStatusExceptions));
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (Logging.IsEnabled)
                {
                    Logging.Enter(this, $"{nameof(DefaultDelegatingHandler)}.Disposed={_disposed}; disposing={disposing}", $"{nameof(RetryDelegatingHandler)}.{nameof(Dispose)}");
                }

                if (!_disposed)
                {
                    base.Dispose(disposing);
                    if (disposing)
                    {
                        _handleDisconnectCts?.Cancel();
                        _handleDisconnectCts?.Dispose();
                        if (_handlerSemaphore != null && _handlerSemaphore.CurrentCount == 0)
                        {
                            _handlerSemaphore.Release();
                        }
                        _handlerSemaphore?.Dispose();
                        _handlerSemaphore = null;
                    }

                    // the _disposed flag is inherited from the base class DefaultDelegatingHandler and is finally set to null there.
                }
            }
            finally
            {
                if (Logging.IsEnabled)
                {
                    Logging.Exit(this, $"{nameof(DefaultDelegatingHandler)}.Disposed={_disposed}; disposing={disposing}", $"{nameof(RetryDelegatingHandler)}.{nameof(Dispose)}");
                }
            }
        }
    }
}
