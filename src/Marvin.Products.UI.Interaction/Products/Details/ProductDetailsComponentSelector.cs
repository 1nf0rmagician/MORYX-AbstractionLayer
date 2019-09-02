﻿using Marvin.AbstractionLayer.UI;
using Marvin.Container;

namespace Marvin.Products.UI.Interaction
{
    /// <summary>
    /// Component selector for resource view models
    /// </summary>
    [Plugin(LifeCycle.Singleton)]
    internal class ProductDetailsComponentSelector : DetailsComponentSelector<IProductDetails>
    {
        public ProductDetailsComponentSelector(IContainer container) : base(container)
        {
        }
    }
}