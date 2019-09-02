﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using C4I;
using Caliburn.Micro;
using Marvin.ClientFramework.Dialog;
using Marvin.ClientFramework.Tasks;
using Marvin.Products.UI.ProductService;
using Marvin.Tools;

namespace Marvin.Products.UI.Interaction
{
    internal class RevisionsViewModel : DialogScreen
    {
        #region Fields and Properties

        private readonly IProductServiceModel _productServiceModel;
        private TaskNotifier _taskNotifier;
        private ProductInfoViewModel _selectedRevision;
        private string _errorMessage;

        /// <summary>
        /// Base product for displaying revision
        /// </summary>
        public ProductInfoViewModel Product { get; }

        /// <summary>
        /// Current revisions of this product
        /// </summary>
        public ObservableCollection<ProductInfoViewModel> Revisions { get; } = new ObservableCollection<ProductInfoViewModel>();

        /// <summary>
        /// Command to open the selected revision
        /// </summary>
        public ICommand OpenCmd { get; }

        /// <summary>
        /// Create command to open the creation dialog
        /// </summary>
        public ICommand CreateCmd { get; }

        /// <summary>
        /// Close command to close the dialog
        /// </summary>
        public ICommand CloseCmd { get; }

        /// <summary>
        /// Task notifier to display a busy indicator
        /// </summary>
        public TaskNotifier TaskNotifier
        {
            get { return _taskNotifier; }
            private set
            {
                _taskNotifier = value;
                NotifyOfPropertyChange();
            }
        }

        /// <summary>
        /// Error message while display revisions
        /// </summary>
        public string ErrorMessage
        {
            get { return _errorMessage; }
            set
            {
                _errorMessage = value;
                NotifyOfPropertyChange();
            }
        }

        /// <summary>
        /// Currently selected revision
        /// </summary>
        public ProductInfoViewModel SelectedRevision
        {
            get { return _selectedRevision; }
            set
            {
                _selectedRevision = value;
                NotifyOfPropertyChange();
            }
        }

        public bool NewRevisionRequested { get; private set; }

        #endregion

        public RevisionsViewModel(IProductServiceModel productServiceModel, ProductInfoViewModel product)
        {
            _productServiceModel = productServiceModel;
            Product = product;

            OpenCmd = new RelayCommand(Open, CanOpen);
            CreateCmd = new RelayCommand(Create, CanCreate);
            CloseCmd = new RelayCommand(Close, CanClose);
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            DisplayName = "Revisions";

            var loadingTask = Task.Run(async delegate
            {
                try
                {
                    var revisions = await _productServiceModel.GetProducts(new ProductQuery
                    {
                        Identifier = Product.Identifier,
                        RevisionFilter = RevisionFilter.All
                    }).ConfigureAwait(false);

                    var vms = revisions.Select(r => new ProductInfoViewModel(r)).ToArray();
                    await Execute.OnUIThreadAsync(() => Revisions.AddRange(vms));
                }
                catch (Exception e)
                {
                    await Execute.OnUIThreadAsync(() => ErrorMessage = e.Message);
                }

                finally
                {
                    await Execute.OnUIThreadAsync(CommandManager.InvalidateRequerySuggested);
                }
            });

            TaskNotifier = new TaskNotifier(loadingTask);
        }

        private bool IsNotBusy() =>
            TaskNotifier == null || TaskNotifier.IsCompleted;

        private bool CanOpen(object parameters) =>
            IsNotBusy() && SelectedRevision != null;

        private void Open(object parameters) =>
            TryClose(true);

        private bool CanClose(object parameters) =>
            IsNotBusy();

        private void Close(object parameters) =>
            TryClose(false);

        private bool CanCreate(object parameters) =>
            IsNotBusy();

        private void Create(object parameters)
        {
            NewRevisionRequested = true;
            TryClose(true);
        }
    }
}