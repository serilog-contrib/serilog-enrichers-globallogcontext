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
using Serilog.Configuration;
using Serilog.Enrichers.GlobalLogContext;

namespace Serilog
{
    /// <summary>
    /// Extends <see cref="LoggerEnrichmentConfiguration"/> with global enrichment methods.
    /// </summary>
    public static class LoggerEnrichmentConfigurationExtensions
    {
        /// <summary>
        /// Enrich log events with properties from <see cref="Context.GlobalLogContext"/>.
        /// </summary>
        /// <returns>Configuration object allowing method chaining.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <returns>Configuration object allowing method chaining.</returns>
        public static LoggerConfiguration FromGlobalLogContext(this LoggerEnrichmentConfiguration enrich)
        {
            return enrich.With<GlobalLogContextEnricher>();
        }
    }
}
