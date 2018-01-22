﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Marvin.AbstractionLayer.Capabilities;
using Marvin.AbstractionLayer.Resources;
using Marvin.Container;
using Marvin.Model;
using Marvin.Modules;
using Marvin.Modules.ModulePlugins;
using Marvin.Resources.Model;
using Marvin.Tools;
using Newtonsoft.Json;

namespace Marvin.Resources.Management
{
    [Plugin(LifeCycle.Singleton, typeof(IResourceManager))]
    internal class ResourceManager : IResourceManager
    {
        #region Dependency Injection

        /// <summary>
        /// Type controller managing the type tree and proxy creation
        /// </summary>
        public IResourceTypeController TypeController { get; set; }

        /// <summary>
        /// Access to the database
        /// </summary>
        public IUnitOfWorkFactory UowFactory { get; set; }

        /// <summary>
        /// Error reporting in case a resource crashes
        /// </summary>
        public IModuleErrorReporting ErrorReporting { get; set; }

        /// <summary>
        /// Config of this module
        /// </summary>
        public ModuleConfig Config { get; set; }

        #endregion

        #region Fields

        /// <summary>
        /// Direct access to all resources of the tree
        /// </summary>
        private IDictionary<long, Resource> _resources;

        /// <summary>
        /// Subset of public resources
        /// </summary>
        private List<IPublicResource> _publicResources;

        private bool _disposed;
        #endregion

        #region LifeCycle

        /// 
        public void Initialize()
        {
            using (var uow = UowFactory.Create(ContextMode.AllOff))
            {
                // Create all objects
                var allResources = ResourceCreationTemplate.FetchResourceTemplates(uow);
                if (allResources.Count > 0)
                    LoadResources(allResources);
                else
                    CreateRoot(uow);
            }

            // Boot resources
            Parallel.ForEach(_resources.Values, resource =>
            {
                try
                {
                    resource.Initialize();
                }
                catch (Exception e)
                {
                    _publicResources.Remove(resource as IPublicResource);
                    ErrorReporting.ReportWarning(this, e);
                }
            });
        }

        /// <summary>
        /// Load and link all resources from the databse
        /// </summary>
        private void LoadResources(ICollection<ResourceCreationTemplate> allResources)
        {
            // Create dictionaries with initial capacity that should avoid the need of resizing
            _resources = new Dictionary<long, Resource>(allResources.Count * 2);
            _publicResources = new List<IPublicResource>(allResources.Count);

            // Create resource objects on multiple threads
            var query = from template in allResources.AsParallel()
                        select template.Instantiate(TypeController, this);
            foreach (var resource in query)
            {
                AddResource(resource, false);
            }
            // Link them to each other
            Parallel.ForEach(allResources, LinkReferences);
            // Register events after all links were set
            foreach (var resource in _resources.Values)
            {
                RegisterEvents(resource, resource as IPublicResource);
            }
        }

        /// <summary>
        /// Create root resource if the database is empty
        /// </summary>
        private void CreateRoot(IUnitOfWork uow)
        {
            // Create dictionaries with initial capacity that should avoid the need of resizing
            _resources = new Dictionary<long, Resource>(64);
            _publicResources = new List<IPublicResource>(32);

            // Create a root resource
            var root = Create(Config.RootType);
            Save(uow, root);
            uow.Save();
            AddResource(root, true);
        }

        /// <summary>
        /// Add resource to all collections and register to the <see cref="Resource.Changed"/> event
        /// </summary>
        private void AddResource(Resource instance, bool registerEvents)
        {
            IPublicResource publicResource;

            lock (_resources)
            {
                // Add to collections
                _resources[instance.Id] = instance;
                publicResource = instance as IPublicResource;
                if (publicResource != null)
                    _publicResources.Add(publicResource);

                // Register to events
                if (registerEvents)
                    RegisterEvents(instance, publicResource);
            }

            RaiseResourceAdded(publicResource);
        }

        /// <summary>
        /// Register a resources events
        /// </summary>
        private void RegisterEvents(Resource instance, IPublicResource asPublic)
        {
            instance.Changed += OnResourceChanged;
            if (asPublic != null)
                asPublic.CapabilitiesChanged += RaiseCapabilitiesChanged;
        }

        /// <summary>
        /// Register a resources events
        /// </summary>
        private void UnregisterEvents(Resource instance, IPublicResource asPublic)
        {
            instance.Changed -= OnResourceChanged;
            if (asPublic != null)
                asPublic.CapabilitiesChanged -= RaiseCapabilitiesChanged;
        }

        /// <summary>
        /// Event handler when a resource was modified and the changes need to
        /// written to storage
        /// </summary>
        private void OnResourceChanged(object sender, EventArgs eventArgs)
        {
            Save((Resource)sender);
        }

        /// <summary>
        /// Build object graph from simplified <see cref="ResourceCreationTemplate"/> and flat resource list
        /// </summary>
        private void LinkReferences(ResourceCreationTemplate creationTemplate)
        {
            LinkReferences(creationTemplate.Instance, creationTemplate.Relations);
        }

        /// <summary>
        /// Link all references of a resource
        /// </summary>
        private void LinkReferences(Resource resource, ICollection<ResourceRelationTemplate> relations)
        {
            var resourceType = resource.GetType();
            foreach (var property in ReferenceProperties(resourceType))
            {
                var referenceOverride = property.GetCustomAttribute<ReferenceOverrideAttribute>();

                // Link a single resource
                if (typeof(IResource).IsAssignableFrom(property.PropertyType) && referenceOverride == null)
                {
                    var relation = MatchingRelations(relations, property).SingleOrDefault();
                    if (relation != null)
                        property.SetValue(resource, _resources[relation.ReferenceId]);
                }
                // Link a list of resources
                else if (typeof(IEnumerable<IResource>).IsAssignableFrom(property.PropertyType) && referenceOverride == null)
                {
                    // Read attribute and get the ReferenceCollection
                    var att = property.GetCustomAttribute<ResourceReferenceAttribute>();
                    var value = (IReferenceCollection)property.GetValue(resource);

                    var matches = MatchingRelations(relations, property).ToList();
                    var resources = _resources.Where(pair => matches.Any(m => m.ReferenceId == pair.Key)).Select(pair => (IResource)pair.Value);
                    foreach (var referencedResource in resources)
                    {
                        value.UnderlyingCollection.Add(referencedResource);
                    }
                    if (att != null && att.AutoSave)
                        value.CollectionChanged += new SaveResourceTrigger(this, resource, property).OnCollectionChanged;
                }
                // Register on changes for ReferenceOverrides with AutoSave
                else if (typeof(IEnumerable<IResource>).IsAssignableFrom(property.PropertyType) && referenceOverride != null && referenceOverride.AutoSave)
                {
                    var target = resourceType.GetProperty(referenceOverride.Source);
                    var value = (IReferenceCollection)property.GetValue(resource);
                    // Reference override publish change for the source property instead
                    value.CollectionChanged += new SaveResourceTrigger(this, resource, target).OnCollectionChanged;
                }
            }
        }

        ///
        public void Start()
        {
            Parallel.ForEach(_resources.Values, resource =>
            {
                try
                {
                    resource.Start();
                }
                catch (Exception e)
                {
                    _publicResources.Remove(resource as IPublicResource);
                    ErrorReporting.ReportWarning(this, e);
                }
            });
        }

        public void Stop()
        {
            Parallel.ForEach(_resources.Values, resource =>
            {
                try
                {
                    resource.Stop();
                }
                catch (Exception e)
                {
                    ErrorReporting.ReportWarning(this, e);
                }
            });
        }

        ///
        public void Dispose()
        {
            if (_disposed)
                return;

            foreach (var resource in _resources.Values)
            {
                UnregisterEvents(resource, resource as IPublicResource);
            }

            _disposed = true;
        }

        ~ResourceManager()
        {
            Dispose();
        }

        #endregion

        public Resource Get(long id) => _resources[id];

        public Resource Create(string type)
        {
            // Create simplified template and instantiate
            var template = new ResourceCreationTemplate();
            template.Name = template.Type = type; // Initially set name to type
            var instance = template.Instantiate(TypeController, this);

            // Provide ReferenceCollections for the new instance
            LinkReferences(instance, new List<ResourceRelationTemplate>());

            return instance;
        }

        public void Save(Resource resource)
        {
            using (var uow = UowFactory.Create())
            {
                Save(uow, resource);
                uow.Save();
            }
        }

        /// <summary>
        /// A collection with "AutoSave = true" was modified. Write current state to the database
        /// </summary>
        private void AutoSaveCollection(Resource instance, PropertyInfo collectionProperty)
        {
            using (var uow = UowFactory.Create())
            {
                var entity = uow.GetEntity<ResourceEntity>(instance);
                var relations = ResourceRelationTemplate.FromEntity(uow, entity);
                var matches = MatchingRelations(relations, collectionProperty);
                UpdateCollectionReference(uow, entity, instance, collectionProperty, matches);
                uow.Save();
            }
        }

        /// <summary>
        /// Save a resource to the database
        /// </summary>
        private ResourceEntity Save(IUnitOfWork uow, Resource resource)
        {
            // Create entity and populate from object
            var entity = uow.GetEntity<ResourceEntity>(resource);
            if (entity.Id == 0)
            {
                entity.Type = resource.GetType().Name;
                LinkReferences(resource, new List<ResourceRelationTemplate>()); // Register on references for new instance
                EntityIdListener.Listen(entity, new SaveResourceTrigger(this, resource));
            }

            entity.Name = resource.Name;
            entity.LocalIdentifier = resource.LocalIdentifier;
            entity.GlobalIdentifier = resource.GlobalIdentifier;
            entity.ExtensionData = JsonConvert.SerializeObject(resource, JsonSettings.Minimal);

            // Save references
            var relations = ResourceRelationTemplate.FromEntity(uow, entity);
            foreach (var referenceProperty in ReferenceProperties(resource.GetType(), false))
            {
                var matches = MatchingRelations(relations, referenceProperty);

                // Save a single reference
                if (typeof(IResource).IsAssignableFrom(referenceProperty.PropertyType))
                {
                    UpdateSingleReference(uow, entity, resource, referenceProperty, matches);
                }
                else
                {
                    UpdateCollectionReference(uow, entity, resource, referenceProperty, matches);
                }
            }

            return entity;
        }

        /// <summary>
        /// Make sure our resource-relation graph in the database is synced to the resource object graph. This method
        /// updates single references like in the example below
        /// </summary>
        /// <example>
        /// [ResourceReference(ResourceRelationType.TransportRoute, ResourceReferenceRole.Source)]
        /// public Resource FriendResource { get; set; }
        /// </example>
        private void UpdateSingleReference(IUnitOfWork uow, ResourceEntity entity, Resource resource, PropertyInfo referenceProperty, IEnumerable<ResourceRelationTemplate> matches)
        {
            var relationRepo = uow.GetRepository<IResourceRelationRepository>();

            var referencedResource = referenceProperty.GetValue(resource) as Resource;
            var att = referenceProperty.GetCustomAttribute<ResourceReferenceAttribute>();

            var relEntity = matches.FirstOrDefault()?.Entity;
            if (relEntity == null && referencedResource != null)
            {
                // Create a new relation
                relEntity = CreateRelationForProperty(relationRepo, referenceProperty, att);
            }
            else if (relEntity != null && referencedResource == null)
            {
                // Delete a relation, that no longer exists
                relationRepo.Remove(relEntity);
                return;
            }
            else
            {
                // Relation did not exist before and still does not OR was only modified
                return;
            }

            // Set source and target of the relation depending on the reference roles
            var referencedEntity = referencedResource.Id > 0 ? uow.GetEntity<ResourceEntity>(referencedResource) : Save(uow, referencedResource);
            UpdateRelationEntity(entity, referencedEntity, relEntity, att);
        }

        /// <summary>
        /// Make sure our resource-relation graph in the database is synced to the resource object graph. This method
        /// updates a collection of references
        /// </summary>
        /// <example>
        /// [ResourceReference(ResourceRelationType.TransportRoute, ResourceReferenceRole.Source)]
        /// public IReferences&lt;Resource&gt; FriendResources { get; set; }
        /// </example>
        private void UpdateCollectionReference(IUnitOfWork uow, ResourceEntity entity, Resource resource, PropertyInfo referenceProperty, IEnumerable<ResourceRelationTemplate> matches)
        {
            var relationTemplates = matches.ToList();
            var relationRepo = uow.GetRepository<IResourceRelationRepository>();
            var referenceAtt = referenceProperty.GetCustomAttribute<ResourceReferenceAttribute>();

            // Get the value stored in the reference property
            var propertyValue = referenceProperty.GetValue(resource);
            var referencedResources = ((IEnumerable<IResource>)propertyValue).Cast<Resource>().ToList();

            // First delete references that no longer exist
            var deleted = relationTemplates.Where(m => referencedResources.All(r => r.Id != m.ReferenceId)).Select(m => m.Entity);
            relationRepo.RemoveRange(deleted);

            // Now create new relations
            var created = referencedResources.Where(r => relationTemplates.All(m => m.ReferenceId != r.Id));
            foreach (var createdReference in created)
            {
                var referencedEntity = createdReference.Id > 0 ? uow.GetEntity<ResourceEntity>(createdReference) : Save(uow, createdReference);
                var relEntity = CreateRelationForProperty(relationRepo, referenceProperty, referenceAtt);
                UpdateRelationEntity(entity, referencedEntity, relEntity, referenceAtt);
            }
        }

        /// <summary>
        /// Create a <see cref="ResourceRelation"/> entity for a property match
        /// </summary>
        private static ResourceRelation CreateRelationForProperty(IResourceRelationRepository relationRepo, PropertyInfo referenceProperty, ResourceReferenceAttribute att)
        {
            var relationType = att?.RelationType ?? ResourceRelationType.Custom;
            var relEntity = relationRepo.Create((int)relationType);
            if (relationType == ResourceRelationType.Custom)
                relEntity.RelationName = referenceProperty.Name;
            else if (!string.IsNullOrEmpty(att?.Name))
                relEntity.RelationName = att.Name;
            return relEntity;
        }

        /// <summary>
        /// Set <see cref="ResourceRelation.SourceId"/> and <see cref="ResourceRelation.TargetId"/> depending on the <see cref="ResourceReferenceRole"/>
        /// of the reference property
        /// </summary>
        private static void UpdateRelationEntity(ResourceEntity resource, ResourceEntity referencedResource, ResourceRelation relEntity, ResourceReferenceAttribute att)
        {
            if (att?.Role == ResourceReferenceRole.Source)
            {
                relEntity.Source = referencedResource;
                relEntity.Target = resource;
            }
            else
            {
                relEntity.Source = resource;
                relEntity.Target = referencedResource;
            }
        }

        private static IEnumerable<PropertyInfo> ReferenceProperties(Type resourceType, bool includeOverrides = true)
        {
            return (from property in resourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    let propertyType = property.PropertyType
                    where property.CanWrite && (typeof(IResource).IsAssignableFrom(propertyType)
                       || propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(IReferences<>))
                    where includeOverrides || !Attribute.IsDefined(property, typeof(ReferenceOverrideAttribute))
                    select property);
        }

        /// <summary>
        /// Find the relation that matches the property
        /// </summary>
        private static IEnumerable<ResourceRelationTemplate> MatchingRelations(IEnumerable<ResourceRelationTemplate> relations, PropertyInfo property)
        {
            var attribute = property.GetCustomAttribute<ResourceReferenceAttribute>();
            var matches = (from relation in relations
                           where (attribute?.Role ?? ResourceReferenceRole.Target) == relation.Role
                           where attribute?.RelationType == relation.RelationType // Typed relation without name or matching name
                                && (string.IsNullOrEmpty(attribute.Name) || attribute.Name == relation.Name)
                              || attribute?.RelationType == ResourceRelationType.Custom && attribute.Name == relation.Name // Custom relation with name
                              || property.Name == relation.Name  // Undecorated, named reference
                           select relation);
            return matches;
        }

        ///
        public bool Start(Resource resource)
        {
            try
            {
                resource.Start();
                return true;
            }
            catch (Exception e)
            {
                ErrorReporting.ReportWarning(this, e);
                return false;
            }
        }

        ///
        public bool Stop(Resource resource)
        {
            try
            {
                resource.Stop();
                return true;
            }
            catch (Exception e)
            {
                ErrorReporting.ReportWarning(this, e);
                return false;
            }
        }

        #region IResourceCreator
        public TResource Instantiate<TResource>() where TResource : Resource
        {
            return (TResource)Instantiate(typeof(TResource).Name);
        }

        public TResource Instantiate<TResource>(string type) where TResource : class, IResource
        {
            return Instantiate(type) as TResource;
        }

        public Resource Instantiate(string type)
        {
            return Create(type);
        }

        public bool Destroy(IResource resource)
        {
            return Destroy(resource, false);
        }

        public bool Destroy(IResource resource, bool permanent)
        {
            var instance = (Resource)resource;
            instance.Stop();

            // Load entity and relations to disconnect resource and remove from database
            using (var uow = UowFactory.Create())
            {
                var resourceRepository = uow.GetRepository<IResourceEntityRepository>();
                var relationRepository = uow.GetRepository<IResourceRelationRepository>();

                // Fetch entity and relations
                // Update properties on the references and get rid of relation entities
                var entity = resourceRepository.GetByKey(instance.Id);
                foreach (var relationEntity in entity.Sources.Concat(entity.Targets).ToArray())
                {
                    var referenceId = relationEntity.SourceId == instance.Id
                        ? relationEntity.TargetId
                        : relationEntity.SourceId;
                    var reference = _resources[referenceId];

                    var property = GetProperty(reference, instance);
                    if (property != null)
                        UpdateProperty(reference, instance, property);

                    if (permanent)
                        relationRepository.Remove(relationEntity);
                }

                resourceRepository.Remove(entity, permanent);

                uow.Save();
            }

            // Unregister from all events to avoid memory leaks
            UnregisterEvents(instance, instance as IPublicResource);

            // Destroy the object
            TypeController.Destroy(instance);

            // Remove from internal collections
            return _publicResources.Remove(resource as IPublicResource) | _resources.Remove(instance.Id);
        }

        private static PropertyInfo GetProperty(IResource referencedResource, IResource instance)
        {
            var type = referencedResource.GetType();
            return (from property in ReferenceProperties(type)
                        // Instead of comparing the resource type we simply look for the object reference
                    let value = property.GetValue(referencedResource)
                    where value == instance || ((value as IEnumerable<IResource>)?.Contains(instance) ?? false)
                    select property).FirstOrDefault();
        }

        private static void UpdateProperty(Resource reference, Resource instance, PropertyInfo property)
        {
            if (typeof(IEnumerable<IResource>).IsAssignableFrom(property.PropertyType))
            {
                var referenceCollection = (IReferenceCollection)property.GetValue(reference);
                referenceCollection.UnderlyingCollection.Remove(instance);
            }
            else
            {
                property.SetValue(reference, null);
            }
        }

        #endregion

        #region IResourceManagement

        public TResource GetResource<TResource>() where TResource : class, IPublicResource
        {
            return GetResource<TResource>(r => true);
        }

        public TResource GetResource<TResource>(long id) where TResource : class, IPublicResource
        {
            return GetResource<TResource>(r => r.Id == id);
        }

        public TResource GetResource<TResource>(string name) where TResource : class, IPublicResource
        {
            return GetResource<TResource>(r => r.Name == name);
        }

        public TResource GetResource<TResource>(ICapabilities requiredCapabilities) where TResource : class, IPublicResource
        {
            return GetResource<TResource>(r => requiredCapabilities.ProvidedBy(r.Capabilities));
        }

        public TResource GetResource<TResource>(Func<TResource, bool> predicate)
            where TResource : class, IPublicResource
        {
            // Public resources without capabilities are considered non-public
            var match = _publicResources.OfType<TResource>().SingleOrDefault(r => r.Capabilities != NullCapabilities.Instance && predicate(r));
            if (match == null)
                throw new ResourceNotFoundException();

            return (TResource)TypeController.GetProxy(match as Resource);
        }

        public IEnumerable<TResource> GetResources<TResource>() where TResource : class, IPublicResource
        {
            return GetResources<TResource>(r => true);
        }

        public IEnumerable<TResource> GetResources<TResource>(ICapabilities requiredCapabilities) where TResource : class, IPublicResource
        {
            return GetResources<TResource>(r => requiredCapabilities.ProvidedBy(r.Capabilities));
        }

        public IEnumerable<TResource> GetResources<TResource>(Func<TResource, bool> predicate) where TResource : class, IPublicResource
        {
            return _publicResources.OfType<TResource>().Where(r => r.Capabilities != NullCapabilities.Instance)
                .Where(predicate).Select(r => TypeController.GetProxy(r as Resource)).Cast<TResource>();
        }

        private void RaiseResourceAdded(IPublicResource newResource)
        {
            ResourceAdded?.Invoke(this, newResource);
        }
        public event EventHandler<IPublicResource> ResourceAdded;

        ///
        public event EventHandler<ICapabilities> CapabilitiesChanged;

        private void RaiseCapabilitiesChanged(object originalSender, ICapabilities capabilities)
        {
            var senderResource = (Resource)originalSender;
            var senderProxy = TypeController.GetProxy(senderResource);
            CapabilitiesChanged?.Invoke(senderProxy, capabilities);
        }
        #endregion

        /// <summary>
        /// Save resource trigger that forwards an event back to the
        /// <see cref="ResourceManager"/> to save the instance
        /// </summary>
        private class SaveResourceTrigger : EntityIdListener
        {
            private readonly ResourceManager _parent;
            private readonly Resource _instance;
            private readonly PropertyInfo _referenceProperty;

            public SaveResourceTrigger(ResourceManager parent, Resource instance, PropertyInfo referenceProperty = null)
            {
                _parent = parent;
                _instance = instance;
                _referenceProperty = referenceProperty;
            }

            protected override void AssignId(long id)
            {
                _parent.AddResource(_instance, true);
            }

            internal void OnCollectionChanged(object sender, EventArgs e)
            {
                _parent.AutoSaveCollection(_instance, _referenceProperty);
            }
        }
    }
}