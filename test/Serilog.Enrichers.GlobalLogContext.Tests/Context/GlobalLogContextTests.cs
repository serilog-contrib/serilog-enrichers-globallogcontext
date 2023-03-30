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
using Xunit;
using Serilog.Core.Enrichers;
using Serilog.Events;
using Serilog.Enrichers.GlobalLogContext.Tests.Support;
using System.Linq;

namespace Serilog.Enrichers.GlobalLogContext.Tests.Context
{
    public class GlobalLogContextTests
    {
        [Fact]
        public void Pushed_properties_are_available_to_loggers()
        {
            LogEvent lastEvent = null;

            var log = new LoggerConfiguration()
                .Enrich.FromGlobalLogContext()
                .WriteTo.Sink(new DelegatingSink(e => lastEvent = e))
                .CreateLogger();

            using (Serilog.Context.GlobalLogContext.Lock())
            using (Serilog.Context.GlobalLogContext.PushProperty("A", 1))
            using (Serilog.Context.GlobalLogContext.Push(new PropertyEnricher("B", 2)))
            using (Serilog.Context.GlobalLogContext.Push(new PropertyEnricher("C", 3), new PropertyEnricher("D", 4))) // Different overload
            {
                log.Write(Some.InformationEvent());
                Assert.NotNull(lastEvent);
                Assert.Equal(1, lastEvent!.Properties["A"].LiteralValue());
                Assert.Equal(2, lastEvent.Properties["B"].LiteralValue());
                Assert.Equal(3, lastEvent.Properties["C"].LiteralValue());
                Assert.Equal(4, lastEvent.Properties["D"].LiteralValue());
            }
        }

        [Fact]
        public void More_nested_properties_override_less_nested_ones()
        {
            LogEvent lastEvent = null;

            var log = new LoggerConfiguration()
                .Enrich.FromGlobalLogContext()
                .WriteTo.Sink(new DelegatingSink(e => lastEvent = e))
                .CreateLogger();

            using (Serilog.Context.GlobalLogContext.Lock())
            using (Serilog.Context.GlobalLogContext.PushProperty("A", 1))
            {
                log.Write(Some.InformationEvent());
                Assert.NotNull(lastEvent);
                Assert.Equal(1, lastEvent!.Properties["A"].LiteralValue());

                using (Serilog.Context.GlobalLogContext.PushProperty("A", 2))
                {
                    log.Write(Some.InformationEvent());
                    Assert.Equal(2, lastEvent.Properties["A"].LiteralValue());
                }

                log.Write(Some.InformationEvent());
                Assert.Equal(1, lastEvent.Properties["A"].LiteralValue());
            }

            log.Write(Some.InformationEvent());
            Assert.False(lastEvent.Properties.ContainsKey("A"));
        }

        [Fact]
        public void Multiple_nested_properties_override_less_nested_ones()
        {
            LogEvent lastEvent = null;

            var log = new LoggerConfiguration()
                .Enrich.FromGlobalLogContext()
                .WriteTo.Sink(new DelegatingSink(e => lastEvent = e))
                .CreateLogger();

            using (Serilog.Context.GlobalLogContext.Lock())
            using (Serilog.Context.GlobalLogContext.Push(new PropertyEnricher("A1", 1), new PropertyEnricher("A2", 2)))
            {
                log.Write(Some.InformationEvent());
                Assert.NotNull(lastEvent);
                Assert.Equal(1, lastEvent!.Properties["A1"].LiteralValue());
                Assert.Equal(2, lastEvent.Properties["A2"].LiteralValue());

                using (Serilog.Context.GlobalLogContext.Push(new PropertyEnricher("A1", 10), new PropertyEnricher("A2", 20)))
                {
                    log.Write(Some.InformationEvent());
                    Assert.Equal(10, lastEvent.Properties["A1"].LiteralValue());
                    Assert.Equal(20, lastEvent.Properties["A2"].LiteralValue());
                }

                log.Write(Some.InformationEvent());
                Assert.Equal(1, lastEvent.Properties["A1"].LiteralValue());
                Assert.Equal(2, lastEvent.Properties["A2"].LiteralValue());
            }

            log.Write(Some.InformationEvent());
            Assert.False(lastEvent.Properties.ContainsKey("A1"));
            Assert.False(lastEvent.Properties.ContainsKey("A2"));
        }

        [Fact]
        public async Task GlobalLogContext_properties_cross_async_calls()
        {
            await TestWithSyncContext(async () =>
            {
                LogEvent lastEvent = null;

                var log = new LoggerConfiguration()
                    .Enrich.FromGlobalLogContext()
                    .WriteTo.Sink(new DelegatingSink(e => lastEvent = e))
                    .CreateLogger();

                using (Serilog.Context.GlobalLogContext.Lock())
                using (Serilog.Context.GlobalLogContext.PushProperty("A", 1))
                {
                    var pre = Thread.CurrentThread.ManagedThreadId;

                    await Task.Yield();

                    var post = Thread.CurrentThread.ManagedThreadId;

                    log.Write(Some.InformationEvent());
                    Assert.NotNull(lastEvent);
                    Assert.Equal(1, lastEvent!.Properties["A"].LiteralValue());

                    Assert.False(Thread.CurrentThread.IsThreadPoolThread);
                    Assert.True(Thread.CurrentThread.IsBackground);
                    Assert.NotEqual(pre, post);
                }
            },
            new ForceNewThreadSyncContext());
        }

        [Fact]
        public async Task GlobalLogContext_enrichers_in_async_scope_can_be_cleared()
        {
            LogEvent lastEvent = null;

            var log = new LoggerConfiguration()
                .Enrich.FromGlobalLogContext()
                .WriteTo.Sink(new DelegatingSink(e => lastEvent = e))
                .CreateLogger();

            using (Serilog.Context.GlobalLogContext.Lock())
            using (Serilog.Context.GlobalLogContext.Push(new PropertyEnricher("A", 1)))
            {
                await Task.Run(() =>
                {
                    Serilog.Context.GlobalLogContext.Reset();
                    log.Write(Some.InformationEvent());
                });

                Assert.NotNull(lastEvent);
                Assert.Empty(lastEvent!.Properties);

                // Reset should work for the global scope, outside of it previous Context
                // instance should not be available again.
                log.Write(Some.InformationEvent());
                Assert.Empty(lastEvent!.Properties);
            }
        }

        [Fact]
        public async Task GlobalLogContext_enrichers_can_be_temporarily_cleared()
        {
            LogEvent lastEvent = null;

            var log = new LoggerConfiguration()
                .Enrich.FromGlobalLogContext()
                .WriteTo.Sink(new DelegatingSink(e => lastEvent = e))
                .CreateLogger();

            using (Serilog.Context.GlobalLogContext.Lock())
            using (Serilog.Context.GlobalLogContext.Push(new PropertyEnricher("A", 1)))
            {
                using (Serilog.Context.GlobalLogContext.Suspend())
                {
                    await Task.Run(() =>
                    {
                        log.Write(Some.InformationEvent());
                    });

                    Assert.NotNull(lastEvent);
                    Assert.Empty(lastEvent!.Properties);
                }

                // Suspend should work for the global scope. After calling Dispose all enrichers
                // should be restored.
                log.Write(Some.InformationEvent());
                Assert.Equal(1, lastEvent.Properties["A"].LiteralValue());
            }
        }

        [Fact]
        public void GlobalLogContext_can_be_locked_synchronously()
        {
            LogEvent lastEvent = null;

            var log = new LoggerConfiguration()
                .Enrich.FromGlobalLogContext()
                .WriteTo.Sink(new DelegatingSink(e => lastEvent = e))
                .CreateLogger();

            const int maxIterations = 1000;
            const int maxDegreeOfParallelism = 10;

            Parallel.For(1, maxIterations, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism }, i =>
            {
                using (Serilog.Context.GlobalLogContext.Lock())
                {
                    Serilog.Context.GlobalLogContext.PushProperty("Lock", i);
                    log.Write(Some.InformationEvent());

                    Assert.Equal(i, lastEvent.Properties["Lock"].LiteralValue());
                }
            });
        }

        [Fact]
        public async Task GlobalLogContext_can_be_locked_asynchronously()
        {
            LogEvent lastEvent = null;

            var log = new LoggerConfiguration()
                .Enrich.FromGlobalLogContext()
                .WriteTo.Sink(new DelegatingSink(e => lastEvent = e))
                .CreateLogger();

            const int maxIterations = 1000;
            const int maxDegreeOfParallelism = 10;

            var parallelGroups = Enumerable
                .Range(0, maxIterations)
                .GroupBy(r => r % maxDegreeOfParallelism);

            var parallelTasks = parallelGroups.Select(groups =>
            {
                return Task.Run(async () =>
                {
                    using (await Serilog.Context.GlobalLogContext.LockAsync())
                    {
                        foreach (var i in groups)
                        {
                            Serilog.Context.GlobalLogContext.PushProperty("LockAsync", i);
                            log.Write(Some.InformationEvent());

                            Assert.Equal(i, lastEvent.Properties["LockAsync"].LiteralValue());
                        }
                    }
                });
            });

            await Task.WhenAll(parallelTasks);
        }

        private static async Task TestWithSyncContext(Func<Task> testAction, SynchronizationContext syncContext)
        {
            var prevCtx = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(syncContext);

            Task t;
            try
            {
                t = testAction();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevCtx);
            }

            await t;
        }
    }
}
