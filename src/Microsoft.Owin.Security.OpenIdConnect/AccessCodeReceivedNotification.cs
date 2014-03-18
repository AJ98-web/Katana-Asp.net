﻿//-----------------------------------------------------------------------
// Copyright (c) Microsoft Open Technologies, Inc.
// All Rights Reserved
// Apache License 2.0
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//-----------------------------------------------------------------------

namespace Microsoft.Owin.Security.Notifications
{
    using System.IdentityModel.Tokens;
    using System.Security.Claims;

    using Microsoft.IdentityModel.Protocols;

    /// <summary>
    /// This Notification can be used to be informed when an 'AccessCode' is received over the OpenIdConnect protocol.
    /// </summary>
    public class AccessCodeReceivedNotification
    {
        /// <summary>
        /// Creates a <see cref="AccessCodeReceivedNotification"/>
        /// </summary>
        public AccessCodeReceivedNotification() 
        { 
        }
        
        /// <summary>
        /// Gets or sets the 'code'.
        /// </summary>
        public string AccessCode { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ClaimsIdentity"/> associated with the code.
        /// </summary>
        public ClaimsIdentity ClaimsIdentity { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="JwtSecurityToken"/> associated with the code.
        /// </summary>
        public JwtSecurityToken JwtSecurityToken { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="OpenIdConnectMessage"/>.
        /// </summary>
        public OpenIdConnectMessage ProtocolMessage { get; set; }
    }
}