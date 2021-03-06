﻿using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Serilog.Events;
using Serilog.Sinks.Graylog.Extensions;
using Serilog.Sinks.Graylog.Helpers;
using Serilog.Sinks.Graylog.MessageBuilders.PropertyNaming;

namespace Serilog.Sinks.Graylog.MessageBuilders
{
    /// <summary>
    /// Message builder
    /// </summary>
    /// <seealso cref="Serilog.Sinks.Graylog.MessageBuilders.IMessageBuilder" />
    public class GelfMessageBuilder : IMessageBuilder
    {
        private readonly string hostName;
        private const string GelfVersion = "1.1";
        private readonly IPropertyNamingStrategy propertyNamingStrategy;
        protected GraylogSinkOptions Options { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GelfMessageBuilder"/> class.
        /// </summary>
        /// <param name="hostName">Name of the host.</param>
        /// <param name="options">The options.</param>
        public GelfMessageBuilder(string hostName = null, GraylogSinkOptions options = null)
        {
            Options = options ?? new GraylogSinkOptions();
            this.hostName = string.IsNullOrWhiteSpace(hostName) ? "localhost" : hostName;
            propertyNamingStrategy = options.PropertyNamingStrategy ?? new NoOpPropertyNamingStrategy();
        }

        /// <summary>
        /// Builds the specified log event.
        /// </summary>
        /// <param name="logEvent">The log event.</param>
        /// <returns></returns>
        public virtual JObject Build(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage();
            var shortMessage = message.Truncate(Options.ShortMessageMaxLength);

            var gelfMessage = new GelfMessage
            {
                Version = GelfVersion,
                Host = hostName,
                ShortMessage = shortMessage,
                FullMessage = message,
                Timestamp = logEvent.Timestamp.DateTime.ConvertToNix(),
                Level = LogLevelMapper.GetMappedLevel(logEvent.Level),
                StringLevel = logEvent.Level.ToString(),
                Facility = Options.Facility
            };

            var jsonObject = JObject.FromObject(gelfMessage);
            foreach (var property in logEvent.Properties)
            {
                AddAdditionalField(jsonObject, property);
            }

            return jsonObject;
        }


        private void AddAdditionalField(IDictionary<string, JToken> jObject,
                                        KeyValuePair<string, LogEventPropertyValue> property,
                                        string memberPath = "")
        {
            var propertyName = propertyNamingStrategy.GetPropertyName(property.Key);
            var key = string.IsNullOrWhiteSpace(memberPath)
                ? propertyName
                : $"{memberPath}.{propertyName}";

            switch (property.Value)
            {
                case ScalarValue scalarValue:
                    if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
                        key = "id_";
                    if (!key.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                        key = "_" + key;

                    if (scalarValue.Value == null)
                    {
                        jObject.Add(key, null);
                        return;
                    }

                    var shouldCallToString = SholdCallToString(scalarValue.Value.GetType());

                    var value = JToken.FromObject(shouldCallToString ? scalarValue.Value.ToString() : scalarValue.Value);

                    jObject.Add(key, value);
                    return;
                case SequenceValue sequenceValue:
                    var sequenceValuestring = RenderPropertyValue(sequenceValue);
                    jObject.Add(key, sequenceValuestring);
                    return;
                case StructureValue structureValue:
                    foreach (var logEventProperty in structureValue.Properties)
                    {
                        AddAdditionalField(jObject,
                                           new KeyValuePair<string, LogEventPropertyValue>(logEventProperty.Name, logEventProperty.Value), key);
                    }
                    return;
                default:
                    return;
            }
        }

        private bool SholdCallToString(Type type)
        {
            var isNumeric = type.IsNumericType();
            if (type == typeof(DateTime) || isNumeric)
            {
                return false;
            }
            return true;
        }

        private string RenderPropertyValue(LogEventPropertyValue propertyValue)
        {
            using (TextWriter tw = new StringWriter())
            {
                propertyValue.Render(tw);
                var result = tw.ToString();
                result = result.Trim('"');
                return result;
            }
        }
    }
}