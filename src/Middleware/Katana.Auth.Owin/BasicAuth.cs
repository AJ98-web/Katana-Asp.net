﻿//-----------------------------------------------------------------------
// <copyright>
//   Copyright (c) Katana Contributors. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Katana.Auth.Owin
{
    using AppFunc = Func<IDictionary<string, object>, Task>;
    using AuthCallback = Func<IDictionary<string, object> /*env*/, string/*user*/, string/*psw*/, Task<bool>>;

    public class BasicAuth
    {
        private static readonly Encoding Encoding = Encoding.GetEncoding(28591);

        private AppFunc nextApp;
        private string challenge;
        private Options options;

        public BasicAuth(AppFunc nextApp, Options options)
        {
            this.nextApp = nextApp;
            this.options = options;

            this.challenge = "Basic";
            if (!string.IsNullOrWhiteSpace(options.Realm))
            {
                this.challenge += " realm=\"" + options.Realm + "\"";
            }
        }

        public Task Invoke(IDictionary<string, object> env)
        {
            var requestHeaders = env.Get<IDictionary<string, string[]>>(Constants.RequestHeadersKey);
            var authHeader = requestHeaders.GetHeader(Constants.AuthorizationHeader);

            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    byte[] data = Convert.FromBase64String(authHeader.Substring(6).Trim());
                    string userAndPass = Encoding.GetString(data);
                    int colonIndex = userAndPass.IndexOf(':');

                    if (colonIndex < 0)
                    {
                        env[Constants.ResponseStatusCodeKey] = 400;
                        return TaskHelpers.Completed();
                    }

                    string user = userAndPass.Substring(0, colonIndex);
                    string pass = userAndPass.Substring(colonIndex + 1);

                    return options.Authenticate(env, user, pass)
                        .Then(authenticated =>
                        {
                            if (authenticated == false)
                            {
                                // Failure, bad credentials
                                env[Constants.ResponseStatusCodeKey] = 401;
                                AppendChallengeOn401(env);
                                return TaskHelpers.Completed();
                            }

                            var scheme = env.Get<string>(Constants.RequestSchemeKey);
                            if (options.RequireEncryption && !string.Equals("HTTPS", scheme, StringComparison.OrdinalIgnoreCase))
                            {
                                // Good credentials, but SSL required
                                env[Constants.ResponseStatusCodeKey] = 401;
                                env[Constants.ResponseReasonPhraseKey] = "HTTPS Required";
                                AppendChallengeOn401(env);
                                return TaskHelpers.Completed();
                            }

                            // Success!
                            env[Constants.ServerUserKey] = new GenericPrincipal(
                                new GenericIdentity(user, "Basic"),
                                new string[0]);

                            return nextApp(env);
                        })
                        .Catch(catchInfo =>
                        {
                            // TODO: 500 error
                            return catchInfo.Throw();
                        });
                }
                catch (Exception)
                {
                    // TODO: 500 error
                    throw;
                }
            }

            // Hook the OnSendHeaders event and append our challenge if there's a 401.
            var registerOnSendingHeaders = env.Get<Action<Action<object>, object>>(Constants.ServerOnSendingHeadersKey);
            Contract.Assert(registerOnSendingHeaders != null);
            registerOnSendingHeaders(AppendChallengeOn401, env);

            return nextApp(env);
        }

        private void AppendChallengeOn401(object state)
        {
            IDictionary<string, object> env = (IDictionary<string, object>)state;
            var responseHeaders = env.Get<IDictionary<string, string[]>>(Constants.ResponseHeadersKey);
            if (env.Get<int>(Constants.ResponseStatusCodeKey) == 401)
            {
                responseHeaders.AppendHeader(Constants.WwwAuthenticateHeader, challenge);
            }
        }

        public class Options
        {
            public string Realm { get; set; }
            public bool RequireEncryption { get; set; }
            public AuthCallback Authenticate { get; set; }
        }
    }
}
