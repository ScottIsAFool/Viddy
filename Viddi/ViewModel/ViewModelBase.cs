﻿using System.Threading.Tasks;
using GalaSoft.MvvmLight.Messaging;
using ScottIsAFool.Windows.Core.Logging;
using Viddi.Messaging;

namespace Viddi.ViewModel
{
    public abstract class ViewModelBase : GalaSoft.MvvmLight.ViewModelBase
    {
        public ILog Log { get; set; }

        protected ViewModelBase()
        {
            if (!IsInDesignMode)
            {
                WireMessages();
                Log = new WinLogger(GetType().FullName);
            }
        }

        protected virtual void WireMessages()
        {
            Messenger.Default.Register<PinMessage>(this, m => RaisePropertyChanged(() => IsPinned));
        }

        public virtual bool IsPinned
        {
            get { return false; }
        }

        public void SetProgressBar(string text)
        {
            ProgressIsVisible = true;
            ProgressText = text;

            UpdateProperties();
        }

        public void SetProgressBar()
        {
            ProgressIsVisible = false;
            ProgressText = string.Empty;

            UpdateProperties();
        }

        public bool ProgressIsVisible { get; set; }
        public string ProgressText { get; set; }

        public virtual void UpdateProperties() { }

        public virtual Task PinUnpin()
        {
            return Task.FromResult(0);
        }

        public virtual string GetPinFileName(bool isWideTile = false)
        {
            return string.Empty;
        }
    }
}
