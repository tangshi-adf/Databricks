﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// This application uses the Azure IoT Hub device SDK for .NET
// For samples see: https://github.com/Azure/azure-iot-sdk-csharp/tree/main/iothub/device/samples

using System;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Newtonsoft.Json;

namespace Microsoft.Azure.Devices.Client.Samples
{
    /// <summary>
    /// This sample illustrates the very basics of a device app sending telemetry. For a more comprehensive device app sample, please see
    /// <see href="https://github.com/Azure-Samples/azure-iot-samples-csharp/tree/main/iot-hub/Samples/device/DeviceReconnectionSample"/>.
    /// </summary>
    internal class Program
    {
        private static TimeSpan s_telemetryInterval = TimeSpan.FromSeconds(30);

        private static async Task Main(string[] args)
        {
            Parameters parameters = null;
            ParserResult<Parameters> result = Parser.Default.ParseArguments<Parameters>(args)
                .WithParsed(parsedParams => parameters = parsedParams)
                .WithNotParsed(errors => Environment.Exit(1));

            Console.WriteLine("IoT Hub Quickstarts #1 - Simulated device.");

            var options = new IotHubClientOptions(parameters.GetHubTransportSettings());

            // Connect to the IoT hub using the MQTT protocol by default
            await using var deviceClient = new IotHubDeviceClient(
                parameters.DeviceConnectionString,
                options);

            // Set up a condition to quit the sample
            Console.WriteLine("Press control-C to exit.");
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
                Console.WriteLine("Exiting...");
            };

            await deviceClient.SetDirectMethodCallbackAsync(DirectMethodCallback);

            // Run the telemetry loop
            await SendDeviceToCloudMessagesAsync(deviceClient, cts.Token);

            // SendDeviceToCloudMessagesAsync is designed to run until cancellation has been explicitly requested by Console.CancelKeyPress.
            // As a result, by the time the control reaches the call to close the device client, the cancellation token source would
            // have already had cancellation requested.
            // Hence, if you want to pass a cancellation token to any subsequent calls, a new token needs to be generated.
            // For device client APIs, you can also call them without a cancellation token, which will set a default
            // cancellation timeout of 4 minutes: https://github.com/Azure/azure-iot-sdk-csharp/blob/64f6e9f24371bc40ab3ec7a8b8accbfb537f0fe1/iothub/device/src/InternalClient.cs#L1922
            await deviceClient.CloseAsync();

            Console.WriteLine("Device simulator finished.");
        }
        private static Task<DirectMethodResponse> DirectMethodCallback(DirectMethodRequest methodRequest)
        {
            Console.WriteLine($"Received direct method [{methodRequest.MethodName}] with payload [{methodRequest.GetPayloadAsJsonString()}].");

            switch (methodRequest.MethodName)
            {
                case "SetTelemetryInterval":
                    try
                    {
                        if (methodRequest.TryGetPayload(out int telemetryIntervalSeconds))
                        {
                            s_telemetryInterval = TimeSpan.FromSeconds(telemetryIntervalSeconds);
                            Console.WriteLine($"Setting the telemetry interval to {s_telemetryInterval}.");
                            return Task.FromResult(new DirectMethodResponse(200));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to parse the payload for direct method {methodRequest.MethodName} due to {ex}");
                    }
                    break;
            }

            return Task.FromResult(new DirectMethodResponse(400));
        }

        // Async method to send simulated telemetry
        private static async Task SendDeviceToCloudMessagesAsync(IotHubDeviceClient deviceClient, CancellationToken ct)
        {
            // Initial telemetry values
            double minTemperature = 20;
            double minHumidity = 60;
            var rand = new Random();

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    double currentTemperature = minTemperature + rand.NextDouble() * 15;
                    double currentHumidity = minHumidity + rand.NextDouble() * 20;

                    var telemetryDataPoint = new
                    {
                        temperature = currentTemperature,
                        humidity = currentHumidity,
                    };
                    var message = new TelemetryMessage(telemetryDataPoint);

                    // Add a custom application property to the message.
                    // An IoT hub can filter on these properties without access to the message body.
                    message.Properties.Add("temperatureAlert", (currentTemperature > 30) ? "true" : "false");

                    await deviceClient.OpenAsync(ct);
                    // Send the telemetry message
                    await deviceClient.SendTelemetryAsync(message, ct);
                    Console.WriteLine($"{DateTime.Now} > Sending message: {JsonConvert.SerializeObject(telemetryDataPoint)}");

                    await Task.Delay(s_telemetryInterval, ct);
                }
            }
            catch (TaskCanceledException) { } // ct was signaled
        }
    }
}