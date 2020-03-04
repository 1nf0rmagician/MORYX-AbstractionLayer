﻿using System.Windows.Media;
using Marvin.AbstractionLayer.UI.Aspects;
using Marvin.ClientFramework;
using Marvin.Logging;
using Marvin.Products.UI.Interaction.Properties;
using Marvin.Tools.Wcf;

namespace Marvin.Products.UI.Interaction
{
    /// <summary>
    /// Module controller handling the lifecycle of the module
    /// </summary>
    [ClientModule(ModuleName, typeof(IRecipeWorkspaceProvider))]
    public class ModuleController : WorkspaceModuleBase<ModuleConfig>, IRecipeWorkspaceProvider
    {
        internal const string ModuleName = "Products";

        public static string IconPath =
            "M43.377,21.458c0.704,0.694,0.704,1.836,0,2.54L32.26,35.103c-0.697,0.704-1.841,0.704-2.53,0.021" +
            "c-0.707-0.728-0.702-1.856-0.01-2.561l11.123-11.104C41.544,20.754,42.673,20.754,43.377,21.458z M48.659,54.039" +
            "c0.704,0.704,1.843,0.704,2.537,0.011l11.117-11.123c0.709-0.704,0.704-1.844,0-2.542c-0.694-0.697-1.838-0.697-2.543,0" +
            "L48.659,51.496C47.954,52.206,47.954,53.345,48.659,54.039z M75.306,21.836v53.087H17.598v-0.855l-5.116,5.469L4.23,71.271" +
            "l13.367-12.5V0h36.685L75.306,21.836z M55.261,20.06h9.906L55.261,9.776V20.06z M69.236,24.614H50.709V6.077h-27.04v47.009" +
            "l13.569-12.691l-4.073-4.067l11.423-11.434L58.87,39.186l-11.413,11.42l-4.091-4.096l-19.697,21.06v1.279h45.567V24.614z";

        /// <inheritdoc />
        public override Geometry Icon => Geometry.Parse(IconPath);
        /// <summary>
        /// Initializes the module
        /// </summary>
        protected override void OnInitialize()
        {
            DisplayName = Strings.ModuleController_DisplayName;

            // Register aspect factory
            Container.Register<IAspectFactory>();

            // Load ResourceDetails and RecipeDetails to the current module container
            Container.Register<IAspectConfigurator, AspectConfiguratorViewModel>();
            Container.Register<IAspectConfiguratorFactory>();
            Container.LoadComponents<IProductDetails>();
            Container.LoadComponents<IRecipeDetails>();
            Container.LoadComponents<IProductAspect>();

            // Register and start service model
            var clientFactory = Container.Resolve<IWcfClientFactory>();
            var logger = Container.Resolve<IModuleLogger>();

            var serviceModel = Products.CreateServiceModel(clientFactory, logger);

            Container.SetInstance(serviceModel);

            serviceModel.Start();
        }

        /// <summary>
        /// Will be called when the module will be selected
        /// </summary>
        protected override void OnActivate()
        {

        }

        /// <summary>
        /// Will be called when the module will be deactivated
        /// </summary>
        protected override void OnDeactivate(bool close)
        {
            if (close)
                Container.Resolve<IProductServiceModel>().Stop();
        }

        /// <summary>
        /// Will be called by selecting the module
        /// </summary>
        protected override IModuleWorkspace OnCreateWorkspace()
        {
            return Container.Resolve<IModuleWorkspace>(ProductsWorkspaceViewModel.WorkspaceName);
        }

        /// <summary>
        /// Will destroy the given workspace
        /// </summary>
        protected override void OnDestroyWorkspace(IModuleWorkspace workspace)
        {

        }

        /// <inheritdoc />
        IRecipeWorkspace IRecipeWorkspaceProvider.CreateWorkspace(string title, params long[] recipeIds)
        {
            var recipeEditorFactory = Container.Resolve<IRecipeWorkspaceFactory>();
            return recipeEditorFactory.CreateRecipeWorkspace(title, recipeIds);
        }
    }
}