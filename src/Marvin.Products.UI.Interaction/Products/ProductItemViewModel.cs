﻿using Marvin.AbstractionLayer.UI;
using Marvin.Products.UI.ProductService;

namespace Marvin.Products.UI.Interaction
{
    internal class TypeItemViewModel : TreeItemViewModel
    {
        public ProductTypeModel Model { get; private set; }

        public string TypeName => Model.Name;

        public override long Id => 0;

        /// <inheritdoc />
        public override string DisplayName => Model.DisplayName;

        public TypeItemViewModel(ProductTypeModel model)
        {
            Model = model;
            UpdateModel(model);
        }

        public void UpdateModel(ProductTypeModel model)
        {
            Model = model;
            NotifyOfPropertyChange(DisplayName);
        }
    }

    internal class ProductItemViewModel : TreeItemViewModel
    {
        /// <inheritdoc />
        public override string DisplayName => Product.DisplayName;

        public ProductInfoViewModel Product { get; private set; }

        public override long Id => Product.Id;

        public string Identifier => Product.Identifier;

        public ProductItemViewModel(ProductModel model)
        {
            UpdateModel(model);
        }

        public void UpdateModel(ProductModel model)
        {
            Product = new ProductInfoViewModel(model);

            NotifyOfPropertyChange(nameof(DisplayName));
            NotifyOfPropertyChange(nameof(Identifier));
            NotifyOfPropertyChange(nameof(Product));
        }
    }
}