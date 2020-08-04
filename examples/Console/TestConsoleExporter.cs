// <copyright file="TestConsoleExporter.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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
// limitations under the Licen// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Samplers;

namespace Examples.Console
{
    internal class TestConsoleExporter
    {
        public static readonly AdaptiveSampler Sampler = new AdaptiveSampler(20, 0.5);

        internal static object Run(ConsoleOptions options)
        {
            // Enable TracerProvider for the source "MyCompany.MyProduct.MyWebServer"
            // and use a single pipeline with a custom MyProcessor, and Console exporter.
            using var tracerProvider = OpenTelemetrySdk.CreateTracerProvider(
                (builder) => builder.AddActivitySource("MyCompany.MyProduct.MyWebServer")
                    .SetResource(Resources.CreateServiceResource("MyServiceName"))
                    .SetSampler(Sampler)
                    .UseConsoleExporter(opt => opt.DisplayAsJson = options.DisplayAsJson,
                                                (p) => p.AddProcessor((next) => new MyProcessor(next))));

            // The above line is required only in applications
            // which decide to use Open Telemetry.

            Thread[] threads = new Thread[6];

            long result = 0;

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() =>
                {
                    result = SampleThreadTest(5);
                });
            }

            foreach (Thread thread in threads)
            {
                thread.Start();

                System.Console.WriteLine("thread {0} has result {1}", thread.ManagedThreadId, result);
            }

            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // System.Console.WriteLine("result from threads is: {0}", result);
            return null;
        }

        private static long SampleThreadTest(int maxItemsAllowed)
        {
            long counter = 0;

            for (int j = 0; j < 200; j++)
            {
                for (int i = 0; i < 10; i++)
                {
                    // System.Console.WriteLine("on span {0}", counter);

                    // Libraries would simply write the following lines of code to
                    // emit activities, which are the .NET representation of OT Spans.
                    var source = new ActivitySource("MyCompany.MyProduct.MyWebServer");

                    // The below commented out line shows more likely code in a real world webserver.
                    // using (var parent = source.StartActivity("HttpIn", ActivityKind.Server, HttpContext.Request.Headers["traceparent"] ))
                    using (var parent = source.StartActivity("HttpIn", ActivityKind.Server))
                    {
                        // counter += 1;

                        SamplingParameters parameters = new SamplingParameters();
                        SamplingResult result = Sampler.ShouldSample(parameters);
                        if (result.IsSampled)
                        {
                            counter++;
                            System.Console.WriteLine("num of items actually sampled: {0})", counter / 200);
                        }

                        // TagNames can follow the OT guidelines
                        // from https://github.com/open-telemetry/opentelemetry-specification/tree/master/specification/trace/semantic_conventions
                        parent?.AddTag("http.method", "GET");
                        parent?.AddTag("http.host", "MyHostName");
                        if (parent != null)
                        {
                            parent.DisplayName = "HttpIn DisplayName";

                            // IsAllDataRequested is equivalent of Span.IsRecording
                            if (parent.IsAllDataRequested)
                            {
                                parent.AddTag("expensive data", "This data is expensive to obtain. Avoid it if activity is not being recorded");
                            }
                        }

                        try
                        {
                            // Actual code to achieve the purpose of the library.
                            // For websebserver example, this would be calling
                            // user middlware pipeline.

                            // There can be child activities.
                            // In this example HttpOut is a child of HttpIn.
                            using (var child = source.StartActivity("HttpOut", ActivityKind.Client))
                            {
                                child?.AddTag("http.url", "www.mydependencyapi.com");
                                try
                                {
                                    // do actual work.

                                    child?.AddEvent(new ActivityEvent("sample activity event."));
                                    child?.AddTag("http.status_code", "200");
                                }
                                catch (Exception)
                                {
                                    child?.AddTag("http.status_code", "500");
                                }
                            }

                            parent?.AddTag("http.status_code", "200");
                        }
                        catch (Exception)
                        {
                            parent?.AddTag("http.status_code", "500");
                        }
                    }

                    Thread.Sleep(1000 / 10);
                    // System.Console.WriteLine("num items sampled in: {0}", Sampler.GetItemsSampled());
                }
            }

            // System.Console.WriteLine("Spans sampled: {0}", counter);
            System.Console.WriteLine("Press Enter key to exit.");

            return Sampler.GetItemsSampled();
        }

        internal class MyProcessor : ActivityProcessor
        {
            private ActivityProcessor next;

            public MyProcessor(ActivityProcessor next)
            {
                this.next = next;
            }

            public override void OnEnd(Activity activity)
            {
                this.next.OnEnd(activity);
            }

            public override void OnStart(Activity activity)
            {
                if (activity.IsAllDataRequested)
                {
                    if (activity.Kind == ActivityKind.Server)
                    {
                        activity.AddTag("customServerTag", "Custom Tag Value for server");
                    }
                    else if (activity.Kind == ActivityKind.Client)
                    {
                        activity.AddTag("customClientTag", "Custom Tag Value for Client");
                    }
                }

                this.next.OnStart(activity);
            }

            public override Task ShutdownAsync(CancellationToken cancellationToken)
            {
                return this.next.ShutdownAsync(cancellationToken);
            }

            public override Task ForceFlushAsync(CancellationToken cancellationToken)
            {
                return this.next.ForceFlushAsync(cancellationToken);
            }
        }
    }
}
