// <copyright file="AdaptiveSamplerTest.cs" company="OpenTelemetry Authors">
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
// limitations under the License.
// </copyright>
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Samplers;
using Xunit;

namespace OpenTelemetry.Tests.Implementation.Trace.Samplers
{
    public class AdaptiveSamplerTest
    {
        private const string ActivityDisplayName = "MyActivityName";
        private const int NumSampleTries = 1000;
        private readonly ActivityTraceId traceId;
        private readonly ActivityContext sampledActivityContext;
        private readonly ActivityContext notSampledActivityContext;
        private readonly ActivityLink sampledLink;
        private readonly ActivityContext parent;
        private readonly ActivityKind activityKindServer = ActivityKind.Server;

        public AdaptiveSamplerTest()
        {
            this.traceId = ActivityTraceId.CreateRandom();
            var parentSpanId = ActivitySpanId.CreateRandom();
            this.parent = new ActivityContext(this.traceId, parentSpanId, ActivityTraceFlags.Recorded);
            this.sampledActivityContext = new ActivityContext(this.traceId, parentSpanId, ActivityTraceFlags.Recorded);
            this.notSampledActivityContext = new ActivityContext(this.traceId, parentSpanId, ActivityTraceFlags.None);
            this.sampledLink = new ActivityLink(this.sampledActivityContext);
        }

        [Fact]
        public void AdaptiveSampler_Initialization()
        {
            AdaptiveSampler sampler = new AdaptiveSampler(5, 0.5);

            Assert.NotNull(sampler);
            Assert.Equal(5, sampler.GetMaxTelemetryItemsPerSecond());
            Assert.Equal(0.5, sampler.ProbSampler.GetProbability());
            sampler.ProbSampler.SetProbability(0.2);
            Assert.Equal(0.2, sampler.ProbSampler.GetProbability());
        }

        [Fact]
        public void AdaptiveSampler_GetDescription()
        {
            var expectedDescription = "AdaptiveSampler{5.000000, 0.500000}";
            Assert.Equal(expectedDescription, new AdaptiveSampler(5, 0.5).Description);
        }

        [Fact]
        public void AdaptiveSampler_CheckMaxTelemetry_OutOfBound()
        {
            AdaptiveSampler sampler = new AdaptiveSampler(-5, 0.5);
            sampler.CheckMaxTelemetryItemsPerSecond(-5);
            Assert.Equal(0, sampler.GetMaxTelemetryItemsPerSecond());
        }

        [Fact]
        public void AdaptiveSampler_CheckingMovingAverageBounds()
        {
            AdaptiveSampler sampler = new AdaptiveSampler(20, 0.5);

            Assert.Equal(1.0, sampler.CheckMovingAverageBounds(1.43));
            Assert.Equal(0.0, sampler.CheckMovingAverageBounds(-0.3));
        }

        [Fact]
        public void AdaptiveSampler_CheckNumItemsSampled()
        {
            ActivitySamplingParameters parameters = new ActivitySamplingParameters(this.parent, this.traceId, "test", this.activityKindServer, null, null);
            AdaptiveSampler sampler = new AdaptiveSampler(5, 0.5);

            int numSampled = 0;

            // sends 10 events in 1 second
            int events = 10;
            for (int i = 0; i < events; i++)
            {
                SamplingResult result = sampler.ShouldSample(parameters);
                if (result.IsSampled)
                {
                    numSampled += 1;
                }

                Thread.Sleep(1000 / events);
            }

            int expectedSampled = events;
            Assert.Equal(expectedSampled, numSampled);
        }

        [Fact]
        public void AdaptiveSampler_CheckNewProbability_SameNumEvents()
        {
            double oldProbability = 0.5;
            int maxSamplesAllowed = 20;
            ActivitySamplingParameters parameters = new ActivitySamplingParameters(this.parent, this.traceId, "test", this.activityKindServer, null, null);
            AdaptiveSampler sampler = new AdaptiveSampler(maxSamplesAllowed, oldProbability);

            // run for 200 seconds to let adaptive sampling adjust
            for (int i = 0; i < 200; i++)
            {
                // send 20 events each second
                int events = 20;
                for (int j = 0; j < events; j++)
                {
                    sampler.ShouldSample(parameters);
                    Thread.Sleep(1000 / events);
                }
            }

            // since events == maxSamplesAllowed, we expect to see all sampled (probability around 1)
            double expectedProbability = 1;
            Assert.True(expectedProbability - 0.2 <= sampler.ProbSampler.GetProbability() && sampler.ProbSampler.GetProbability() <= expectedProbability + 0.2);
        }

        [Fact]
        public void AdaptiveSampler_CheckNewProbability_DecreaseMaxAllowed()
        {
            double oldProbability = 0.5;
            int maxSamplesAllowed = 5;
            ActivitySamplingParameters parameters = new ActivitySamplingParameters(this.parent, this.traceId, "test", this.activityKindServer, null, null);
            AdaptiveSampler sampler = new AdaptiveSampler(maxSamplesAllowed, oldProbability);

            // run for 200 seconds to let adaptive sampling adjust
            for (int i = 0; i < 200; i++)
            {
                // send 20 events each second
                int events = 20;
                for (int j = 0; j < events; j++)
                {
                    sampler.ShouldSample(parameters);
                    Thread.Sleep(1000 / events);
                }
            }

            // since events > maxSamplesAllowed, we expect to see less sampled (probability around 20/5 = 0.25)
            double expectedProbability = 0.25;
            Assert.True(expectedProbability - 0.2 <= sampler.ProbSampler.GetProbability() && sampler.ProbSampler.GetProbability() <= expectedProbability + 0.2);
        }

        [Fact]
        public void AdaptiveSampler_CheckNewProbability_IncreaseMaxAllowed()
        {
            double oldProbability = 0.5;
            int maxSamplesAllowed = 40;
            ActivitySamplingParameters parameters = new ActivitySamplingParameters(this.parent, this.traceId, "test", this.activityKindServer, null, null);
            AdaptiveSampler sampler = new AdaptiveSampler(maxSamplesAllowed, oldProbability);

            // run for 200 seconds to let adaptive sampling adjust
            for (int i = 0; i < 200; i++)
            {
                // send 20 events each second
                int events = 20;
                for (int j = 0; j < events; j++)
                {
                    sampler.ShouldSample(parameters);
                    Thread.Sleep(1000 / events);
                }
            }

            // since events < maxSamplesAllowed, we expect to see all sampled (probability around 1)
            double expectedProbability = 1.0;

            // Assert.True(sampler.ProbSampler.GetProbability() - 0.2 < expectedProbability && expectedProbability < sampler.ProbSampler.GetProbability() + 0.2);
            // Assert.Equal(1.0, sampler.ProbSampler.GetProbability());
            Assert.True(expectedProbability - 0.2 <= sampler.ProbSampler.GetProbability() && sampler.ProbSampler.GetProbability() <= expectedProbability + 0.2);
        }

        [Fact]
        public void AdaptiveSampler_CheckNewProbability_StaySame()
        {
            double oldProbability = 0.5;
            int maxSamplesAllowed = 10; // should stay at 0.5
            ActivitySamplingParameters parameters = new ActivitySamplingParameters(this.parent, this.traceId, "test", this.activityKindServer, null, null);
            AdaptiveSampler sampler = new AdaptiveSampler(maxSamplesAllowed, oldProbability);

            // run for 200 seconds to let adaptive sampling adjust
            for (int i = 0; i < 200; i++)
            {
                // send 20 events each second
                int events = 20;
                for (int j = 0; j < events; j++)
                {
                    sampler.ShouldSample(parameters);
                    Thread.Sleep(1000 / events);
                }
            }

            // since events > maxSamplesAllowed, we expect to less sampled (20/10 = 0.5)
            double expectedProbability = 0.5;
            Assert.True(expectedProbability - 0.2 <= sampler.ProbSampler.GetProbability() && sampler.ProbSampler.GetProbability() <= expectedProbability + 0.2);
        }

        [Fact]
        public void AdaptiveSampler_CheckNewProbability_Adjusted()
        {
            double oldProbability = 0.5;
            int maxSamplesAllowed = 10; // should stay at 0.5
            ActivitySamplingParameters parameters = new ActivitySamplingParameters(this.parent, this.traceId, "test", this.activityKindServer, null, null);
            AdaptiveSampler sampler = new AdaptiveSampler(maxSamplesAllowed, oldProbability);

            // run for 200 seconds to let adaptive sampling adjust
            for (int i = 0; i < 200; i++)
            {
                // send 20 events each second
                int events = 20;
                for (int j = 0; j < events; j++)
                {
                    sampler.ShouldSample(parameters);
                    Thread.Sleep(1000 / events);
                }
            }

            // probability should be around (10/20) 0.5
            double expectedProbability = 0.5;

            Assert.True(expectedProbability - 0.2 <= sampler.ProbSampler.GetProbability() && sampler.ProbSampler.GetProbability() <= expectedProbability + 0.2);

            // max increases, so probability should increase to around 1
            sampler.CheckMaxTelemetryItemsPerSecond(40);

            // run for 200 seconds to let adaptive sampling adjust
            for (int i = 0; i < 200; i++)
            {
                // send 20 events each second
                int events = 20;
                for (int j = 0; j < events; j++)
                {
                    sampler.ShouldSample(parameters);
                    Thread.Sleep(1000 / events);
                }
            }

            expectedProbability = 1;
            Assert.Equal(expectedProbability, sampler.ProbSampler.GetProbability());
            Assert.True(expectedProbability - 0.2 <= sampler.ProbSampler.GetProbability() && sampler.ProbSampler.GetProbability() <= expectedProbability + 0.2);
        }

        [Fact]
        public void AdaptiveSampling_EpochCalculation()
        {
            DateTime currentTime = DateTime.Now;
            double epoch = (currentTime.Millisecond / TimeSpan.FromSeconds(1).TotalMilliseconds) * TimeSpan.FromSeconds(1).TotalMilliseconds;
            Assert.Equal(currentTime.Millisecond, epoch);
        }
    }
}
