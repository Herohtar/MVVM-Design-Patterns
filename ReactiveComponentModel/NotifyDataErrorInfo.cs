namespace ReactiveComponentModel
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Reactive.Linq;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Provides functionality to provide errors for the object if it is in an invalid state.
    /// </summary>
    /// <typeparam name="T">The type of this instance.</typeparam>
    public abstract class NotifyDataErrorInfo<T> : NotifyPropertyChanges, INotifyDataErrorInfo, IDataErrorInfo
        where T : NotifyDataErrorInfo<T>
    {
        private const string HasErrorsPropertyName = "HasErrors";
        private Dictionary<string, List<object>> errors;

        /// <summary>
        /// Occurs when the validation errors have changed for a property or for the entire object.
        /// </summary>
        event EventHandler<DataErrorsChangedEventArgs> INotifyDataErrorInfo.ErrorsChanged
        {
            add { this.errorsChanged += value; }
            remove { this.errorsChanged -= value; }
        }

#pragma warning disable IDE1006 // Naming Styles
        private event EventHandler<DataErrorsChangedEventArgs> errorsChanged;
#pragma warning restore IDE1006 // Naming Styles

        /// <summary>
        /// Gets the when errors changed observable event. Occurs when the validation errors have changed for a property or for the entire object.
        /// </summary>
        /// <value>
        /// The when errors changed observable event.
        /// </value>
        public IObservable<string> WhenErrorsChanged
        {
            get
            {
                this.ThrowIfDisposed();

                return Observable
                    .FromEventPattern<DataErrorsChangedEventArgs>(
                        h => this.errorsChanged += h,
                        h => this.errorsChanged -= h)
                    .Select(x => x.EventArgs.PropertyName);
            }
        }

        /// <summary>
        /// Gets the errors for the property with the specified name.
        /// </summary>
        /// <param name="columnName">The name of the property to get errors for.</param>
        /// <value>A collection of all errors from the <see cref="IDataErrorInfo"/>. <c>null</c>
        /// if there are no errors.</value>
        string IDataErrorInfo.this[string columnName] => string.Join(". ", this.GetErrors(columnName));

        /// <summary>
        /// Gets a value indicating whether the object has validation errors.
        /// </summary>
        /// <value><c>true</c> if this instance has errors, otherwise <c>false</c>.</value>
        public virtual bool HasErrors
        {
            get
            {
                this.InitializeErrors();
                return this.errors.Count > 0;
            }
        }

        /// <summary>
        /// Gets an error message indicating what is wrong with this object.
        /// </summary>
        /// <value></value>
        /// <returns>
        /// An error message indicating what is wrong with this object. The default is an empty string ("").
        /// </returns>
        string IDataErrorInfo.Error => ((IDataErrorInfo)this)[null];

        /// <summary>
        /// Gets the rules which provide the errors.
        /// </summary>
        /// <value>The rules this instance must satisfy.</value>
        protected static RuleCollection<T> Rules { get; } = new RuleCollection<T>();

        /// <summary>
        /// Gets the validation errors for the entire object.
        /// </summary>
        /// <returns>A collection of errors.</returns>
        public IEnumerable GetErrors() => this.GetErrors(null);

        /// <summary>
        /// Gets the validation errors for a specified property or for the entire object.
        /// </summary>
        /// <param name="propertyName">Name of the property to retrieve errors for. <c>null</c> to
        /// retrieve all errors for this instance.</param>
        /// <returns>A collection of errors.</returns>
        public IEnumerable GetErrors(string propertyName)
        {
            Debug.Assert(
                string.IsNullOrEmpty(propertyName) ||
                (this.GetType().GetProperty(propertyName) != null),
                "Check that the property name exists for this instance.");

            this.InitializeErrors();

            IEnumerable result;
            if (string.IsNullOrEmpty(propertyName))
            {
                var allErrors = new List<object>();
                foreach (var keyValuePair in this.errors)
                {
                    allErrors.AddRange(keyValuePair.Value);
                }

                result = allErrors;
            }
            else
            {
                if (this.errors.ContainsKey(propertyName))
                {
                    result = this.errors[propertyName];
                }
                else
                {
                    result = Enumerable.Empty<object>();
                }
            }

            return result;
        }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            if (string.IsNullOrEmpty(propertyName))
            {
                this.ApplyRules();
            }
            else
            {
                this.ApplyRules(propertyName);
            }

            base.OnPropertyChanged(HasErrorsPropertyName);
        }

        /// <summary>
        /// Called when the errors have changed.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        protected virtual void OnErrorsChanged([CallerMemberName] string propertyName = null)
        {
            Debug.Assert(
                string.IsNullOrEmpty(propertyName) ||
                (this.GetType().GetProperty(propertyName) != null),
                "Check that the property name exists for this instance.");

            this.errorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Applies all rules to this instance.
        /// </summary>
        private void ApplyRules()
        {
            this.InitializeErrors();

            foreach (var propertyName in Rules.Select(x => x.PropertyName))
            {
                this.ApplyRules(propertyName);
            }
        }

        /// <summary>
        /// Applies the rules to this instance for the specified property.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        private void ApplyRules(string propertyName)
        {
            this.InitializeErrors();

            var propertyErrors = Rules.Apply((T)this, propertyName).ToList();
            if (propertyErrors.Count > 0)
            {
                if (this.errors.ContainsKey(propertyName))
                {
                    this.errors[propertyName].Clear();
                }
                else
                {
                    this.errors[propertyName] = new List<object>();
                }

                this.errors[propertyName].AddRange(propertyErrors);
                this.OnErrorsChanged(propertyName);
            }
            else if (this.errors.ContainsKey(propertyName))
            {
                this.errors.Remove(propertyName);
                this.OnErrorsChanged(propertyName);
            }
        }

        /// <summary>
        /// Initializes the errors and applies the rules if not initialized.
        /// </summary>
        private void InitializeErrors()
        {
            if (this.errors == null)
            {
                this.errors = new Dictionary<string, List<object>>();

                this.ApplyRules();
            }
        }
    }
}
