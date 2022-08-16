﻿using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.Devices.E2ETests.iothub.service
{
    /// <summary>
    /// E2E test class for PurgeMesageQueue.
    /// </summary>
    [TestClass]
    [TestCategory("E2E")]
    [TestCategory("IoTHub")]
    public class PurgeMesageQueueE2ETests : E2EMsTestBase
    {
        [TestMethod]
        public async Task PurgeMessageQueueOperation()
        {
            using Message testMessage = ComposeD2CTestMessage();
            using var sc = new IotHubServiceClient(TestConfiguration.IoTHub.ConnectionString);
            var deviceId = TestConfiguration.IoTHub.X509ChainDeviceName;
            var expectedResult = new PurgeMessageQueueResult()
            {
                DeviceId = deviceId,
                TotalMessagesPurged = 3
            };
            for (int i = 0; i < 3; ++i)
            {
                await sc.Messaging.SendAsync(deviceId, testMessage);
            }
            PurgeMessageQueueResult result = await sc.Messaging.PurgeMessageQueueAsync(deviceId, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(expectedResult.DeviceId, result.DeviceId);
            Assert.AreEqual(expectedResult.TotalMessagesPurged, result.TotalMessagesPurged);
        }

        private Message ComposeD2CTestMessage()
        {
            return new Message(Encoding.UTF8.GetBytes("some payload"));
        }
    }
}
