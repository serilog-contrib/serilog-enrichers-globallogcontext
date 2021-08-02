#region Copyright 2021 C. Augusto Proiete & Contributors
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
using System.Linq;
using System.Threading;
using Serilog.Events;
using Serilog.Parsing;

namespace Serilog.Enrichers.GlobalLogContext.Tests.Support
{
    internal class Some
    {
        private static int Counter;

        public static int Int() => Interlocked.Increment(ref Counter);

        public static string String(string tag = null) => (tag ?? "") + "__" + Int();

        public static TimeSpan TimeSpan() => System.TimeSpan.FromMinutes(Int());

        public static DateTime Instant() => new DateTime(2012, 10, 28) + TimeSpan();

        public static DateTimeOffset OffsetInstant() => new(Instant());

        public static LogEvent InformationEvent(DateTimeOffset? timestamp = null)
        {
            return LogEvent(timestamp, LogEventLevel.Information);
        }

        public static LogEvent LogEvent(DateTimeOffset? timestamp = null, LogEventLevel level = LogEventLevel.Information)
        {
            return new(timestamp ?? OffsetInstant(), level,
                null, MessageTemplate(), Enumerable.Empty<LogEventProperty>());
        }

        public static MessageTemplate MessageTemplate()
        {
            return new MessageTemplateParser().Parse(String());
        }
    }
}
