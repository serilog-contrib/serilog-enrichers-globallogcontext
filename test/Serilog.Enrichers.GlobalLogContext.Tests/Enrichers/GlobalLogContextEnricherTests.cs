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

using Serilog.Enrichers.GlobalLogContext.Tests.Support;
using Serilog.Events;
using Xunit;

namespace Serilog.Enrichers.GlobalLogContext.Tests.Enrichers
{
    public class GlobalLogContextEnricherTests
    {
        [Fact]
        public void GlobalLogContextEnricher_is_applied()
        {
            LogEvent evt = null;

            var log = new LoggerConfiguration()
                .Enrich.FromGlobalLogContext()
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            var appName = typeof(GlobalLogContextEnricherTests).Namespace;

            using (Serilog.Context.GlobalLogContext.Lock())
            {
                Serilog.Context.GlobalLogContext.PushProperty("AppName", appName);
            }

            log.Information(@"Has an AppName property");

            Assert.NotNull(evt);

            Assert.Equal(appName, (string)evt.Properties["AppName"].LiteralValue());
        }
    }
}
