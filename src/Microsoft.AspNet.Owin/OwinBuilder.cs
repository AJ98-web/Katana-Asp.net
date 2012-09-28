﻿using System;
using System.Configuration;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using Microsoft.AspNet.Owin.CallEnvironment;
using Owin;
using Owin.Builder;
using Owin.Loader;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace Microsoft.AspNet.Owin
{
    static class OwinBuilder
    {
        public static Func<IDictionary<string, object>, Task> Build()
        {
            var configuration = ConfigurationManager.AppSettings["owin:Configuration"];
            var loader = new DefaultLoader();
            var startup = loader.Load(configuration ?? "");
            return Build(startup);
        }

        public static Func<IDictionary<string, object>, Task> Build(Action<IAppBuilder> startup)
        {
            if (startup == null)
            {
                return null;
            }

            var builder = new AppBuilder();
            builder.Properties["builder.DefaultApp"] = NotFound;
            builder.Properties["host.TraceOutput"] = TraceTextWriter.Instance;
            builder.Properties["host.AppName"] = HostingEnvironment.SiteName;
            builder.Properties["host.OnAppDisposing"] = new Action<Action>(callback => OwinApplication.ShutdownToken.Register(callback));

            var capabilities = new Dictionary<string, object>();
            builder.Properties[Constants.ServerCapabilitiesKey] = capabilities;
            
            capabilities[Constants.ServerNameKey] = Constants.ServerName;
            capabilities[Constants.ServerVersionKey] = Constants.ServerVersion;
            capabilities[Constants.SendFileVersionKey] = Constants.SendFileVersion;

            builder.UseFunc(next => env =>
            {
                env[Constants.ServerCapabilitiesKey] = capabilities;
                return next(env);
            });

            DetectWebSocketSupport(builder);
            startup(builder);
            return builder.Build<Func<IDictionary<string, object>, Task>>();
        }

        public static readonly Func<IDictionary<string, object>, Task> NotFound = env =>
        {
            env["owin.ResponseStatusCode"] = 404;
            return TaskHelpers.Completed();
        };

        private static void DetectWebSocketSupport(IAppBuilder builder)
        {
            // There is no explicit API to detect server side websockets, just check for v4.5 / Win8.
            // Per request we can provide actual verification.
            if (Environment.OSVersion.Version >= new Version(6, 2))
            {
                try
                {
                    Assembly webSocketMiddlewareAssembly = Assembly.Load("Microsoft.WebSockets.Owin");

                    webSocketMiddlewareAssembly.GetType("Owin.WebSocketWrapperExtensions")
                        .GetMethod("UseWebSocketWrapper")
                        .Invoke(null, new object[] { builder });
                }
                catch (Exception)
                {
                    // TODO: Trace
                }
            }
            else
            {
                // TODO: Trace
            }
        }
    }
}
