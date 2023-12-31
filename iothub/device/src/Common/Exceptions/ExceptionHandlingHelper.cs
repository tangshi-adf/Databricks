﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Client.Exceptions
{
    internal class ExceptionHandlingHelper
    {
        public static IDictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>> GetDefaultErrorMapping()
        {
            var mappings = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>();

            mappings.Add(HttpStatusCode.NoContent, async (response) => new DeviceNotFoundException(await GetExceptionMessageAsync(response).ConfigureAwait(false)));
            mappings.Add(HttpStatusCode.NotFound, async (response) => new DeviceNotFoundException(await GetExceptionMessageAsync(response).ConfigureAwait(false)));
            mappings.Add(HttpStatusCode.BadRequest, async (response) => new ArgumentException(await GetExceptionMessageAsync(response).ConfigureAwait(false)));
            mappings.Add(HttpStatusCode.Unauthorized, async (response) => new UnauthorizedException(await GetExceptionMessageAsync(response).ConfigureAwait(false)));
            mappings.Add(HttpStatusCode.Forbidden, async (response) => new QuotaExceededException(await GetExceptionMessageAsync(response).ConfigureAwait(false)));
            mappings.Add(HttpStatusCode.PreconditionFailed, async (response) => new DeviceMessageLockLostException(await GetExceptionMessageAsync(response).ConfigureAwait(false)));
            mappings.Add(HttpStatusCode.RequestEntityTooLarge, async (response) => new MessageTooLargeException(await GetExceptionMessageAsync(response).ConfigureAwait(false)));
            mappings.Add(HttpStatusCode.InternalServerError, async (response) => new ServerErrorException(await GetExceptionMessageAsync(response).ConfigureAwait(false)));
            mappings.Add(HttpStatusCode.ServiceUnavailable, async (response) => new ServerBusyException(await GetExceptionMessageAsync(response).ConfigureAwait(false)));
            mappings.Add((HttpStatusCode)429, async (response) => new IotHubThrottledException(await GetExceptionMessageAsync(response).ConfigureAwait(false), null));
            return mappings;
        }

        public static Task<string> GetExceptionMessageAsync(HttpResponseMessage response)
        {
            return response.Content.ReadAsStringAsync();
        }
    }
}
