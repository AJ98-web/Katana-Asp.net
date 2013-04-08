// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace",
    Target = "Owin",
    Justification = "Other types exist in this namespace; they are just located in other assemblies.")]
[assembly: SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace",
    Target = "Microsoft.Owin.Security.Basic",
    Justification = "Following the Katana convention of one namespace per assembly.")]
