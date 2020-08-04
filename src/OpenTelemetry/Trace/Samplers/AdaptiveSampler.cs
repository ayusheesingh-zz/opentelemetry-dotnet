// <copyright file="AdaptiveSampler.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace OpenTelemetry.Trace.Samplers
{
    /// <summary>
    /// Sampler implementation which will take a sample if parent Activity or any linked Activity is sampled.
    /// Otherwise, samples traces according to the max telemetry items per second.
    /// </summary>
    public sealed class AdaptiveSampler : Sampler
    {
        // change to private, public for testing
        public ProbabilitySampler ProbSampler;
        private const double Alpha = 0.7;

        private int maxTelemetryItemsPerSecond;
        private long itemsSampledIn;
        private double movingAverage;
        private long currentEpoch = -1;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveSampler"/> class.
        /// </summary>
        /// <param name="maxTelemetryItemsPerSecond">The desired max telemetry items per second of sampling.
        /// </param>
        /// <param name="initialProbability">The initial probability to sample at (in decimal form).
        /// </param>
        public AdaptiveSampler(int maxTelemetryItemsPerSecond, double initialProbability)
        {
            this.Description = "AdaptiveSampler{" + maxTelemetryItemsPerSecond.ToString("F6", CultureInfo.InvariantCulture) + ", " + initialProbability.ToString("F6", CultureInfo.InvariantCulture) + "}";

            this.CheckMaxTelemetryItemsPerSecond(maxTelemetryItemsPerSecond);
            this.ProbSampler = new ProbabilitySampler(initialProbability);

            // initial moving average
            // if we assumed probability was 0.5 and we wanted 5 samples, we were expecting to
            // see 10 raw events, and smaple them at 50% probability to get to our 5. So the initial moving average
            // amounts to be being 10
            this.movingAverage = this.maxTelemetryItemsPerSecond / this.ProbSampler.GetProbability();
        }

        /// <inheritdoc />
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            this.CalculateMovingAverage();
            return this.ProbSampler.ShouldSample(samplingParameters);
        }

        /// <summary>
        /// Get max telemetry items per second.
        /// </summary>
        /// <returns> Max telemetry items per second.
        /// </returns>
        public int GetMaxTelemetryItemsPerSecond()
        {
            return this.maxTelemetryItemsPerSecond;
        }

        /// <summary>
        /// Set max telemetry items per second to value.
        /// </summary>
        /// <param name="value"> Value to set maxTelemetryItemsPerSecond to.
        /// </param>
        public void CheckMaxTelemetryItemsPerSecond(int value)
        {
            if (value <= 0)
            {
                this.maxTelemetryItemsPerSecond = 0;
            }
            else
            {
                this.maxTelemetryItemsPerSecond = value;
            }
        }

        /// <summary>
        /// Check bounds of moving average value.
        /// </summary>
        /// <param name="val"> Moving average value that must be checked.
        /// </param>
        /// <returns> Updated moving average value if it is not within bounds.
        /// </returns>
        public double CheckMovingAverageBounds(double val)
        {
            if (val > 1)
            {
                return 1.0;
            }

            if (val < 0)
            {
                return 0.0;
            }

            return this.movingAverage;
        }

        /// <summary>
        /// Calculates Moving average of incoming Activity data.
        /// </summary>
        public void CalculateMovingAverage()
        {
            DateTime currentTime = DateTime.Now;

            // get in milliseconds
            long epoch = (long)currentTime.Subtract(DateTime.MinValue).TotalMilliseconds / 1000;
            if (this.currentEpoch == -1)
            {
                // first time around the epoch becomes the current epoch
                this.currentEpoch = epoch;
            }

            if (epoch == this.currentEpoch)
            {
                // this.itemsSampledIn++;
                Interlocked.Add(ref this.itemsSampledIn, 1);
            }
            else
            {
                this.movingAverage = (Alpha * this.itemsSampledIn) + ((1 - Alpha) * this.movingAverage);
                this.ProbSampler.SetProbability(this.maxTelemetryItemsPerSecond / this.movingAverage);

                for (long i = this.currentEpoch + 1; i < epoch; i++)
                {
                    this.movingAverage = (Alpha * 0) + ((1 - Alpha) * this.movingAverage);
                    this.ProbSampler.SetProbability(this.maxTelemetryItemsPerSecond / this.movingAverage);
                }

                this.currentEpoch = epoch;
                this.itemsSampledIn = 1;
            }
        }

        public long GetItemsSampled()
        {
            return this.itemsSampledIn;
        }
    }
}
