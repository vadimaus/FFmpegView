using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Linq;
using ReactiveUI;

namespace FFmpegView.AvaloniaDemo.Models
{
    public class ViewModelBase : ReactiveObject, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly ConcurrentDictionary<string, bool> QueuedNotifications = new ConcurrentDictionary<string, bool>();
        private readonly bool UseDeferredNotifications;

        protected ViewModelBase() : this(false)
        {
        }

        protected ViewModelBase(bool useDeferredNotifications)
        {
            UseDeferredNotifications = useDeferredNotifications;
        }

        /// <summary>Checks if a property already matches a desired value.  Sets the property and
        /// notifies listeners only when necessary.</summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="storage">Reference to a property with both getter and setter.</param>
        /// <param name="value">Desired value for the property.</param>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers that
        /// support CallerMemberName.</param>
        /// <param name="notifyAlso">An rray of property names to notify in addition to notifying the changes on the current property name.</param>
        /// <returns>True if the value was changed, false if the existing value matched the
        /// desired value.</returns>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "", string[] notifyAlso = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            NotifyPropertyChanged(propertyName, notifyAlso);
            return true;
        }

        protected void NotifyPropertyChanged(params string[] propertyNames) => NotifyPropertyChanged(null, propertyNames);

        private void NotifyPropertyChanged(string mainProperty, string[] auxiliaryProperties)
        {
            // Queue property notification
            if (string.IsNullOrWhiteSpace(mainProperty) == false)
                QueuedNotifications[mainProperty] = true;

            // Set the state for notification properties
            if (auxiliaryProperties != null)
            {
                foreach (var property in auxiliaryProperties)
                {
                    if (string.IsNullOrWhiteSpace(property) == false)
                        QueuedNotifications[property] = true;
                }
            }

            // Depending on operation mode, either fire the notifications in the background
            // or fire them immediately
            if (UseDeferredNotifications)
                Task.Run(NotifyQueuedProperties);
            else
                NotifyQueuedProperties();
        }

        private void NotifyQueuedProperties()
        {
            // get a snapshot of property names.
            var propertyNames = QueuedNotifications.Keys.ToArray();

            // Iterate through the properties
            foreach (var property in propertyNames)
            {
                // don't notify if we don't have a change
                if (!QueuedNotifications[property]) continue;

                // notify and reset queued state to false
                try { OnPropertyChanged(property); }
                finally { QueuedNotifications[property] = false; }
            }
        }

        /// <summary>
        /// Called when a property changes its backing value.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
    }
}
