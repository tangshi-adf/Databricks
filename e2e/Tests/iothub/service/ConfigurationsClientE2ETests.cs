﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure;
using FluentAssertions;
using FluentAssertions.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.Devices.E2ETests.IotHub.Service
{
    /// <summary>
    /// E2E test class for configurations.
    /// </summary>
    [TestClass]
    [TestCategory("E2E")]
    [TestCategory("IoTHub-Service")]
    public class ConfigurationsClientE2ETests : E2EMsTestBase
    {
        private readonly string _idPrefix = $"{nameof(ConfigurationsClientE2ETests)}_";

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task ConfigurationOperations_Work()
        {
            // arrange

            bool configCreated = false;
            string configurationId = (_idPrefix + Guid.NewGuid()).ToLower(); // Configuration Id characters must be all lower-case.
            using var sc = new IotHubServiceClient(TestConfiguration.IotHub.ConnectionString);

            try
            {
                var expected = new Configuration(configurationId)
                {
                    Priority = 2,
                    Labels = { { "labelName", "labelValue" } },
                    TargetCondition = "deviceId='fakeDevice'",
                    Content =
                    {
                        DeviceContent = { { "properties.desired.x", 4L } },
                    },
                    Metrics =
                    {
                        Queries = { { "successfullyConfigured", "select deviceId from devices where properties.reported.x = 4" } }
                    },
                };

                // act and assert

                Configuration addResult = await sc.Configurations.CreateAsync(expected).ConfigureAwait(false);
                configCreated = true;
                addResult.Id.Should().Be(configurationId);
                addResult.Priority.Should().Be(expected.Priority);
                addResult.TargetCondition.Should().Be(expected.TargetCondition);
                addResult.Content.DeviceContent.First().Should().Be(expected.Content.DeviceContent.First());
                addResult.Metrics.Queries.First().Should().Be(expected.Metrics.Queries.First());
                addResult.ETag.ToString().Should().NotBeNullOrEmpty();

                Configuration getResult = await sc.Configurations.GetAsync(configurationId).ConfigureAwait(false);
                getResult.Id.Should().Be(configurationId);
                getResult.Priority.Should().Be(expected.Priority);
                getResult.TargetCondition.Should().Be(expected.TargetCondition);
                getResult.Content.DeviceContent.First().Should().Be(expected.Content.DeviceContent.First());
                getResult.Metrics.Queries.First().Should().Be(expected.Metrics.Queries.First());
                getResult.ETag.Should().Be(addResult.ETag);

                IEnumerable<Configuration> listResult = await sc.Configurations.GetAsync(100).ConfigureAwait(false);
                listResult.Should().Contain(x => x.Id == configurationId);

                expected.Priority++;
                expected.ETag = getResult.ETag;
                Configuration updateResult = await sc.Configurations.SetAsync(expected).ConfigureAwait(false);
                updateResult.Id.Should().Be(configurationId);
                updateResult.Priority.Should().Be(expected.Priority);
                updateResult.TargetCondition.Should().Be(expected.TargetCondition);
                updateResult.Content.DeviceContent.First().Should().Be(expected.Content.DeviceContent.First());
                updateResult.Metrics.Queries.First().Should().Be(expected.Metrics.Queries.First());
                updateResult.ETag.ToString()
                    .Should().NotBeNullOrEmpty()
                    .And
                    .NotBe(getResult.ETag.ToString(), "The ETag should have changed after update");
            }
            finally
            {
                if (configCreated)
                {
                    // If this fails, we shall let it throw an exception and fail the test
                    await sc.Configurations.DeleteAsync(configurationId).ConfigureAwait(false);
                }
            }
        }

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task ConfigurationsClient_SetETag_Works()
        {
            using var serviceClient = new IotHubServiceClient(TestConfiguration.IotHub.ConnectionString);
            string configurationId = (_idPrefix + Guid.NewGuid()).ToLower(); // Configuration Id characters must be all lower-case.
            try
            {
                var configuration = new Configuration(configurationId)
                {
                    Priority = 2,
                    Labels = { { "labelName", "labelValue" } },
                    TargetCondition = "deviceId='fakeDevice'",
                };

                configuration = await serviceClient.Configurations.CreateAsync(configuration).ConfigureAwait(false);
                ETag oldEtag = configuration.ETag;

                configuration.Priority = 3;

                configuration = await serviceClient.Configurations.SetAsync(configuration).ConfigureAwait(false);
                configuration.ETag = oldEtag;

                // set the 'onlyIfUnchanged' flag to true to check that, with an out of date ETag, the request throws a PreconditionFailedException.
                Func<Task> act = async () => await serviceClient.Configurations.SetAsync(configuration, true).ConfigureAwait(false);
                ExceptionAssertions<IotHubServiceException> error = await act.Should().ThrowAsync<IotHubServiceException>(
                    "Expected test to throw a precondition failed exception since it updated a configuration with an out of date ETag");
                error.And.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
                error.And.ErrorCode.Should().Be(IotHubServiceErrorCode.PreconditionFailed);
                error.And.IsTransient.Should().BeFalse();

                // set the 'onlyIfUnchanged' flag to false to check that, even with an out of date ETag, the request performs without exception.
                await FluentActions
                    .Invoking(async () => configuration = await serviceClient.Configurations.SetAsync(configuration, false).ConfigureAwait(false))
                    .Should()
                    .NotThrowAsync<IotHubServiceException>("Did not expect test to throw a precondition failed exception since 'onlyIfUnchanged' was set to false");

                // set the 'onlyIfUnchanged' flag to true to check that, with an out of date ETag, the request throws a PreconditionFailedException.
                configuration.Priority = 2;
                await FluentActions
                    .Invoking(async () => await serviceClient.Configurations.SetAsync(configuration, true).ConfigureAwait(false))
                    .Should()
                    .NotThrowAsync<IotHubServiceException>("Did not expect test to throw a precondition failed exception since 'onlyIfUnchanged' was set to true");
            }
            finally
            {
                try
                {
                    // If this fails, it won't fail the test
                    await serviceClient.Configurations.DeleteAsync(configurationId).ConfigureAwait(false);
                }
                catch (IotHubServiceException ex) when (ex.ErrorCode is IotHubServiceErrorCode.DeviceNotFound)
                {
                    // configuration was already deleted during the normal test flow
                }
                catch (Exception ex)
                {
                    VerboseTestLogger.WriteLine($"Failed to clean up configuration due to {ex}");
                }
            }
        }

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task ConfigurationsClient_DeleteETag_Works()
        {
            using var serviceClient = new IotHubServiceClient(TestConfiguration.IotHub.ConnectionString);
            string configurationId = (_idPrefix + Guid.NewGuid()).ToLower(); // Configuration Id characters must be all lower-case.
            try
            {
                var configuration = new Configuration(configurationId)
                {
                    Priority = 2,
                    Labels = { { "labelName", "labelValue" } },
                    TargetCondition = "deviceId='fakeDevice'",
                };

                configuration = await serviceClient.Configurations.CreateAsync(configuration).ConfigureAwait(false);
                ETag oldEtag = configuration.ETag;

                configuration.Priority = 3;

                configuration = await serviceClient.Configurations.SetAsync(configuration).ConfigureAwait(false);

                configuration.ETag = oldEtag;

                // set the 'onlyIfUnchanged' flag to true to check that, with an out of date ETag, the request throws a PreconditionFailedException.
                Func<Task> act = async () => await serviceClient.Configurations.DeleteAsync(configuration, true).ConfigureAwait(false);
                ExceptionAssertions<IotHubServiceException> error = await act.Should().ThrowAsync<IotHubServiceException>(
                    "Expected test to throw a precondition failed exception since it updated a configuration with an out of date ETag");
                error.And.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
                error.And.ErrorCode.Should().Be(IotHubServiceErrorCode.PreconditionFailed);
                error.And.IsTransient.Should().BeFalse();

                // set the 'onlyIfUnchanged' flag to false to check that, even with an out of date ETag, the request performs without exception.
                await FluentActions
                    .Invoking(async () => await serviceClient.Configurations.DeleteAsync(configuration, false).ConfigureAwait(false))
                    .Should()
                    .NotThrowAsync<IotHubServiceException>("Did not expect test to throw a precondition failed exception since 'onlyIfUnchanged' was set to false");
            }
            finally
            {
                try
                {
                    await serviceClient.Configurations.DeleteAsync(configurationId).ConfigureAwait(false);
                }
                catch (IotHubServiceException ex)
                    when (ex.StatusCode is HttpStatusCode.NotFound && ex.ErrorCode is IotHubServiceErrorCode.DeviceNotFound)
                {
                    // configuration was already deleted during the normal test flow
                }
                catch (Exception ex)
                {
                    VerboseTestLogger.WriteLine($"Failed to clean up configuration due to {ex}");
                }
            }
        }

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task ConfigurationsClient_ClearLabels()
        {
            // arrange

            bool configCreated = false;
            string configurationId = (_idPrefix + Guid.NewGuid()).ToLower(); // Configuration Id characters must be all lower-case.
            using var sc = new IotHubServiceClient(TestConfiguration.IotHub.ConnectionString);

            try
            {
                var expected = new Configuration(configurationId)
                {
                    Priority = 3,
                    Labels = { { "labelName", "labelValue" } },
                    TargetCondition = "deviceId='fakeDevice'",
                };
                Configuration addResult = await sc.Configurations.CreateAsync(expected).ConfigureAwait(false);
                configCreated = true;

                // act

                // Labels gets a default dictionary instance, and we want to make sure that if we clear them out, the empty
                // list gets serialized and the labels on the service-side object indeed get removed.
                addResult.Labels.Clear();

                Configuration updateResult = await sc.Configurations.SetAsync(addResult, false).ConfigureAwait(false);

                // assert
                updateResult.Labels.Should().BeEmpty();
            }
            finally
            {
                if (configCreated)
                {
                    // If this fails, we shall let it throw an exception and fail the test
                    await sc.Configurations.DeleteAsync(configurationId).ConfigureAwait(false);
                }
            }
        }

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task ConfigurationsClient_NullOutLabels()
        {
            // arrange

            bool configCreated = false;
            string configurationId = (_idPrefix + Guid.NewGuid()).ToLower(); // Configuration Id characters must be all lower-case.
            using var sc = new IotHubServiceClient(TestConfiguration.IotHub.ConnectionString);

            try
            {
                var expected = new Configuration(configurationId)
                {
                    Priority = 3,
                    Labels = { { "labelName", "labelValue" } },
                    TargetCondition = "deviceId='fakeDevice'",
                };
                Configuration addResult = await sc.Configurations.CreateAsync(expected).ConfigureAwait(false);
                configCreated = true;

                // act

                // Labels gets a default dictionary instance, and we want to make sure that if we set it to null, it
                // gets serialized and the labels on the service-side object indeed get removed.
                addResult.Labels = null;

                Configuration updateResult = await sc.Configurations.SetAsync(addResult, false).ConfigureAwait(false);

                // assert
                updateResult.Labels.Should().BeNull();
            }
            finally
            {
                if (configCreated)
                {
                    // If this fails, we shall let it throw an exception and fail the test
                    await sc.Configurations.DeleteAsync(configurationId).ConfigureAwait(false);
                }
            }
        }
    }
}