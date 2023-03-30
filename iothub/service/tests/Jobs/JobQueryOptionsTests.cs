﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.Devices.Tests.Jobs
{
    [TestClass]
    [TestCategory("Unit")]
    public class JobQueryOptionsTests
    {
        [TestMethod]
        public void JobQueryOptions_FieldValueInitializations()
        {
            // arrange
            var options = new JobQueryOptions();

            // assert
            options.JobType.Should().BeNull();
            options.JobStatus.Should().BeNull();

            // rearrange
            var jobType = new JobType();
            var jobStatus = new JobStatus();
            options.JobType = jobType;
            options.JobStatus = jobStatus;

            // reassert
            options.JobType.Should().Be(jobType);
            options.JobStatus.Should().Be(jobStatus);
        }
    }
}