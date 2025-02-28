﻿// <copyright file="OpenTelemetryProtocolExporterEventSource.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Globalization;
    using System.Threading;

    [EventSource(Name = "OpenTelemetry-Exporter-OpenTelemetryProtocol")]
    internal class OpenTelemetryProtocolExporterEventSource : EventSource
    {
        public static readonly OpenTelemetryProtocolExporterEventSource Log = new OpenTelemetryProtocolExporterEventSource();

        [NonEvent]
        public void FailedToConvertToProtoDefinitionError(Exception ex)
        {
            if (Log.IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
            {
                this.FailedToConvertToProtoDefinitionError(ToInvariantString(ex));
            }
        }

        [NonEvent]
        public void FailedToReachCollector(Exception ex)
        {
            if (Log.IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
            {
                this.FailedToReachCollector(ToInvariantString(ex));
            }
        }

        [Event(1, Message = "Exporter failed to convert SpanData content into gRPC proto definition. Data will not be sent. Exception: {0}", Level = EventLevel.Error)]
        public void FailedToConvertToProtoDefinitionError(string ex)
        {
            this.WriteEvent(1, ex);
        }

        [Event(2, Message = "Exporter failed send spans to collector. Data will not be sent. Exception: {0}", Level = EventLevel.Error)]
        public void FailedToReachCollector(string ex)
        {
            this.WriteEvent(2, ex);
        }

        [Event(3, Message = "Could not translate activity from class '{0}' and method '{1}', span will not be recorded.", Level = EventLevel.Informational)]
        public void CouldNotTranslateActivity(string className, string methodName)
        {
            this.WriteEvent(3, className, methodName);
        }

        /// <summary>
        /// Returns a culture-independent string representation of the given <paramref name="exception"/> object,
        /// appropriate for diagnostics tracing.
        /// </summary>
        private static string ToInvariantString(Exception exception)
        {
            var originalUICulture = Thread.CurrentThread.CurrentUICulture;

            try
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
                return exception.ToString();
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = originalUICulture;
            }
        }
    }
}
