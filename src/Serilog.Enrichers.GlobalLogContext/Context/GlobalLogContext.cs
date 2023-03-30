#region Copyright 2021-2023 C. Augusto Proiete & Contributors
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
//
#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Core.Enrichers;
using Serilog.Events;

namespace Serilog.Context
{
    /// <summary>
    /// Holds global properties that can be attached to log events. To
    /// configure, use the <see cref="LoggerEnrichmentConfigurationExtensions.FromGlobalLogContext(Configuration.LoggerEnrichmentConfiguration)" /> method.
    /// </summary>
    /// <example>
    /// Configuration:
    /// <code lang="C#">
    /// Log.Logger = new LoggerConfiguration()
    ///     .Enrich.FromGlobalLogContext()
    ///     // ... other configuration ...
    ///     .CreateLogger();
    /// </code>
    /// Usage:
    /// <code lang="C#">
    /// GlobalLogContext.PushProperty("AppVersion", buildInfo.Version);
    /// 
    /// Log.Information("The AppVersion property will be attached to this event and all others following");
    /// Log.Warning("The AppVersion property will also be attached to this event and all others following");
    /// </code>
    /// </example>
    /// <remarks>
    /// The scope of the context is global to the application and is
    /// shared between all threads.
    /// </remarks>
    public static class GlobalLogContext
    {
        private static readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
        private static ImmutableStack<ILogEventEnricher> _data;

        /// <summary>
        /// Acquires an exclusive lock on the global log context.
        /// </summary>
        /// <returns>
        /// A token that can be disposed, in order to release
        /// the exclusive lock on the global log context.
        /// </returns>
        public static IDisposable Lock()
        {
            _semaphoreSlim.Wait();
            return new ContextLock();
        }

        /// <summary>
        /// Acquires an exclusive lock on the global log context, asynchronously.
        /// </summary>
        /// <returns>
        /// A token that can be disposed, in order to release
        /// the exclusive lock on the global log context.
        /// </returns>
        public static async Task<IDisposable> LockAsync()
        {
            await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
            return new ContextLock();
        }

        /// <summary>
        /// Push a property onto the global log context, returning an <see cref="IDisposable"/>
        /// that can later be used to remove the property, along with any others that
        /// may have been pushed on top of it and not yet popped.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <returns>A handle to later remove the property from the global log context.</returns>
        /// <param name="destructureObjects">If true, and the value is a non-primitive, non-array type,
        /// then the value will be converted to a structure; otherwise, unknown types will
        /// be converted to scalars, which are generally stored as strings.</param>
        /// <returns>A token that can be disposed, in order, to pop properties back off the stack.</returns>
        public static IDisposable PushProperty(string name, object value, bool destructureObjects = false)
        {
            return Push(new PropertyEnricher(name, value, destructureObjects));
        }

        /// <summary>
        /// Push an enricher onto the global log context, returning an <see cref="IDisposable"/>
        /// that can later be used to remove the property, along with any others that
        /// may have been pushed on top of it and not yet popped.
        /// </summary>
        /// <param name="enricher">An enricher to push onto the global log context</param>
        /// <returns>A token that can be disposed, in order, to pop properties back off the stack.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="enricher"/> is <code>null</code></exception>
        public static IDisposable Push(ILogEventEnricher enricher)
        {
            if (enricher is null)
            {
                throw new ArgumentNullException(nameof(enricher));
            }

            var stack = GetOrCreateEnricherStack();
            var bookmark = new ContextStackBookmark(stack);

            Enrichers = stack.Push(enricher);

            return bookmark;
        }

        /// <summary>
        /// Push multiple enrichers onto the global log context, returning an <see cref="IDisposable"/>
        /// that can later be used to remove the property, along with any others that
        /// may have been pushed on top of it and not yet popped.
        /// </summary>
        /// <seealso cref="PropertyEnricher"/>.
        /// <param name="enrichers">Enrichers to push onto the global log context</param>
        /// <returns>A token that can be disposed, in order, to pop properties back off the stack.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="enrichers"/> is <code>null</code></exception>
        public static IDisposable Push(params ILogEventEnricher[] enrichers)
        {
            if (enrichers is null)
            {
                throw new ArgumentNullException(nameof(enrichers));
            }

            var stack = GetOrCreateEnricherStack();
            var bookmark = new ContextStackBookmark(stack);

            for (var i = 0; i < enrichers.Length; ++i)
            {
                stack = stack.Push(enrichers[i]);
            }

            Enrichers = stack;

            return bookmark;
        }

        /// <summary>
        /// Remove all enrichers from the <see cref="GlobalLogContext"/>, returning an <see cref="IDisposable"/>
        /// that can later be used to restore enrichers that were on the stack before <see cref="Suspend"/> was called.
        /// </summary>
        /// <returns>A token that can be disposed, in order, to restore properties back to the stack.</returns>
        public static IDisposable Suspend()
        {
            var stack = GetOrCreateEnricherStack();
            var bookmark = new ContextStackBookmark(stack);

            Enrichers = ImmutableStack<ILogEventEnricher>.Empty;

            return bookmark;
        }

        /// <summary>
        /// Remove all enrichers from <see cref="GlobalLogContext"/>.
        /// </summary>
        public static void Reset()
        {
            if (!(Enrichers is null) && Enrichers != ImmutableStack<ILogEventEnricher>.Empty)
            {
                Enrichers = ImmutableStack<ILogEventEnricher>.Empty;
            }
        }

        internal static void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var enrichers = Enrichers;
            if (enrichers is null || enrichers == ImmutableStack<ILogEventEnricher>.Empty)
            {
                return;
            }

            foreach (var enricher in enrichers)
            {
                enricher.Enrich(logEvent, propertyFactory);
            }
        }

        private static ImmutableStack<ILogEventEnricher> GetOrCreateEnricherStack()
        {
            var enrichers = Enrichers;
            if (enrichers is null)
            {
                enrichers = ImmutableStack<ILogEventEnricher>.Empty;
                Enrichers = enrichers;
            }

            return enrichers;
        }

        private static ImmutableStack<ILogEventEnricher> Enrichers
        {
            get => _data;
            set => _data = value;
        }

#if SUPPORTS_IASYNCDISPOSABLE
        private sealed class ContextLock : IDisposable, IAsyncDisposable
#else
        private sealed class ContextLock : IDisposable
#endif
        {
            public void Dispose()
            {
                _semaphoreSlim.Release();
            }

#if SUPPORTS_IASYNCDISPOSABLE
            public ValueTask DisposeAsync()
            {
                _semaphoreSlim.Release();
                return ValueTask.CompletedTask;
            }
#endif
        }

        private sealed class ContextStackBookmark : IDisposable
        {
            private readonly ImmutableStack<ILogEventEnricher> _bookmark;

            public ContextStackBookmark(ImmutableStack<ILogEventEnricher> bookmark)
            {
                _bookmark = bookmark;
            }

            public void Dispose()
            {
                Enrichers = _bookmark;
            }
        }
    }
}
