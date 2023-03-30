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

using Serilog;
using Serilog.Context;

namespace ConsoleSample
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
                .Enrich.FromGlobalLogContext()
                .CreateLogger();

            try
            {
                GlobalLogContext.PushProperty("A", 1);

                Log.Information("Carries property A = 1");

                using (GlobalLogContext.PushProperty("A", 2))
                using (GlobalLogContext.PushProperty("B", 1))
                {
                    Log.Information("Carries A = 2 and B = 1");
                }

                Log.Information("Carries property A = 1, again");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
