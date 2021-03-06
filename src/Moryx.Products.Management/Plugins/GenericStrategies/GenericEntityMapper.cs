// Copyright (c) 2020, Phoenix Contact GmbH & Co. KG
// Licensed under the Apache License, Version 2.0

using System;
using System.Linq;
using Moryx.AbstractionLayer;
using Moryx.Container;
using Moryx.Products.Model;
using Moryx.Serialization;
using Moryx.Tools;
using Newtonsoft.Json;

namespace Moryx.Products.Management
{
    /// <summary>
    /// Reusable component to map business objects onto entities of type
    /// <see cref="IGenericColumns"/>
    /// </summary>
    [Component(LifeCycle.Transient, typeof(GenericEntityMapper<,>))]
    internal class GenericEntityMapper<TBase, TReference> : IGenericMapper
        where TReference : class
    {
        /// <summary>
        /// Injected factory for property mappers
        /// </summary>
        public IPropertyMapperFactory MapperFactory { get; set; }

        private IPropertyMapper[] _configuredMappers;

        private JsonSerializerSettings _jsonSettings;

        private IPropertyAccessor<IGenericColumns, string> JsonAccessor { get; set; }


        public void Initialize(Type concreteType, IGenericMapperConfiguration config)
        {
            // Get JSON accessor
            var jsonColumn = typeof(IGenericColumns).GetProperty(config.JsonColumn);
            JsonAccessor = ReflectionTool.PropertyAccessor<IGenericColumns, string>(jsonColumn);

            var baseProperties = typeof(TBase).GetProperties()
                .Select(p => p.Name);
            var configuredProperties = config.PropertyConfigs.Select(cm => cm.PropertyName);
            var ignoredProperties = baseProperties.Concat(configuredProperties).ToArray();
            _jsonSettings = JsonSettings.Minimal
                .Overwrite(j => j.ContractResolver = new DifferentialContractResolver<TReference>(ignoredProperties));

            _configuredMappers = config.PropertyConfigs.Select(pc => MapperFactory.Create(pc, concreteType)).ToArray();
        }

        public bool HasChanged(IGenericColumns storage, object instance)
        {
            // Compare JSON and mappers to entity
            var json = JsonConvert.SerializeObject(instance, _jsonSettings);
            return JsonAccessor.ReadProperty(storage) != json || _configuredMappers.Any(m => m.HasChanged(storage, instance));
        }

        public void ReadValue(IGenericColumns source, object target)
        {
            // Use all configured mappers
            var properties = source;
            foreach (var mapper in _configuredMappers)
            {
                mapper.ReadValue(properties, target);
            }

            // Fill the rest from JSON
            var json = JsonAccessor.ReadProperty(source);
            if (!string.IsNullOrEmpty(json))
                JsonConvert.PopulateObject(json, target, _jsonSettings);

        }

        public void WriteValue(object source, IGenericColumns target)
        {
            // Convert and write JSON
            var json = JsonConvert.SerializeObject(source, _jsonSettings);
            JsonAccessor.WriteProperty(target, json);

            // Execute property mappers
            foreach (var mapper in _configuredMappers)
            {
                mapper.WriteValue(source, target);
            }
        }
    }
}
