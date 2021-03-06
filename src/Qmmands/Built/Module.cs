﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Qmmands
{
    /// <summary>
    ///     Represents a module built using the <see cref="CommandService"/>.
    /// </summary>
    public sealed class Module
    {
        /// <summary>
        ///     Gets the name of this <see cref="Module"/>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Gets the description of this <see cref="Module"/>.
        /// </summary>
        public string Description { get; }

        /// <summary>
        ///     Gets the remarks of this <see cref="Module"/>.
        /// </summary>
        public string Remarks { get; }

        /// <summary>
        ///     Gets the <see cref="Qmmands.RunMode"/> of this <see cref="Module"/>.
        /// </summary>
        public RunMode RunMode { get; }

        /// <summary>
        ///     Gets whether this <see cref="Module"/>'s commands ignore extra arguments or not.
        /// </summary>
        public bool IgnoreExtraArguments { get; }

        /// <summary>
        ///     Gets the aliases of this <see cref="Module"/>.
        /// </summary>
        public IReadOnlyList<string> Aliases { get; }

        /// <summary>
        ///     Gets the full aliases of this <see cref="Module"/>.
        /// </summary>
        /// <remarks>
        ///     Aliases of parent modules and this module concatenated using the <see cref="CommandService.Separator"/>.
        /// </remarks>
        public IReadOnlyList<string> FullAliases { get; }

        /// <summary>
        ///     Gets the checks of this <see cref="Module"/>.
        /// </summary>
        public IReadOnlyList<CheckBaseAttribute> Checks { get; }

        /// <summary>
        ///     Gets the attributes of this <see cref="Module"/>.
        /// </summary>
        public IReadOnlyList<Attribute> Attributes { get; }

        /// <summary>
        ///     Gets the submodules of this <see cref="Module"/>.
        /// </summary>
        public IReadOnlyList<Module> Submodules { get; }

        /// <summary>
        ///     Gets the commands of this <see cref="Module"/>.
        /// </summary>
        public IReadOnlyList<Command> Commands { get; }

        /// <summary>
        ///     Gets the parent <see cref="Module"/> of this <see cref="Module"/>.
        /// </summary>
        public Module Parent { get; }

        internal CommandService Service { get; }

        internal Type Type { get; }

        internal Module(CommandService service, ModuleBuilder builder, Module parent)
        {
            Parent = parent;
            Service = service;
            Type = builder.Type;

            Description = builder.Description;
            Remarks = builder.Remarks;
            RunMode = builder.RunMode ?? Parent?.RunMode ?? Service.DefaultRunMode;
            IgnoreExtraArguments = builder.IgnoreExtraArguments ?? Parent?.IgnoreExtraArguments ?? Service.IgnoreExtraArguments;
            Aliases = builder.Aliases.ToImmutableArray();

            var fullAliases = new List<string>();
            if (Parent is null || Parent.FullAliases.Count == 0)
                fullAliases.AddRange(Aliases);

            else if (Aliases.Count == 0)
                fullAliases.AddRange(Parent.FullAliases);

            else
            {
                for (var i = 0; i < Parent.FullAliases.Count; i++)
                    for (var j = 0; j < Aliases.Count; j++)
                        fullAliases.Add(string.Concat(Parent.FullAliases[i], Service.Separator, Aliases[j]));
            }
            FullAliases = fullAliases.ToImmutableArray();

            Name = builder.Name ?? Type?.Name;

            Checks = builder.Checks.ToImmutableArray();
            Attributes = builder.Attributes.ToImmutableArray();

            var modules = new List<Module>(builder.Submodules.Count);
            for (var i = 0; i < builder.Submodules.Count; i++)
                modules.Add(builder.Submodules[i].Build(Service, this));
            Submodules = modules.ToImmutableArray();

            var commands = new List<Command>(builder.Commands.Count);
            for (var i = 0; i < builder.Commands.Count; i++)
                commands.Add(builder.Commands[i].Build(this));
            Commands = commands.ToImmutableArray();
        }

        /// <summary>
        ///     Runs checks on parent modules and this module.
        /// </summary>
        /// <param name="context"> The <see cref="ICommandContext"/> used for execution. </param>
        /// <param name="provider"> The <see cref="IServiceProvider"/> used for execution. </param>
        /// <returns>
        ///     A <see cref="SuccessfulResult"/> if all of the checks pass, otherwise a <see cref="ChecksFailedResult"/>.
        /// </returns>
        public async Task<IResult> RunChecksAsync(ICommandContext context, IServiceProvider provider = null)
        {
            if (provider is null)
                provider = EmptyServiceProvider.Instance;

            if (Parent != null)
            {
                var result = await Parent.RunChecksAsync(context, provider).ConfigureAwait(false);
                if (!result.IsSuccessful)
                    return result;
            }

            if (Checks.Count > 0)
            {
                var checkResults = (await Task.WhenAll(Checks.Select(x => RunCheckAsync(x, context, provider))).ConfigureAwait(false));
                var failedGroups = checkResults.GroupBy(x => x.Check.Group).Where(x => x.Key == null ? x.Any(y => y.Error != null) : x.All(y => y.Error != null)).ToArray();
                if (failedGroups.Length > 0)
                    return new ChecksFailedResult(this, failedGroups.SelectMany(x => x).Where(x => x.Error != null).ToImmutableList());
            }

            return new SuccessfulResult();
        }

        private async Task<(CheckBaseAttribute Check, string Error)> RunCheckAsync(CheckBaseAttribute check, ICommandContext context, IServiceProvider provider)
        {
            var checkResult = await check.CheckAsync(context, provider).ConfigureAwait(false);
            return (check, checkResult.Error);
        }

        /// <summary>
        ///     Returns this <see cref="Module"/>'s name or calls <see cref="object.ToString"/> if the name is null.
        /// </summary>
        /// <returns>
        ///     A <see cref="string"/> representing this command.
        /// </returns>
        public override string ToString()
            => Name ?? base.ToString();
    }
}
