﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Marvin.AbstractionLayer.UI;
using Marvin.Resources.UI.Interaction.ResourceInteraction;
using Marvin.Serialization;
using Marvin.Tools.Wcf;

namespace Marvin.Resources.UI.Interaction
{
    internal interface IResourceController : IInteractionController
    {
        /// <summary>
        /// Full type tree of the currently installed resources
        /// </summary>
        List<ResourceTypeModel> TypeTree { get; }

        /// <summary>
        /// Gets the current resource tree.
        /// </summary>
        List<ResourceModel> ResourceTree { get; }

        /// <summary>
        /// Creates a new resource with the given plugin name
        /// </summary>
        Task<ResourceModel> CreateResource(string typeName, MethodEntry constructor);

        /// <summary>
        /// Saves the given resource with all changes
        /// </summary>
        Task<ResourceModel> SaveResource(ResourceModel resource);

        /// <summary>
        /// Removed the given resource
        /// </summary>
        Task<bool> RemoveResource(long resourceId);

        /// <summary>
        /// Gets the details of a resource with the resource id
        /// </summary>
        Task<ResourceModel> GetDetails(long resourceId, int depth);

        /// <summary>
        /// Invoke method on a resource object
        /// </summary>
        Task<Entry> InvokeMethod(long resourceId, MethodEntry method);

        /// <summary>
        /// Updates the resource tree
        /// </summary>
        void UpdateTree();

        /// <summary>
        /// Occurs when the resource tree was updated
        /// </summary>
        event EventHandler ResourceTreeUpdated;
    }

    [ResourceInteractionRegistration(typeof(IResourceController))]
    internal class ResourceController : ResourceInteractionControllerBase<ResourceInteractionClient, IResourceInteraction>, IResourceController
    {
        #region Properties

        protected override string MinServerVersion => "2.0.0";

        protected override string ClientVersion => "2.0.0";

        #endregion

        ///
        public List<ResourceTypeModel> TypeTree { get; private set; }

        ///
        public List<ResourceModel> ResourceTree { get; private set; }

        public ResourceController()
        {
            ResourceTree = new List<ResourceModel>();
        }

        protected override void ClientCallback(ConnectionState state, ResourceInteractionClient client)
        {
            base.ClientCallback(state, client);

            if (IsAvailable)
            {
                Task.Run(async delegate
                {
                    await LoadResourceTree();
                });
            }
        }

        private async Task LoadResourceTree()
        {
            ResourceTree = await WcfClient.GetResourceTreeAsync();
            TypeTree = await WcfClient.GetTypeTreeAsync();
            RaiseResourceTreeLoaded();
        }

        public void UpdateTree()
        {
            Task.Run(LoadResourceTree);
        }

        public event EventHandler ResourceTreeUpdated;
        private void RaiseResourceTreeLoaded()
        {
            var handler = ResourceTreeUpdated;
            handler?.Invoke(this, EventArgs.Empty);
        }

        public Task<ResourceModel> CreateResource(string typeName, MethodEntry constructor)
        {
            return WcfClient.CreateAsync(typeName, constructor);
        }

        public Task<ResourceModel> SaveResource(ResourceModel resource)
        {
            return WcfClient.SaveAsync(resource);
        }

        public Task<bool> RemoveResource(long resourceId)
        {
            return WcfClient.RemoveAsync(resourceId);
        }

        public Task<ResourceModel> GetDetails(long resourceId, int depth)
        {
            return WcfClient.GetDetailsAsync(resourceId, depth);
        }

        public Task<Entry> InvokeMethod(long resourceId, MethodEntry method)
        {
            return WcfClient.InvokeMethodAsync(resourceId, method);
        }
    }
}