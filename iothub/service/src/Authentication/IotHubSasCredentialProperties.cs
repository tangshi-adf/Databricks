﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Amqp;

#if !NET451

using System.Collections.Generic;
using System.Linq;
using Azure;
using Microsoft.Azure.Devices.Common.Data;

#endif

namespace Microsoft.Azure.Devices
{
    internal class IotHubSasCredentialProperties : IotHubConnectionProperties
    {
#if NET451

        public IotHubSasCredentialProperties()
        {
            throw new InvalidOperationException("IotHubSasCredential is not supported on NET451");
        }
#else
        private readonly AzureSasCredential _credential;

        public IotHubSasCredentialProperties(string hostName, AzureSasCredential credential) : base(hostName)
        {
            _credential = credential;
            AmqpAudience = new List<string> { AccessRights.ServiceConnect.ToString() };
        }

#endif

        public override string GetAuthorizationHeader()
        {
#if NET451
            throw new InvalidOperationException($"IotHubSasCredential is not supported on NET451");

#else
            return _credential.Signature;
#endif
        }

        public override Task<CbsToken> GetTokenAsync(Uri namespaceAddress, string appliesTo, string[] requiredClaims)
        {
#if NET451
            throw new InvalidOperationException($"IotHubSasCredential is not supported on NET451");

#else
            // Parse the SAS token to find the expiration date and time.
            // SharedAccessSignature sr=ENCODED(dh://myiothub.azure-devices.net/a/b/c?myvalue1=a)&sig=<Signature>&se=<ExpiryInSecondsFromEpochTime>[&skn=<KeyName>]
            var tokenParts = _credential.Signature.Split('&').ToList();
            IEnumerable<string> expiresAtTokenPart = tokenParts.Where(tokenPart => tokenPart.StartsWith("se=", StringComparison.OrdinalIgnoreCase));

            if (!expiresAtTokenPart.Any())
            {
                throw new InvalidOperationException($"There is no expiration time on {nameof(AzureSasCredential)} signature.");
            }

            string expiresAtStr = expiresAtTokenPart.First().Split('=')[1];
            bool isSuccess = double.TryParse(expiresAtStr, out double secondsFromEpochTime);

            if (!isSuccess)
            {
                throw new InvalidOperationException($"Invalid seconds from epoch time on {nameof(AzureSasCredential)} signature.");
            }

            var epochTime = new DateTime(1970, 1, 1);
            var timeToLiveFromEpochTime = TimeSpan.FromSeconds(secondsFromEpochTime);
            DateTime expiresAt = epochTime.Add(timeToLiveFromEpochTime);

            var token = new CbsToken(
                _credential.Signature,
                CbsConstants.IotHubSasTokenType,
                expiresAt);
            return Task.FromResult(token);
#endif
        }
    }
}
