﻿// <copyright file="CertificateSubjectKeyIdentifierValidator.cs" company="Microsoft Open Technologies, Inc.">
// Copyright 2011-2013 Microsoft Open Technologies, Inc. All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Owin.Security
{
    /// <summary>
    /// Provides pinned certificate validation based on the subject key identifier of the certificate.
    /// </summary>
    public class CertificateSubjectKeyIdentifierValidator : ICertificateValidator
    {
        private readonly HashSet<string> _validSubjectKeyIdentifiers;

        /// <summary>
        /// Initializes a new instance of the <see cref="CertificateSubjectKeyIdentifierValidator"/> class.
        /// </summary>
        /// <param name="validSubjectKeyIdentifiers">A set of subject key identifiers which are valid for an HTTPS request.</param>
        public CertificateSubjectKeyIdentifierValidator(IEnumerable<string> validSubjectKeyIdentifiers)
        {
            if (validSubjectKeyIdentifiers == null)
            {
                throw new ArgumentNullException("validSubjectKeyIdentifiers");
            }

            _validSubjectKeyIdentifiers = new HashSet<string>(validSubjectKeyIdentifiers, StringComparer.OrdinalIgnoreCase);

            if (_validSubjectKeyIdentifiers.Count == 0)
            {
                throw new ArgumentOutOfRangeException("validSubjectKeyIdentifiers");
            }
        }

        public bool Validate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors != SslPolicyErrors.None)
            {
                return false;
            }

            if (chain == null)
            {
                throw new ArgumentNullException("chain");
            }

            if (chain.ChainElements.Count < 2)
            {
                // Self signed.
                return false;
            }

            foreach (X509ChainElement chainElement in chain.ChainElements)
            {
                string subjectKeyIdentifier = GetSubjectKeyIdentifier(chainElement.Certificate);
                if (string.IsNullOrWhiteSpace(subjectKeyIdentifier))
                {
                    continue;
                }

                if (_validSubjectKeyIdentifiers.Contains(subjectKeyIdentifier))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetSubjectKeyIdentifier(X509Certificate2 certificate)
        {
            const string SubjectKeyIdentidierOid = "2.5.29.14";
            var extension = certificate.Extensions[SubjectKeyIdentidierOid] as X509SubjectKeyIdentifierExtension;

            return extension == null ? null : extension.SubjectKeyIdentifier;
        }
    }
}
