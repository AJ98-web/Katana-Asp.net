﻿// <copyright file="ServerFactoryLoaderTests.cs" company="Microsoft Open Technologies, Inc.">
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
using System.Linq;
using System.Text;
using Microsoft.Owin.Builder;
using Microsoft.Owin.Host.HttpListener;
using Microsoft.Owin.Hosting.ServerFactory;
using Microsoft.Owin.Hosting.Services;
using Owin;
using Xunit;
using Xunit.Extensions;

namespace Microsoft.Owin.Hosting.Tests
{
    public class ServerFactoryLoaderTests
    {
        [Theory]
        [InlineData("Microsoft.Owin.Host.HttpListener")]
        [InlineData("Microsoft.Owin.Host.HttpListener.OwinServerFactory")]
        public void LoadWithDefaults_LoadAssemblyAndDiscoverFactory(string data)
        {
            ServerFactoryLoader loader = new ServerFactoryLoader(new ServerFactoryActivator(ServicesFactory.Create()));
            IServerFactoryAdapter serverFactory = loader.Load(data);
            Assert.NotNull(serverFactory);
            IAppBuilder builder = new AppBuilder();
            serverFactory.Initialize(builder);
            Assert.IsType<OwinHttpListener>(builder.Properties[typeof(OwinHttpListener).FullName]);
        }

        [Fact]
        public void LoadWithAssemblyName_DiscoverDefaultFactoryName()
        {
            ServerFactoryLoader loader = new ServerFactoryLoader(new ServerFactoryActivator(ServicesFactory.Create()));
            IServerFactoryAdapter serverFactory = loader.Load("Microsoft.Owin.Hosting.Tests");
            Assert.NotNull(serverFactory);
            IAppBuilder builder = new AppBuilder();
            serverFactory.Create(builder);
            Assert.Equal("Microsoft.Owin.Hosting.Tests.OwinServerFactory", builder.Properties["create.server"]);
        }

        [Theory]
        [InlineData("Microsoft.Owin.Hosting.Tests.OwinServerFactory")]
        [InlineData("Microsoft.Owin.Hosting.Tests.StaticServerFactory")]
        [InlineData("Microsoft.Owin.Hosting.Tests.InstanceServerFactory")]
        public void LoadWithAssemblyAndTypeName_Success(string data)
        {
            ServerFactoryLoader loader = new ServerFactoryLoader(new ServerFactoryActivator(ServicesFactory.Create()));
            IServerFactoryAdapter serverFactory = loader.Load(data);
            Assert.NotNull(serverFactory);
            IAppBuilder builder = new AppBuilder();
            serverFactory.Create(builder);
            Assert.Equal(data, builder.Properties["create.server"]);
        }

        [Theory]
        [InlineData("Microsoft.Owin.Hosting.Tests.OwinServerFactory, Microsoft.Owin.Hosting.Tests, Culture=neutral, PublicKeyToken=null", "Microsoft.Owin.Hosting.Tests.OwinServerFactory")]
        [InlineData("Microsoft.Owin.Hosting.Tests.StaticServerFactory, Microsoft.Owin.Hosting.Tests, Culture=neutral, PublicKeyToken=null", "Microsoft.Owin.Hosting.Tests.StaticServerFactory")]
        [InlineData("Microsoft.Owin.Hosting.Tests.InstanceServerFactory, Microsoft.Owin.Hosting.Tests, Culture=neutral, PublicKeyToken=null", "Microsoft.Owin.Hosting.Tests.InstanceServerFactory")]
        public void LoadWithAssemblyAndFullTypeName_Success(string data, string expected)
        {
            ServerFactoryLoader loader = new ServerFactoryLoader(new ServerFactoryActivator(ServicesFactory.Create()));
            IServerFactoryAdapter serverFactory = loader.Load(data);
            Assert.NotNull(serverFactory);
            IAppBuilder builder = new AppBuilder();
            serverFactory.Create(builder);
            Assert.Equal(expected, builder.Properties["create.server"]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("OwinServerFactory")]
        [InlineData("Microsoft.Owin")]
        [InlineData("Microsoft.Owin.Hosting")]
        [InlineData("Microsoft.Owin.Hosting.Tests.MissingServerFactory")]
        [InlineData("Microsoft.Owin.Hosting.Tests.Nested.MissingServerFactory")]
        public void LoadWithWrongAssemblyOrType_ReturnsNull(string data)
        {
            ServerFactoryLoader loader = new ServerFactoryLoader(new ServerFactoryActivator(ServicesFactory.Create()));
            IServerFactoryAdapter serverFactory = loader.Load(data);
            Assert.Null(serverFactory);
        }
    }
}
