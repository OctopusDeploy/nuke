﻿// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Nuke.Common.ValueInjection;

namespace Nuke.Common.Tooling
{
    /// <summary>
    ///     Injects a delegate for process execution. The executable name is derived from the member name or can be
    ///     passed as constructor argument. The path to the executable is resolved in the following order:
    ///     <ul>
    ///         <li>From environment variables (e.g., <c>[NAME]_EXE=path</c>)</li>
    ///         <li>From the PATH variable using <c>which</c> or <c>where</c></li>
    ///     </ul>
    /// </summary>
    /// <example>
    ///     <code>
    /// [PathTool] readonly Tool Echo;
    /// Target FooBar => _ => _
    ///     .Executes(() =>
    ///     {
    ///         var output = Echo("test");
    ///     });
    ///     </code>
    /// </example>
    [PublicAPI]
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class PathVariableAttribute : ValueInjectionAttributeBase
    {
        private readonly string _name;

        public PathVariableAttribute(string name = null)
        {
            _name = name;
        }

        public override object GetValue(MemberInfo member, object instance)
        {
            var name = _name ?? member.Name;
            return ToolResolver.TryGetEnvironmentTool(name) ??
                   ToolResolver.GetPathTool(name);
        }
    }

    [Obsolete($"Use {nameof(PathVariableAttribute)} instead")]
    public class PathExecutableAttribute : PathVariableAttribute
    {
        public PathExecutableAttribute(string name = null)
            : base(name)
        {
        }
    }
}
